using RebuildSharedData.Data;
using RebuildSharedData.Enum;

namespace RoBotClient.Bot.State;

/// <summary>A nearby entity (player / monster / NPC) as last reported by the server.</summary>
public sealed class EntityView
{
    public int Id;
    public CharacterType Type;
    public int ClassId;
    public string Name = "";
    public Position Position;
    public CharacterState State;
    public Direction Facing;
    public byte Level;
    public int Hp;
    public int MaxHp;

    // Walk interpolation (#2): the reconstructed remaining path, seconds-per-cell, and the time the walk
    // started. Lets us estimate where a moving entity actually is between sparse position updates.
    public Position[]? WalkPath;
    public float MoveCellTime;
    public DateTime WalkStartUtc;

    // Last target id observed for this entity (updated from PacketType.Attack server broadcasts). Lets a
    // follower bot mirror its party leader's target without re-deriving it. 0 = no known target.
    public int CurrentTargetId;

    // Active status effects on this entity (debuffs like Poison/Stun AND buffs like Blessing/IncreaseAgi).
    // Keyed by status id; value is the UTC time the effect expires (server sends remaining seconds in the
    // ApplyStatusEffect packet — we add to UtcNow). RemoveStatusEffect removes the key. Used by the bot's
    // skill / item DSL to react ("buff me if no Blessing", "use Green Potion if poisoned") and by MCP
    // get_bot so the agent can see what's affecting the bot.
    public readonly Dictionary<CharacterStatusEffect, DateTime> Statuses = new();

    public bool HasStatus(CharacterStatusEffect s) =>
        Statuses.TryGetValue(s, out var until) && until > DateTime.UtcNow;

    public bool IsMonster => Type == CharacterType.Monster;
    public bool IsNpc => Type == CharacterType.NPC;
    public bool IsPlayer => Type is CharacterType.Player or CharacterType.PlayerLikeNpc;
    public bool IsAlive => MaxHp <= 0 || Hp > 0;

    /// <summary>Best estimate of the current cell: for a moving entity, advance along the walk path by the
    /// elapsed time; otherwise the last authoritative cell. Always clamped to the path, so a stale or
    /// mis-timed estimate can never land off the actual route.</summary>
    public Position EstimatedCell()
    {
        if (State != CharacterState.Moving || WalkPath == null || WalkPath.Length == 0 || MoveCellTime <= 0.001f)
            return Position;
        var step = (int)((DateTime.UtcNow - WalkStartUtc).TotalSeconds / MoveCellTime);
        if (step <= 0) return WalkPath[0];
        if (step >= WalkPath.Length) step = WalkPath.Length - 1;
        return WalkPath[step];
    }
}
