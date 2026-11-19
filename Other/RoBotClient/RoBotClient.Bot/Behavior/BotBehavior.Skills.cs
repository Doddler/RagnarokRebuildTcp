using System.Globalization;
using RebuildSharedData.Enum;

namespace RoBotClient.Bot.Behavior;

// Per-bot skill scripting (#5). The user/MCP writes a short rule script (see SKILL_SCRIPT.md); each combat
// tick we evaluate the rules top-to-bottom and fire the first one whose conditions match and whose skill is
// known + off our local throttle. Unparseable lines are skipped — the parser also exposes diagnostics so the
// MCP can surface them when the script is set.
public sealed partial class BotBehavior
{
    public enum SkillCastTarget { Enemy, SelfCast, Ground, Ally }

    public readonly record struct SkillCond(string Lhs, string Op, double Value);

    public sealed record SkillRule(
        CharacterSkill Skill, int Level, SkillCastTarget Target, double EveryCooldown,
        IReadOnlyList<SkillCond> Conditions);

    private string? _skillScriptSource;
    private List<SkillRule> _skillRules = new();
    private DateTime _nextSkill = DateTime.MinValue;
    private readonly Dictionary<CharacterSkill, DateTime> _skillCooldown = new();
    private string? _lastSkillSkipReason; // deduped log: which rule(s) were blocked + why

    // MCP queue: agents can fire-and-forget a sequence of casts. The tick loop drains one at a time
    // honoring the same global + per-skill throttles as the script engine, so the queue can't exceed the
    // server's cast rate. Enqueue + execute are both logged so the agent can correlate timing.
    public sealed record QueuedSkill(int Seq, CharacterSkill Skill, int Level, SkillCastTarget Target,
        int TargetId, int X, int Y, DateTime QueuedAt);

    private readonly object _skillQueueLock = new();
    private readonly Queue<QueuedSkill> _skillQueue = new();
    private int _skillQueueSeq;

    public int SkillQueueSize { get { lock (_skillQueueLock) return _skillQueue.Count; } }

    /// <summary>Enqueue a one-shot skill cast. Returns the seq number so the caller can correlate the
    /// later "Skill executing #N" log line.</summary>
    public int QueueSkill(CharacterSkill skill, int level, SkillCastTarget target, int targetId, int x, int y)
    {
        int seq;
        int size;
        lock (_skillQueueLock)
        {
            seq = ++_skillQueueSeq;
            _skillQueue.Enqueue(new QueuedSkill(seq, skill, level, target, targetId, x, y, DateTime.UtcNow));
            size = _skillQueue.Count;
        }
        var desc = target switch
        {
            SkillCastTarget.SelfCast => "self",
            SkillCastTarget.Ground => $"ground ({x},{y})",
            SkillCastTarget.Ally => targetId > 0 ? $"ally id={targetId}" : "ally (auto-pick injured)",
            _ => targetId > 0 ? $"enemy id={targetId}" : "enemy (current target)",
        };
        OnLog?.Invoke($"Skill queued #{seq}: {skill} L{level} ({desc}). Queue size: {size}.");
        return seq;
    }

    /// <summary>Drop every pending queued cast. Returns the number cleared.</summary>
    public int ClearSkillQueue()
    {
        int cleared;
        lock (_skillQueueLock)
        {
            cleared = _skillQueue.Count;
            _skillQueue.Clear();
        }
        if (cleared > 0) OnLog?.Invoke($"Skill queue cleared ({cleared} pending dropped).");
        return cleared;
    }

    /// <summary>Snapshot of the pending queue (for MCP inspection).</summary>
    public IReadOnlyList<QueuedSkill> PeekSkillQueue()
    {
        lock (_skillQueueLock) return _skillQueue.ToList();
    }

