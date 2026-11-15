using RebuildSharedData.Enum;
using RoBotClient.Bot.State;

namespace RoBotClient.Bot.Behavior;

// #20: auto job change at the prt_fild08 "Adventuring Bard". Pure NPC-dialog flow (no special packet):
// main menu -> "Job change" -> job-name menu -> confirm "I'm sure". Eligibility is enforced by the NPC
// (a Novice needs job level 10 with all skill points spent; an existing job can re-change). We navigate
// with the same warp-graph + A* travel as shopping, then drive the dialog by matching option text.
public sealed partial class BotBehavior
{
    private const string BardMap = "prt_fild08";
    private const int BardX = 153, BardY = 357;

    private enum JobPhase { None, Travel, Approach, Converse }

    private JobPhase _jobPhase = JobPhase.None;
    private bool _jobChangeSent;
    private int _lastJobChangeTarget = -1; // guards against re-looping if JobId is slow to refresh
    private int _jobSteps;
    private DateTime _jobDeadline;
    private DateTime _jobNextAction;
    private DateTime _jobCooldownUntil = DateTime.MinValue;
    private bool _warnedDesiredJobMismatch;

    private bool JobChangeActive => _jobPhase != JobPhase.None;

    private bool WantsJobChange(Snapshot snap)
    {
        var target = _config.DesiredJobId;
        if (target < 1 || target > 6) return false;
        if (target == snap.SelfJobId) return false;
        if (target == _lastJobChangeTarget) return false; // already changed to it this session
        if (snap.SelfJobId != 0)
        {
            // The Adventuring Bard handles Novice → 1st-job only. We can't satisfy DesiredJobId on a bot
            // that's already past job 0 (e.g. reconnected to an existing Swordsman with DesiredJobId=2). Log
            // once so the misconfiguration is visible, then stop trying.
            if (!_warnedDesiredJobMismatch)
            {
                _warnedDesiredJobMismatch = true;
                OnLog?.Invoke($"DesiredJob={target} ignored — bot is already job {snap.SelfJobId}; the Bard only handles Novice → 1st-job.");
            }
            return false;
        }
        return DateTime.UtcNow >= _jobCooldownUntil;
    }

    // Spend one leftover skill point (the Bard refuses a Novice with unspent points). Returns false only
    // when there is genuinely nothing left to spend on (so the caller can back off instead of spinning).
    private async Task<bool> SpendOneSkillPointAsync(Snapshot snap, CancellationToken ct)
    {
        if (DateTime.UtcNow < _jobNextAction) return true; // throttled — still working
        var skill = _bot.WithState(FirstUnmaxedLearnable);
        if (skill == null) return false;
        _jobNextAction = DateTime.UtcNow.AddMilliseconds(600);
        OnLog?.Invoke($"Spending a leftover skill point on {_data?.SkillInfo(skill.Value)?.Name ?? skill.Value.ToString()} before changing job.");
        await _bot.ApplySkillPointAsync(skill.Value, ct);
        return true;
    }

    private CharacterSkill? FirstUnmaxedLearnable(WorldState w)
    {
        if (_data == null) return null;
        var learned = new Dictionary<CharacterSkill, int>();
        foreach (var k in w.Self.KnownSkills) learned[k.Skill] = k.Level;
        foreach (var entry in _data.LearnableSkills(w.Self.JobId))
        {
            var max = _data.SkillInfo(entry.Skill)?.MaxLevel ?? 0;
            if (max > 0 && learned.GetValueOrDefault(entry.Skill, 0) < max) return entry.Skill;
        }
        return null;
    }

    private void StartJobChange(Snapshot snap)
    {
        _jobPhase = JobPhase.Travel;
        _jobChangeSent = false;
        _jobSteps = 0;
        _jobDeadline = DateTime.UtcNow.AddSeconds(150);
        _jobNextAction = DateTime.MinValue;
        OnLog?.Invoke($"Job change: heading to the Adventuring Bard to become a {JobName(_config.DesiredJobId)}.");
    }

