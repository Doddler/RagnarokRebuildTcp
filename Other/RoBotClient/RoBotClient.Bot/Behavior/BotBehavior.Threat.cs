using RebuildSharedData.Enum;

namespace RoBotClient.Bot.Behavior;

// Tank threat management. When BotSession sees another player take a hit it fires OnAllyHit; if THIS bot
// is the squad's tank and the hit is on a squadmate, switch target to the assailant so the support bots
// can keep doing their thing. Event-driven — the actual target rewrite happens between ticks; the next
// regular tick lands in the Attack path with the new target id and the existing combat pipeline does the
// rest.
public sealed partial class BotBehavior
{
    private DateTime _lastThreatSwitchAt;
    private int _lastThreatAssailant;

    // Don't whip-saw between assailants: once we've switched, hold the new target for at least this long
    // before another threat event can pull us. Keeps a tank from oscillating between two mobs both
    // beating on different allies.
    private static readonly TimeSpan ThreatSwitchCooldown = TimeSpan.FromSeconds(1.5);

    private void OnAllyHit(int allyEntityId, int attackerEntityId)
    {
        try
        {
            // Threat handling is squad-leader-and-tanks-only. Buffers / Healers / DPS get to keep doing
            // their job; only the tank pivots to body-block.
            var jobId = _bot.WithState(w => w.Self.JobId);
            if (_config.ResolveRole(jobId) != PartyRole.Tank) return;

            // Filter to squadmates so we don't try to defend strangers. Bots in the legacy party-leader
            // fallback (no SquadId set) still defend their leader because party-leader name is captured
            // separately — extend the predicate if you want to include all party members.
            var leader = _config.SquadLeaderName;
            if (string.IsNullOrEmpty(_config.SquadId) || string.IsNullOrEmpty(leader)) return;

            var (allyName, attackerName, attackerClass, attackerHp, alreadyOnAttacker) =
                _bot.WithState(w =>
                {
                    var an = w.Entities.TryGetValue(allyEntityId, out var a) ? a.Name : "(unknown)";
                    string atkName = "(unknown)";
                    int atkClass = -1, atkHp = 0;
                    if (attackerEntityId > 0 && w.Entities.TryGetValue(attackerEntityId, out var atk))
                    {
                        atkName = atk.Name;
                        atkClass = atk.ClassId;
                        atkHp = atk.Hp;
                    }
                    var selfTargeting = w.Entities.TryGetValue(w.Self.EntityId, out var se)
                                        && se.CurrentTargetId == attackerEntityId;
                    return (an, atkName, atkClass, atkHp, selfTargeting);
                });

            if (attackerEntityId <= 0 || attackerClass < 0) return;          // unresolved attacker
            if (alreadyOnAttacker) return;                                   // already on it — nothing to do
            if (_targetId == attackerEntityId) return;                       // race: tick already noticed
            if (DateTime.UtcNow - _lastThreatSwitchAt < ThreatSwitchCooldown
                && _lastThreatAssailant != attackerEntityId) return;         // recent switch — debounce

            // Only switch to a squadmate's attacker — a hit on the bot itself is handled by the normal
            // engagement code, no need for the threat hook to override that.
            if (allyEntityId == _bot.WithState(w => w.Self.EntityId)) return;

            // Don't pull mobs into a fight that the forecast says we'd lose. The single-target Forecast
            // path needs a MonsterInfo; build one from the attacker's known stats and run the same
            // CanWin check the regular target-picker uses. If the fight isn't winnable, let the support
            // peel instead of dragging the tank into a death.
            // EXCEPT: when the operator has explicitly force-hunted this mob class, the forecast veto
            // is overridden everywhere — including threat-switch — so the bot commits to the fight even
            // when the simulator is being cautious.
            var mi = new MonsterInfo(attackerEntityId, attackerClass, attackerName,
                _bot.WithState(w => w.Entities.TryGetValue(attackerEntityId, out var atk) ? atk.Position : default),
                attackerHp, attackerHp);
            if (!_config.ForceHuntClassIds.Contains(attackerClass)
                && !Forecast(_bot.WithState(Snapshot.From), mi).CanWin)
            {
                OnLog?.Invoke($"Threat: '{attackerName}' is hitting '{allyName}' but the forecast says I'd lose — staying on current target.");
                return;
            }

            // Race-safety: TickAsync also rewrites these fields from the main task. Lock the trio so the
            // tick never sees a partial write (e.g. new id + stale name in logs / TargetName property).
            lock (_targetLock)
            {
                _targetId = attackerEntityId;
                _targetClass = attackerClass;
                TargetName = attackerName;
            }
            _lastThreatSwitchAt = DateTime.UtcNow;
            _lastThreatAssailant = attackerEntityId;
            _nextAttack = DateTime.UtcNow.AddSeconds(0.5);
            OnLog?.Invoke($"Threat: switching to '{attackerName}' to defend squadmate '{allyName}'.");
        }
        catch { /* never let the event handler escape — bot.OnAllyHit fires from the receive thread */ }
    }
}