    /// <summary>Pop the head of the queue when allowed (throttles + skill known). Returns true if a packet
    /// was sent this tick. Called from TickAsync between hazard avoidance and ally rules so user-directed
    /// casts take priority over script-driven ones.</summary>
    private async Task<bool> TickQueuedSkillsAsync(Snapshot snap, CancellationToken ct)
    {
        if (DateTime.UtcNow < _nextSkill) return false;

        QueuedSkill? head;
        lock (_skillQueueLock) { _skillQueue.TryPeek(out var p); head = p; }
        if (head == null) return false;

        // Per-skill throttle — wait without dequeuing so the head retries next tick.
        if (_skillCooldown.TryGetValue(head.Skill, out var nextOk) && DateTime.UtcNow < nextOk) return false;

        if (!KnowsSkill(head.Skill, out var knownLevel))
        {
            lock (_skillQueueLock) _skillQueue.TryDequeue(out _);
            OnLog?.Invoke($"Queued skill #{head.Seq} {head.Skill}: not known — dropping.");
            return false;
        }

        var level = head.Level > 0 ? Math.Min(head.Level, knownLevel) : knownLevel;
        if (level <= 0)
        {
            lock (_skillQueueLock) _skillQueue.TryDequeue(out _);
            OnLog?.Invoke($"Queued skill #{head.Seq} {head.Skill}: resolved level 0 — dropping.");
            return false;
        }

        // Resolve target. For enemy with no targetId, use the bot's current committed target. For ally with
        // no targetId, find the most-injured visible player. Drop+log if we can't satisfy.
        var targetId = head.TargetId;
        if (head.Target == SkillCastTarget.Enemy && targetId == 0) targetId = _targetId;
        if (head.Target == SkillCastTarget.Ally && targetId == 0)
        {
            var ally = FindMostInjuredVisiblePlayer();
            if (ally != null) targetId = ally.Id;
        }
        if ((head.Target == SkillCastTarget.Enemy || head.Target == SkillCastTarget.Ally) && targetId == 0)
        {
            lock (_skillQueueLock) _skillQueue.TryDequeue(out _);
            OnLog?.Invoke($"Queued skill #{head.Seq} {head.Skill}: no resolvable target — dropping.");
            return false;
        }

        // Dequeue + cast. Stamp the per-cast cooldown so a script rule on the same skill doesn't fire too.
        lock (_skillQueueLock) _skillQueue.TryDequeue(out _);
        _nextSkill = DateTime.UtcNow.AddSeconds(0.5);
        _skillCooldown[head.Skill] = DateTime.UtcNow.AddSeconds(1.5);
        var latencyMs = (int)(DateTime.UtcNow - head.QueuedAt).TotalMilliseconds;
        OnLog?.Invoke($"Skill executing #{head.Seq}: {head.Skill} L{level} ({head.Target}) — queued {latencyMs} ms ago.");

        switch (head.Target)
        {
            case SkillCastTarget.SelfCast:
                await _bot.UseSkillSelfAsync(head.Skill, level, ct);
                break;
            case SkillCastTarget.Ground:
                await _bot.UseSkillGroundAsync(head.Skill, level, head.X, head.Y, ct);
                break;
            default: // Enemy or Ally — both ride the single-target wire path
                await _bot.UseSkillOnTargetAsync(head.Skill, level, targetId, ct);
                break;
        }
        return true;
    }