    private async Task TickJobChangeAsync(Snapshot snap, bool stuck, CancellationToken ct)
    {
        if (DateTime.UtcNow > _jobDeadline) { await AbortJobChangeAsync("timed out", ct); return; }
        Mode = BotMode.JobChange;

        switch (_jobPhase)
        {
            case JobPhase.Travel:
                if (string.Equals(snap.Map, BardMap, StringComparison.OrdinalIgnoreCase))
                {
                    _jobPhase = JobPhase.Approach;
                    _jobNextAction = DateTime.MinValue;
                    return;
                }
                if (stuck) { ResetStuck(snap.SelfPos); await NudgeAsync(snap, ct); return; }
                if (DateTime.UtcNow < _jobNextAction) return;
                _jobNextAction = DateTime.UtcNow.AddSeconds(1.3);
                await StepTowardMapAsync(snap, BardMap, ct);
                return;

            case JobPhase.Approach:
                var npcId = _bot.WithState(w => FindNpcNear(w, BardX, BardY));
                if (npcId != 0)
                {
                    _jobChangeSent = false;
                    _jobSteps = 0;
                    _jobPhase = JobPhase.Converse;
                    _jobNextAction = DateTime.UtcNow.AddMilliseconds(800);
                    await _bot.NpcClickAsync(npcId, ct);
                    return;
                }
                if (stuck) { ResetStuck(snap.SelfPos); await NudgeAsync(snap, ct); return; }
                if (DateTime.UtcNow < _jobNextAction) return;
                _jobNextAction = DateTime.UtcNow.AddSeconds(1.5);
                await WalkPathTowardAsync(snap, BardX, BardY, 16, ct);
                return;

            case JobPhase.Converse:
                await TickJobConverseAsync(snap, ct);
                return;
        }
    }

    private async Task TickJobConverseAsync(Snapshot snap, CancellationToken ct)
    {
        if (DateTime.UtcNow < _jobNextAction) return;
        if (++_jobSteps > 40) { await AbortJobChangeAsync("conversation stalled", ct); return; }

        var npc = _bot.SnapshotNpc();
        switch (npc.Phase)
        {
            case NpcPhase.None:
                _jobNextAction = DateTime.UtcNow.AddMilliseconds(600);
                return;
            case NpcPhase.Dialog:
                _jobNextAction = DateTime.UtcNow.AddMilliseconds(450);
                await _bot.NpcAdvanceAsync(ct);
                return;
            case NpcPhase.Option:
                var idx = ChooseJobOption(npc.Options);
                if (idx < 0) { await AbortJobChangeAsync("unexpected dialog menu", ct); return; }
                _jobNextAction = DateTime.UtcNow.AddMilliseconds(450);
                await _bot.NpcSelectOptionAsync(idx, ct);
                return;
            case NpcPhase.Ended:
                if (_jobChangeSent)
                {
                    _lastJobChangeTarget = _config.DesiredJobId;
                    OnLog?.Invoke($"Job change to {JobName(_config.DesiredJobId)} complete.");
                }
                EndJobChange();
                return;
            default:
                _jobNextAction = DateTime.UtcNow.AddMilliseconds(450);
                await _bot.NpcAdvanceAsync(ct); // unexpected (shop/refine) — try to advance out
                return;
        }
    }

    // Drive each Bard menu by option text rather than fragile index assumptions.
    private int ChooseJobOption(IReadOnlyList<string> options)
    {
        int Find(params string[] keys)
        {
            for (var i = 0; i < options.Count; i++)
                foreach (var k in keys)
                    if (options[i].Equals(k, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        var sure = Find("I'm sure");
        if (sure >= 0) { _jobChangeSent = true; return sure; } // confirmation menu

        var jobChange = Find("Job change");
        if (jobChange >= 0) // main menu
            return _jobChangeSent ? FindOr(Find("Cancel"), options.Count - 1) : jobChange;

        var ji = Find(JobName(_config.DesiredJobId));
        if (ji >= 0) return ji; // job-selection menu

        var bail = Find("Cancel", "No thanks", "I changed my mind"); // unknown menu — back out
        return bail;
    }

    private static int FindOr(int found, int fallback) => found >= 0 ? found : fallback;

    private int FindNpcNear(WorldState w, int x, int y)
    {
        var best = 0;
        var bestD = int.MaxValue;
        foreach (var e in w.Entities.Values)
        {
            if (!e.IsNpc) continue;
            var d = Math.Max(Math.Abs(e.Position.X - x), Math.Abs(e.Position.Y - y));
            if (d <= 3 && d < bestD) { bestD = d; best = e.Id; }
        }
        return best;
    }

    private async Task AbortJobChangeAsync(string reason, CancellationToken ct)
    {
        OnLog?.Invoke($"Job change aborted: {reason}.");
        await _bot.NpcAdvanceAsync(ct); // nudge any open dialog toward closing
        EndJobChange();
    }

    private void EndJobChange()
    {
        _jobPhase = JobPhase.None;
        _jobCooldownUntil = DateTime.UtcNow.AddSeconds(30);
    }

    private static string JobName(int jobId) => jobId switch
    {
        1 => "Swordsman", 2 => "Archer", 3 => "Mage", 4 => "Acolyte", 5 => "Thief", 6 => "Merchant", _ => "",
    };
}
