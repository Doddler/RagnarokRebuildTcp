namespace RoBotClient.Bot.Behavior;

/// <summary>One bot's ranking inputs for the squad-leader election. Pulled live from the running session
/// so the rank reflects the bot's CURRENT class + level — a Novice that's just job-changed to Swordsman
/// immediately outranks the rest of the squad on the next election.</summary>
public readonly record struct SquadCandidate(
    string BotId,
    string CharacterName,
    int JobId,
    int Level,
    int MaxHp,
    int Vit,
    bool InGame);

/// <summary>Job priority for squad-leader election. The user's stated order:
/// Swordsman → Thief → Merchant → Archer → Mage → Acolyte → Novice. Lower number wins.
/// Promotions slot into the same primary class bucket (Knight/Crusader rank with Swordsman, Priest with
/// Acolyte, etc.) so a Knight outranks a base Novice but a higher-level Novice still loses to a Swordsman.
/// </summary>
public static class SquadRanking
{
    public static int JobPriority(int jobId) => jobId switch
    {
        1 or 7 or 14 => 0,  // Swordsman, Knight, Crusader
        5 or 11 or 17 => 1, // Thief, Assassin, Rogue
        6 or 10 or 18 => 2, // Merchant, Blacksmith, Alchemist
        2 or 9 or 12 => 3,  // Archer, Hunter, Dancer? -- Dancer is actually Buffer
        3 or 13 or 16 => 4, // Mage, Wizard, Sage
        4 or 8 or 15 => 5,  // Acolyte, Priest, Monk
        0 => 6,             // Novice — leader of last resort
        _ => 7,
    };

    /// <summary>Composite sort key — lower wins (better leader). Job is primary; ties break on base level
    /// (higher), then MaxHp (higher), then Vit (higher). InGame=false candidates rank dead last so we
    /// don't elect a disconnected bot.</summary>
    public static (int NotInGame, int Job, int NegLevel, int NegMaxHp, int NegVit) SortKey(SquadCandidate c) =>
        (c.InGame ? 0 : 1, JobPriority(c.JobId), -c.Level, -c.MaxHp, -c.Vit);

    /// <summary>Pick the squad leader from a non-empty list of candidates. Returns null if the list is
    /// empty.</summary>
    public static SquadCandidate? PickLeader(IReadOnlyList<SquadCandidate> members)
    {
        if (members.Count == 0) return null;
        var best = members[0];
        var bestKey = SortKey(best);
        for (var i = 1; i < members.Count; i++)
        {
            var key = SortKey(members[i]);
            if (Compare(key, bestKey) < 0) { best = members[i]; bestKey = key; }
        }
        return best;
    }

    private static int Compare(
        (int NotInGame, int Job, int NegLevel, int NegMaxHp, int NegVit) a,
        (int NotInGame, int Job, int NegLevel, int NegMaxHp, int NegVit) b)
    {
        var c = a.NotInGame.CompareTo(b.NotInGame); if (c != 0) return c;
        c = a.Job.CompareTo(b.Job);                if (c != 0) return c;
        c = a.NegLevel.CompareTo(b.NegLevel);      if (c != 0) return c;
        c = a.NegMaxHp.CompareTo(b.NegMaxHp);      if (c != 0) return c;
        return a.NegVit.CompareTo(b.NegVit);
    }
}