    /// <summary>Feed SkillPolicy from the live snapshot + the forecast's per-swing damage. 0 = skip this cast.</summary>
    private int SkillPolicyLevel(Snapshot snap, MonsterInfo target, CharacterSkill skill, int maxLevel, bool pinLevel)
    {
        var debuff = SkillPolicy.AppliedDebuff(skill);
        var hasDebuff = debuff != null && snap.EntityStatuses.TryGetValue(target.Id, out var set) && set.Contains(debuff.Value);
        var (autoDmg, hp) = PolicyInputs(snap, target);
        Func<int, double>? land = null;
        var poisonedAuto = 0.0;
        if (debuff == CharacterStatusEffect.Poison && !hasDebuff && _data?.Monster(target.ClassId) is { } mob)
        {
            var resist = SkillPolicy.PoisonResist(mob.Vit, string.Equals(mob.Special, "Boss", StringComparison.OrdinalIgnoreCase),
                ElementChart.Parse(mob.Element ?? "").Item1 == SimElement.Undead);
            var mobLuk = _data.MonsterLuk(target.ClassId);
            land = lv => SkillPolicy.LuckModifiedChance(SkillPolicy.DebuffChance(skill, lv) * resist, snap.SelfLuk, mobLuk);
            if (!_config.NoAutoAttack && autoDmg > 0)
            {
                var f = Forecast(snap, target, poisoned: true);
                poisonedAuto = f.ExpectedDamagePerSwing;
            }
        }
        return SkillPolicy.ChooseLevel(skill, maxLevel, pinLevel, _data?.SkillInfo(skill)?.SpCost,
            hp, hasDebuff, autoDmg, snap.SelfSp, SkillPolicy.SpIsFree(snap.SelfSp, snap.SelfMaxSp, snap.SelfInt),
            land, snap.SelfAttackInterval > 0.05 ? snap.SelfAttackInterval : 1.5, poisonedAuto);
    }

    /// <summary>Expected damage per auto-attack (0 for NoAutoAttack) and the target's HP (MaxHp 0 = not seen yet: fresh mob).</summary>
    private (double AutoDmg, int Hp) PolicyInputs(Snapshot snap, MonsterInfo target)
    {
        var autoDmg = 0.0;
        if (!_config.NoAutoAttack)
        {
            var f = Forecast(snap, target);
            autoDmg = f.ExpectedDamagePerSwing;
        }
        var hp = target.MaxHp > 0 ? target.Hp : _data?.Monster(target.ClassId)?.HP ?? target.Hp;
        return (autoDmg, hp);
    }

    private bool AoeWorthIt(Snapshot snap, MonsterInfo target, CharacterSkill skill, int level, int enemiesInArea)
    {
        if (enemiesInArea >= 2 || SkillPolicy.SelfBuff(skill, level) is not { } buff) return enemiesInArea >= 2;
        if (snap.SelfStatuses.Contains(buff.Status)) return false;
        var (autoDmg, hp) = PolicyInputs(snap, target);
        if (autoDmg <= 0) return false; // the buff rides on auto-attacks
        // Buff element vs the target, relative to the neutral swing autoDmg already includes.
        var (ele, eleLv) = ElementChart.Parse(_data?.Monster(target.ClassId)?.Element ?? "");
        var eleMod = ElementChart.GetModifier(buff.Element, ele, eleLv) / (double)Math.Max(1, ElementChart.GetModifier(SimElement.Neutral, ele, eleLv));
        var interval = snap.SelfAttackInterval > 0.05 ? snap.SelfAttackInterval : 1.5;
        var saved = SkillPolicy.BuffCastHitsSaved(skill, level, hp, autoDmg, interval, eleMod);
        var cost = SkillPolicy.SpCost(_data?.SkillInfo(skill)?.SpCost, level);
        return SkillPolicy.AoeWorthIt(skill, level, enemiesInArea, false, saved, cost, BestSingleTargetSwingsPerSp(snap, target, autoDmg, hp));
    }

    /// <summary>Best swings-saved-per-SP among the script's single-target damage rules on this target: the SP a buff
    /// cast competes with. 0 when the script has none (or none would fire).</summary>
    private double BestSingleTargetSwingsPerSp(Snapshot snap, MonsterInfo target, double autoDmg, int hp)
    {
        var best = 0.0;
        var spFree = SkillPolicy.SpIsFree(snap.SelfSp, snap.SelfMaxSp, snap.SelfInt);
        foreach (var r in GetSkillRules())
        {
            if (r.Target != SkillCastTarget.Enemy || SkillPolicy.Aoe(r.Skill) != null || SkillPolicy.DamageMultiplier(r.Skill, 1) <= 0) continue;
            if (!KnowsSkill(r.Skill, out var known)) continue;
            var costs = _data?.SkillInfo(r.Skill)?.SpCost;
            var lv = SkillPolicy.ChooseLevel(r.Skill, r.Level > 0 ? Math.Min(r.Level, known) : known, r.Level > 0, costs,
                hp, false, autoDmg, snap.SelfSp, spFree);
            if (lv <= 0) continue;
            best = Math.Max(best, (double)SkillPolicy.HitsSaved(hp, autoDmg, autoDmg * SkillPolicy.DamageMultiplier(r.Skill, lv)) / SkillPolicy.SpCost(costs, lv));
        }
        return best;
    }

    private MonsterInfo? FindMostInjuredVisiblePlayer()
    {
        return _bot.WithState(w =>
        {
            MonsterInfo? best = null;
            var bestHpPct = double.MaxValue;
            foreach (var e in w.Entities.Values)
            {
                if (!e.IsPlayer) continue;
                if (e.Id == w.Self.EntityId) continue;
                var hpPct = e.MaxHp > 0 ? 100.0 * e.Hp / e.MaxHp : 100.0;
                if (hpPct < bestHpPct)
                {
                    bestHpPct = hpPct;
                    best = new MonsterInfo(e.Id, e.ClassId, e.Name, e.Position, e.Hp, e.MaxHp);
                }
            }
            return best;
        });
    }

    private List<SkillRule> GetSkillRules()
    {
        var src = _config.SkillScript ?? "";
        if (!string.Equals(src, _skillScriptSource, StringComparison.Ordinal))
        {
            _skillScriptSource = src;
            _skillRules = ParseSkillScript(src, null);
        }
        return _skillRules;
    }

    private async Task<bool> TickSkillsAsync(Snapshot snap, MonsterInfo target, CancellationToken ct)
    {
        var rules = GetSkillRules();
        if (rules.Count == 0) return false;
        if (DateTime.UtcNow < _nextSkill) return false;

        // Collect skip reasons per rule so the user can see WHY their script didn't fire (deduped below).
        List<string>? skipReasons = null;
        foreach (var rule in rules)
        {
            if (rule.Target == SkillCastTarget.Ally) continue; // ally rules are handled in TickAllyRulesAsync
            if (_skillCooldown.TryGetValue(rule.Skill, out var nextOk) && DateTime.UtcNow < nextOk)
            {
                (skipReasons ??= new()).Add($"{rule.Skill}: local cooldown");
                continue;
            }
            if (!KnowsSkill(rule.Skill, out var knownLevel))
            {
                (skipReasons ??= new()).Add($"{rule.Skill}: not known by this bot");
                continue;
            }
            if (FailedCondition(rule.Conditions, snap, target) is { } failed)
            {
                (skipReasons ??= new()).Add($"{rule.Skill}: {failed}");
                continue;
            }

            var level = rule.Level > 0 ? Math.Min(rule.Level, knownLevel) : knownLevel;
            if (level <= 0)
            {
                (skipReasons ??= new()).Add($"{rule.Skill}: level resolves to 0");
                continue;
            }

            if (SkillPolicy.Aoe(rule.Skill) is { } aoe)
            {
                var center = aoe.OnSelf ? snap.SelfPos : target.Pos;
                var hit = 0;
                foreach (var m in snap.Monsters)
                    if ((m.MaxHp <= 0 || m.Hp > 0) && Math.Max(Math.Abs(m.Pos.X - center.X), Math.Abs(m.Pos.Y - center.Y)) <= aoe.Radius) hit++;
                if (!AoeWorthIt(snap, target, rule.Skill, level, hit))
                {
                    (skipReasons ??= new()).Add($"{rule.Skill}: area skill would hit {hit} enemy (and its buff is up or not worth the SP here)");
                    continue;
                }
            }
            else if (rule.Target == SkillCastTarget.Enemy)
            {
                level = SkillPolicyLevel(snap, target, rule.Skill, level, pinLevel: rule.Level > 0);
                if (level <= 0)
                {
                    (skipReasons ??= new()).Add($"{rule.Skill}: not worth the SP (debuff already up, or saves no swing)");
                    continue;
                }
            }

            var zenyCost = SkillZenyCost(rule.Skill, level);
            if (zenyCost > 0)
            {
                if (_skillZenyLeft < 0) _skillZenyLeft = ConvenienceBudget(_bot.WithState(w => w.Self.Zeny), ZenyForTripHome(snap));
                if (zenyCost > _skillZenyLeft)
                {
                    (skipReasons ??= new()).Add($"{rule.Skill}: zeny budget spent ({_skillZenyLeft}z left of this shop trip's share, needs {zenyCost}z)");
                    continue;
                }
                _skillZenyLeft -= zenyCost;
            }

            _nextSkill = DateTime.UtcNow.AddSeconds(0.25); // global skill throttle: 250ms between any two casts
            var coolSecs = rule.EveryCooldown > 0 ? rule.EveryCooldown : 1.5;
            _skillCooldown[rule.Skill] = DateTime.UtcNow.AddSeconds(coolSecs);

            switch (rule.Target)
            {
                case SkillCastTarget.SelfCast:
                    await _bot.UseSkillSelfAsync(rule.Skill, level, ct);
                    break;
                case SkillCastTarget.Ground:
                    await _bot.UseSkillGroundAsync(rule.Skill, level, target.Pos.X, target.Pos.Y, ct);
                    break;
                default:
                    await _bot.UseSkillOnTargetAsync(rule.Skill, level, target.Id, ct);
                    break;
            }

            OnLog?.Invoke($"Skill: {rule.Skill} L{level} ({rule.Target}) on '{target.Name}'.");
            _lastSkillSkipReason = null;
            return true;
        }

        // Every rule was skipped — log once per unique combination so the user can debug without enabling
        // verbose chat. Deduped to avoid spamming on every tick where the same conditions keep failing.
        if (skipReasons != null && skipReasons.Count > 0)
        {
            var combined = string.Join("; ", skipReasons);
            // Dedupe on which conditions failed, not their live values (SP ticking up would log every tick).
            var key = System.Text.RegularExpressions.Regex.Replace(combined, @", is [^;]*", "");
            if (key != _lastSkillSkipReason)
            {
                _lastSkillSkipReason = key;
                OnLog?.Invoke($"Skills: no rule fired — {combined}.");
            }
        }
        return false;
    }

    // Ally-target rules run every tick when the bot is safe and in a party. We scan visible players, find the
    // most-injured ally for whom the rule's conditions match (lhs target.* refer to that ally), and cast.
    // Server-side validation still applies (it will reject a heal on a non-party player), but gating on
    // InParty avoids spamming pointless casts.
    private async Task<bool> TickAllyRulesAsync(Snapshot snap, CancellationToken ct)
    {
        if (!_bot.InParty) return false;
        var rules = GetSkillRules();
        if (rules.Count == 0) return false;
        if (DateTime.UtcNow < _nextSkill) return false;

        foreach (var rule in rules)
        {
            if (rule.Target != SkillCastTarget.Ally) continue;
            if (_skillCooldown.TryGetValue(rule.Skill, out var nextOk) && DateTime.UtcNow < nextOk) continue;
            if (!KnowsSkill(rule.Skill, out var knownLevel)) continue;

            var ally = FindBestAlly(snap, rule);
            if (ally == null) continue;

            var level = rule.Level > 0 ? Math.Min(rule.Level, knownLevel) : knownLevel;
            if (level <= 0) continue;

            _nextSkill = DateTime.UtcNow.AddSeconds(0.25); // global skill throttle: 250ms between any two casts
            var coolSecs = rule.EveryCooldown > 0 ? rule.EveryCooldown : 1.5;
            _skillCooldown[rule.Skill] = DateTime.UtcNow.AddSeconds(coolSecs);

            await _bot.UseSkillOnTargetAsync(rule.Skill, level, ally.Id, ct);
            OnLog?.Invoke($"Skill: {rule.Skill} L{level} (ally) on '{ally.Name}'.");
            return true;
        }
        return false;
    }

    private MonsterInfo? FindBestAlly(Snapshot snap, SkillRule rule)
    {
        return _bot.WithState(w =>
        {
            MonsterInfo? best = null;
            var bestHpPct = double.MaxValue;
            foreach (var e in w.Entities.Values)
            {
                if (!e.IsPlayer) continue;
                if (e.Id == w.Self.EntityId) continue; // self-target uses 'self', not 'ally'
                var info = new MonsterInfo(e.Id, e.ClassId, e.Name, e.Position, e.Hp, e.MaxHp);
                if (!EvaluateConditions(rule.Conditions, snap, info)) continue;
                var hpPct = e.MaxHp > 0 ? 100.0 * e.Hp / e.MaxHp : 100.0;
                if (hpPct < bestHpPct) { bestHpPct = hpPct; best = info; }
            }
            return best;
        });
    }

    private bool KnowsSkill(CharacterSkill skill, out int level)
    {
        var lv = _bot.WithState(w =>
        {
            var best = 0;
            foreach (var k in w.Self.KnownSkills) if (k.Skill == skill && k.Level > best) best = k.Level;
            foreach (var k in w.Self.GrantedSkills) if (k.Skill == skill && k.Level > best) best = k.Level;
            return best;
        });
        level = lv;
        return lv > 0;
    }

    private bool EvaluateConditions(IReadOnlyList<SkillCond> conds, Snapshot snap, MonsterInfo target) =>
        FailedCondition(conds, snap, target) == null;

    /// <summary>The first condition that doesn't hold, as "needs self.sp >= 8, is 4" for the log; null when all hold.</summary>
    private static string? FailedCondition(IReadOnlyList<SkillCond> conds, Snapshot snap, MonsterInfo target)
    {
        foreach (var c in conds)
        {
            var lhs = LhsValue(c.Lhs, snap, target);
            if (double.IsNaN(lhs)) return $"unknown variable '{c.Lhs}'"; // rule doesn't fire
            if (!Compare(lhs, c.Op, c.Value)) return $"needs {c.Lhs} {c.Op} {c.Value:0.##}, is {lhs:0.##}";
        }
        return null;
    }

    private static double LhsValue(string name, Snapshot snap, MonsterInfo target)
    {
        // Status-effect membership for SELF.
        if (name.StartsWith("self.status.", StringComparison.OrdinalIgnoreCase))
            return HasSnapStatus(snap, name["self.status.".Length..]) ? 1 : 0;
        // Status-effect membership for the rule's TARGET (the chosen enemy or ally). The status data flows
        // in from ApplyStatusEffect packets — already populated on the entity view, snapshotted per-tick.
        // Example: `use HolyLight 0 enemy if target.status.poison == 0` to poison-spam only fresh mobs;
        // or `use Blessing 0 ally if target.status.blessing == 0` so a Priest only re-buffs allies who
        // lost the effect.
        if (name.StartsWith("target.status.", StringComparison.OrdinalIgnoreCase))
            return HasTargetStatus(snap, target.Id, name["target.status.".Length..]) ? 1 : 0;

        switch (name)
        {
            case "target.hp": return target.Hp;
            case "target.maxhp": return target.MaxHp;
            case "target.hppct": return target.MaxHp > 0 ? 100.0 * target.Hp / target.MaxHp : 100.0;
            case "target.dist": return Math.Max(Math.Abs(snap.SelfPos.X - target.Pos.X), Math.Abs(snap.SelfPos.Y - target.Pos.Y));
            case "self.hp": return snap.SelfHp;
            case "self.maxhp": return snap.SelfMaxHp;
            case "self.hppct": return snap.SelfMaxHp > 0 ? 100.0 * snap.SelfHp / snap.SelfMaxHp : 100.0;
            case "self.sp": return snap.SelfSp;
            case "self.maxsp": return snap.SelfMaxSp;
            case "self.sppct": return snap.SelfMaxSp > 0 ? 100.0 * snap.SelfSp / snap.SelfMaxSp : 100.0;
            case "self.level": return snap.SelfLevel;
            case "enemies": return CountAliveMonsters(snap);
            // Convenience flags for the common cases — match by category so a rule doesn't need to spell out
            // every individual status id.
            case "self.poisoned": return HasSnapStatus(snap, "Poison") ? 1 : 0;
            case "self.cursed": return HasSnapStatus(snap, "Curse") ? 1 : 0;
            case "self.silenced": return HasSnapStatus(snap, "Silence") ? 1 : 0;
            case "self.blinded": return HasSnapStatus(snap, "Blind") ? 1 : 0;
            case "self.stunned": return (HasSnapStatus(snap, "Stun") || HasSnapStatus(snap, "Sleep")
                                          || HasSnapStatus(snap, "Frozen") || HasSnapStatus(snap, "Stone")) ? 1 : 0;
            case "self.has_blessing": return HasSnapStatus(snap, "Blessing") ? 1 : 0;
            case "self.has_agiup":    return HasSnapStatus(snap, "IncreaseAgi") ? 1 : 0;
            case "self.has_angelus":  return HasSnapStatus(snap, "Angelus") ? 1 : 0;
            case "self.has_kyrie":    return HasSnapStatus(snap, "KyrieEleison") ? 1 : 0;
            case "self.has_endure":   return HasSnapStatus(snap, "Endure") ? 1 : 0;
            // Target-side convenience flags. Use these so a Priest only re-blesses an ally without
            // Blessing, an Assassin only Envenoms an unpoisoned mob, etc.
            case "target.poisoned":     return HasTargetStatus(snap, target.Id, "Poison") ? 1 : 0;
            case "target.cursed":       return HasTargetStatus(snap, target.Id, "Curse") ? 1 : 0;
            case "target.silenced":     return HasTargetStatus(snap, target.Id, "Silence") ? 1 : 0;
            case "target.stunned":      return (HasTargetStatus(snap, target.Id, "Stun") || HasTargetStatus(snap, target.Id, "Sleep")
                                                || HasTargetStatus(snap, target.Id, "Frozen") || HasTargetStatus(snap, target.Id, "Stone")) ? 1 : 0;
            case "target.has_blessing": return HasTargetStatus(snap, target.Id, "Blessing") ? 1 : 0;
            case "target.has_agiup":    return HasTargetStatus(snap, target.Id, "IncreaseAgi") ? 1 : 0;
            case "target.has_angelus":  return HasTargetStatus(snap, target.Id, "Angelus") ? 1 : 0;
            case "target.has_kyrie":    return HasTargetStatus(snap, target.Id, "KyrieEleison") ? 1 : 0;
            case "target.has_endure":   return HasTargetStatus(snap, target.Id, "Endure") ? 1 : 0;
            default: return double.NaN;
        }
    }

    private static bool HasSnapStatus(Snapshot snap, string statusName)
    {
        if (!Enum.TryParse<CharacterStatusEffect>(statusName, ignoreCase: true, out var s)) return false;
        return snap.SelfStatuses.Contains(s);
    }

    private static bool HasTargetStatus(Snapshot snap, int entityId, string statusName)
    {
        if (!Enum.TryParse<CharacterStatusEffect>(statusName, ignoreCase: true, out var s)) return false;
        return snap.EntityStatuses.TryGetValue(entityId, out var set) && set.Contains(s);
    }

    // Mirrors IsHuntable's alive semantics: when MaxHp is 0 we haven't seen the mob's HP yet, so treat
    // it as alive (the next packet usually fills it in). Only known-dead (MaxHp>0 && Hp<=0) is skipped.
    private static int CountAliveMonsters(Snapshot snap)
    {
        var n = 0;
        foreach (var m in snap.Monsters)
            if (m.MaxHp <= 0 || m.Hp > 0) n++;
        return n;
    }

    private static bool Compare(double lhs, string op, double rhs) => op switch
    {
        "<" => lhs < rhs,
        "<=" => lhs <= rhs,
        ">" => lhs > rhs,
        ">=" => lhs >= rhs,
        "==" => Math.Abs(lhs - rhs) < 1e-9,
        "!=" => Math.Abs(lhs - rhs) >= 1e-9,
        _ => false,
    };

    /// <summary>Parse a skill script. Unparseable lines are skipped with an entry in <paramref name="errors"/>.
    /// Static so the MCP can validate a script before applying it.</summary>
    public static List<SkillRule> ParseSkillScript(string script, List<string>? errors)
    {
        var rules = new List<SkillRule>();
        if (string.IsNullOrWhiteSpace(script)) return rules;

        var lineNo = 0;
        foreach (var raw in script.Split('\n'))
        {
            lineNo++;
            var line = raw;
            var hash = line.IndexOf('#');
            if (hash >= 0) line = line[..hash];
            line = line.Trim();
            if (line.Length == 0) continue;

            if (TryParseRule(line, out var rule, out var err))
                rules.Add(rule);
            else
                errors?.Add($"line {lineNo}: {err}");
        }
        return rules;
    }

    private static bool TryParseRule(string line, out SkillRule rule, out string error)
    {
        rule = null!;
        error = "";
        var toks = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (toks.Length < 3) { error = "expected 'use <Skill> <level> ...'"; return false; }
        if (!string.Equals(toks[0], "use", StringComparison.OrdinalIgnoreCase)) { error = "rule must start with 'use'"; return false; }

        if (!Enum.TryParse<CharacterSkill>(toks[1], true, out var skill))
        {
            if (!int.TryParse(toks[1], out var n) || !Enum.IsDefined(typeof(CharacterSkill), n))
            { error = $"unknown skill '{toks[1]}'"; return false; }
            skill = (CharacterSkill)n;
        }
        if (!int.TryParse(toks[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var level) || level < 0)
        { error = $"bad level '{toks[2]}'"; return false; }

        var target = SkillCastTarget.Enemy;
        double cooldown = 0;
        var i = 3;
        while (i < toks.Length && !string.Equals(toks[i], "if", StringComparison.OrdinalIgnoreCase))
        {
            var t = toks[i].ToLowerInvariant();
            if (t is "self" or "self-target" or "selfcast") { target = SkillCastTarget.SelfCast; i++; continue; }
            if (t is "ground" or "ground-target") { target = SkillCastTarget.Ground; i++; continue; }
            if (t is "enemy" or "target") { target = SkillCastTarget.Enemy; i++; continue; }
            if (t is "ally" or "ally-target") { target = SkillCastTarget.Ally; i++; continue; }
            if (t is "every")
            {
                if (i + 1 >= toks.Length || !double.TryParse(toks[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out cooldown))
                { error = "'every' needs a number of seconds"; return false; }
                i += 2; continue;
            }
            error = $"unexpected token '{toks[i]}'"; return false;
        }

        var conds = new List<SkillCond>();
        if (i < toks.Length) // saw 'if'
        {
            i++; // skip 'if'
            while (i < toks.Length)
            {
                if (i + 2 >= toks.Length) { error = "incomplete condition (expected lhs op value)"; return false; }
                var lhs = toks[i].ToLowerInvariant();
                var op = toks[i + 1];
                if (!double.TryParse(toks[i + 2], NumberStyles.Float, CultureInfo.InvariantCulture, out var val))
                { error = $"condition value '{toks[i + 2]}' isn't a number"; return false; }
                if (op is not ("<" or "<=" or ">" or ">=" or "==" or "!=")) { error = $"bad operator '{op}'"; return false; }
                conds.Add(new SkillCond(lhs, op, val));
                i += 3;
                if (i < toks.Length)
                {
                    if (!string.Equals(toks[i], "and", StringComparison.OrdinalIgnoreCase))
                    { error = $"expected 'and' before more conditions, got '{toks[i]}'"; return false; }
                    i++;
                }
            }
        }

        rule = new SkillRule(skill, level, target, cooldown, conds);
        return true;
    }

    /// <summary>Validate a skill script (without applying it). The MCP/UI uses this to surface parse errors.</summary>
    public static (int RuleCount, IReadOnlyList<string> Errors) ValidateSkillScript(string script)
    {
        var errs = new List<string>();
        var rules = ParseSkillScript(script ?? "", errs);
        return (rules.Count, errs);
    }
}
