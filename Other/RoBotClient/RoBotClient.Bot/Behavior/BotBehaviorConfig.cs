namespace RoBotClient.Bot.Behavior;

/// <summary>Archetype tag for party play. Auto resolves from JobId at runtime (Acolyte/Priest=Healer,
/// Swordsman/Knight/Crusader=Tank, Bard/Dancer=Buffer, Novice/Merchant=Utility, everything else=Dps). The
/// FSM uses the resolved role to prioritize behaviors: Healer prefers healing visible party members over
/// engaging enemies; Tank intercepts threats hitting party-mates; Buffer rotates buffs on the party.</summary>
public enum PartyRole { Auto, Tank, Dps, Healer, Buffer, Utility }

/// <summary>Tunables that decide what a bot hunts and when it flees.</summary>
public sealed class BotBehaviorConfig
{
    /// <summary>Monster ClassIds to hunt. Empty = hunt any monster (subject to the filters below).</summary>
    public HashSet<int> HuntClassIds = new();

    /// <summary>Monster ClassIds to flee from when one is nearby.</summary>
    public HashSet<int> FleeClassIds = new();

    /// <summary>Monster ClassIds to never engage (e.g. an indestructible training dummy).</summary>
    public HashSet<int> IgnoreClassIds = new();

    /// <summary>Skip targets whose MaxHp exceeds this (filters out bosses/dummies). 0 = no cap.</summary>
    public int MaxTargetHp = 5000;

    /// <summary>Only engage if projected damage-taken-to-kill is below this fraction of current HP.</summary>
    public double WinMargin = 0.6;

    /// <summary>Hard cap on the number of SWINGS the bot needs to land to kill a target. Secondary
    /// gate alongside the time-based <see cref="WinMargin"/> check (which is the primary filter).
    /// For an AGI build attacking 2-3x per second this number gets eaten fast against tanky mobs
    /// — raise it if simulate_fight is reporting <c>canWin=false</c> with a reasonable kill time but
    /// high <c>hitsToKill</c>. The time-based check still rejects fights that take too long.</summary>
    public int MaxRoundsToKill = 120;

    /// <summary>Flee when current HP fraction drops below this.</summary>
    public float FleeHpPercent = 0.30f;

    /// <summary>A feared monster within this many tiles triggers a flee.</summary>
    public int FleeMonsterRange = 5;

    /// <summary>If set, the bot travels here (via the warp graph) whenever it's on another map, and
    /// avoids wandering into portals so it stays on the hunting field.</summary>
    public string HomeMap = "";

    public bool Enabled = true;

    /// <summary>If the bot makes no progress (no movement AND no target-HP drop) for this many seconds,
    /// it treats the current target/route as unreachable, abandons it, and relocates.</summary>
    public double StuckSeconds = 3.5;

    /// <summary>When true, the bot announces FSM state changes via in-game chat (public Say).</summary>
    public bool Verbose;

    // ---- auto-loot ----

    /// <summary>Pick up nearby ground items between kills.</summary>
    public bool AutoLoot = true;

    /// <summary>Item ids never picked up off the ground (left for others / junk we don't want).</summary>
    public HashSet<int> LootBlacklist = new();

    /// <summary>Only loot drops within this many tiles, so the bot doesn't run across the map for junk.</summary>
    public int LootRange = 12;

    /// <summary>Minimum seconds between pickup requests.</summary>
    public double LootCooldownSeconds = 0.8;

    // ---- auto-consumables ----

    /// <summary>Use a healing item when HP drops below this fraction. 0 disables auto-pot.</summary>
    public float HealHpPercent = 0.5f;

    /// <summary>Healing item ids to consume, cheapest/weakest first. Default: Red/Orange/Yellow/White Potion.
    /// Every id is validated against the item DB (must be Useable) before sending — sending an unknown id
    /// disconnects the player server-side.</summary>
    public List<int> HealingItemIds = new() { 501, 502, 503, 504 };

    /// <summary>Minimum seconds between potion uses.</summary>
    public double HealCooldownSeconds = 1.0;

    // ---- shopping (auto restock + auto-sell by rarity) ----

    /// <summary>Travel to a town to sell junk and restock potions when overweight or low. Off by default
    /// until the route to a shop is known-good; flip on per-bot once verified.</summary>
    public bool AutoShop;

    /// <summary>Item ids never auto-sold, on top of the always-kept set (equipped, usable, cards, ammo,
    /// carded/refined/slotted gear, zero sell-price, and the configured healing items).</summary>
    public HashSet<int> KeepItemIds = new();

    /// <summary>Go sell when carried weight reaches this fraction of capacity.</summary>
    public float SellWeightPercent = 0.9f;

    /// <summary>Also go sell when holding at least this many distinct item stacks.</summary>
    public int SellItemStacks = 30;

    /// <summary>Also go sell when holding at least this many SALEABLE items (per <see cref="ShopPolicy"/>).
    /// Catches the case where the bot's inventory fills with junk weapon / armor drops well before either
    /// weight or stack-count triggers fire. 0 disables this trigger.</summary>
    public int JunkSellCount = 8;

    /// <summary>Healing item to restock at a tool dealer (Red Potion by default). 0 disables restock.</summary>
    public int RestockItemId = 501;

    /// <summary>Restock up to this many when the held count falls below <see cref="RestockBelow"/>.</summary>
    public int RestockTargetCount = 50;
    public int RestockBelow = 10;

    /// <summary>Town map hosting the shop. Empty = auto-pick a town selling <see cref="RestockItemId"/>.</summary>
    public string ShopMap = "";

    // ---- job change ----

    /// <summary>Desired first job (JobType: 1 Swordsman, 2 Archer, 3 Mage, 4 Acolyte, 5 Thief, 6 Merchant).
    /// 0 = no auto job change. When set and reachable, the bot visits the prt_fild08 Adventuring Bard once
    /// eligible (Novice at job level 10 with skill points spent) and changes to it.</summary>
    public int DesiredJobId;

    // ---- rest / sit to recover ----

    /// <summary>When low on HP or SP and out of usable healing items, sit to regen instead of fighting.
    /// A Novice (job 0) can only sit with Basic Mastery level 2+, so the bot won't try otherwise.</summary>
    public bool RestWhenNoPotions = false;

    /// <summary>Sit to rest when HP% or SP% falls below this (and no healing item is held). Stands back up
    /// once both are nearly full again, a threat appears, or a healing item becomes available.</summary>
    public float RestBelowPercent = 0.5f;

    // ---- skills ----

    /// <summary>Per-bot skill rule script (see SKILL_SCRIPT.md). One rule per line, evaluated top-to-bottom
    /// each combat tick: the first rule whose conditions match and whose skill is known + off cooldown fires.
    /// Empty = no skills auto-cast.</summary>
    public string SkillScript = "";

    /// <summary>Per-bot item-usage rule script (see ITEM_SCRIPT.md). Same shape as <see cref="SkillScript"/>
    /// but for consumables: `use &lt;itemId&gt; if hppct &lt; 50 every 1` etc. Evaluated each tick; first matching
    /// rule with the item in inventory + off cooldown fires. When set, replaces the hard-coded HealHpPercent
    /// path so the agent can express bespoke logic (e.g. Fly Wing on stuck, Butterfly Wing as last resort,
    /// SP potion when low, etc.). Empty = fall back to HealHpPercent + AutoEscapeWhenStuck defaults.</summary>
    public string ItemScript = "";

    // ---- stuck-escape items ----

    /// <summary>When stuck for too long, auto-use Fly Wing (random teleport on same map) or, as a last
    /// resort, Butterfly Wing (return to save point). Off by default — flip on per-bot when the inventory
    /// is stocked.</summary>
    public bool AutoEscapeWhenStuck = false;

    /// <summary>Item ids for the two escape consumables. Override here if a server uses non-standard ids.</summary>
    public int FlyWingItemId = 601;
    public int ButterflyWingItemId = 602;

    /// <summary>Stuck duration (seconds) before reaching for a Fly Wing.</summary>
    public double FlyWingStuckSeconds = 8.0;

    /// <summary>Minimum tile distance to keep between the bot's chosen target/loot cell and the nearest
    /// portal. Prevents accidental warps while engaging a mob standing on a portal apron. 0 disables the
    /// safety buffer entirely. The wanderer uses its own buffer (4) since wander pathing is more likely
    /// to drag the bot onto a portal cell.</summary>
    public int PortalSafeDistance = 3;

    /// <summary>Stuck duration (seconds) before falling back to a Butterfly Wing (used when no Fly Wing is
    /// available, or the bot has been stuck through several Fly Wing attempts).</summary>
    public double ButterflyWingStuckSeconds = 20.0;

    // ---- party / squad ----

    /// <summary>Follower-mode lost-leader grace window (seconds). After this long with neither EntityView
    /// nor a minimap entry for the leader, the follower stops idling and falls back to normal hunting/
    /// wandering — so a same-map-but-far or cross-map separation doesn't lock the bot to Idle.</summary>
    public double FollowerLostFallthroughSeconds = 5.0;

    // -- Squad assignment (replaces the legacy "follow the party leader" path).
    // A party is just the server-side roster (for invite / loot / exp share). Behavior is driven by
    // "squads" — sub-groups inside (or outside) a party that share a single follow-target. A 6-bot party
    // can host two 3-bot squads with different leaders, or all six can be one squad, or each bot can be a
    // solo squad-of-one. Assign squads via MCP `assign_squad` / `auto_form_squad`; leaders are picked by
    // rank (Swordsman > Thief > Merchant > Archer > Mage > Acolyte > Novice; tiebreaks: base level, max HP,
    // VIT). The legacy party-leader path is preserved as a fallback only when SquadId is empty.

    /// <summary>Squad identifier. Empty = unsquadded (legacy party-leader fallback). Bots with the same
    /// SquadId form one squad; the one with <see cref="IsSquadLeader"/> set runs solo behavior, the rest
    /// follow.</summary>
    public string SquadId = "";

    /// <summary>True if this bot is the squad's leader. Exactly one bot in a squad should carry this flag;
    /// the auto-form algorithm sets it on the highest-ranked member.</summary>
    public bool IsSquadLeader = false;

    /// <summary>Character name of this squad's leader. Cached for the follower FSM so it doesn't have to
    /// cross-look-up every tick. Updated together with <see cref="SquadId"/>; cleared on solo.</summary>
    public string SquadLeaderName = "";

    /// <summary>This follower's slot index within the squad (0 = leader, 1..N-1 = followers). Drives the
    /// follow-cell offset table so members don't all stack on the leader's exact cell — slot 1 hangs left
    /// rear, slot 2 right rear, etc. Set by the auto-former at clustering time; manually-assigned squads
    /// can leave this at 0 to fall back to "walk to leader" behavior.</summary>
    public int SquadSlot = 0;

    /// <summary>When this bot is a squad leader, announce target picks in chat as <c>!engage &lt;classId&gt;</c>
    /// so followers running the chat-command listener can react before the server's CurrentTargetId
    /// broadcast propagates. Default OFF — the agent flips it on per-leader via configure_bot when it
    /// wants chat-driven coordination. Throttled to one announce per ~2s + dedup on target id.</summary>
    public bool AnnounceTargetInChat = false;

    /// <summary>When in a squad, listen for chat commands from the squad leader (and the party leader as
    /// fallback). Default ON — commands are no-op unless someone sends them, and the filter rejects chat
    /// from non-leaders, so a follower with this enabled does no harm. Set to false to make a follower
    /// strictly ignore chat (useful for testing or for solo bots that share a map with squads).</summary>
    public bool ListenToPartyChat = true;

    /// <summary>This bot's party archetype. Auto = derive from JobId at runtime; otherwise an explicit
    /// override. Drives target-priority and skill-priority within the party FSM.</summary>
    public PartyRole PartyRole = PartyRole.Auto;

    // ---- combat / kite tuning ----

    /// <summary>When true, the bot does NOT issue basic auto-attacks — it relies entirely on its
    /// skill-script DSL to deal damage. Useful for pure casters (Mage / Wizard) where the staff auto
    /// is wasted DPS, and for skill-only solo builds. Skills + heals + walking still work normally.</summary>
    public bool NoAutoAttack = false;

    /// <summary>Per-bot allowlist of monster class ids the bot will hunt EVEN IF the battle forecast
    /// says <c>CanWin = false</c>. Failsafe so the operator can override a too-cautious WinMargin —
    /// e.g. force a level-50 bot to keep hunting Mandragoras when the simulator hasn't realized they're
    /// immobile. Empty = no overrides; every target obeys the forecast veto.</summary>
    public HashSet<int> ForceHuntClassIds = new();

    /// <summary>Enable the ranged-kite sub-FSM (per KITING_DESIGN.md). The bot tries to stay at
    /// (myRange - <see cref="KiteBufferTiles"/>) tiles from its target while attacking, retreating when
    /// the mob closes the gap. Auto-off for melee jobs and stationary mobs even when this is on.</summary>
    public bool EnableKite = false;

    /// <summary>Sweet-spot buffer: kite target distance = my attack range - this many tiles. Default 1
    /// gives a one-step cushion before the bot gets hit; raise to 2 for fast/dangerous mobs.</summary>
    public int KiteBufferTiles = 1;

    /// <summary>Mob MoveSpeed (tiles/sec) above which the kite FSM treats the mob as "fast" and widens
    /// the buffer by 1 tile. Default 12 — slower than that the bot has a comfortable cadence.</summary>
    public float KiteFastMobThreshold = 12f;

    /// <summary>How long to stay in Suspended mode after the dance-bug detector trips (the gap is
    /// closing despite kiting). The bot anchors and trades hits instead of futile stepping. Resumes
    /// kiting after the timer expires. Default 5s.</summary>
    public double KiteSuspendSeconds = 5.0;

    /// <summary>Resolve the effective role for this bot given its current JobId. Lets the FSM stay
    /// JobId-aware without forcing the user to update their config after each job change.</summary>
    public PartyRole ResolveRole(int jobId)
    {
        if (PartyRole != PartyRole.Auto) return PartyRole;
        return jobId switch
        {
            1 or 7 or 14 => PartyRole.Tank,    // Swordsman, Knight, Crusader
            4 or 8       => PartyRole.Healer,  // Acolyte, Priest
            19 or 20     => PartyRole.Buffer,  // Bard, Dancer
            0            => PartyRole.Dps,     // Novice — punches stuff while it grows up
            // Merchant is a DPS in practice (Mammonite hits hard, Pushcart adds nothing in combat). Use
            // Utility only when the agent explicitly overrides — Auto-derived merchants should fight.
            _            => PartyRole.Dps,
        };
    }

    /// <summary>Overwrite every tunable on THIS config from <paramref name="other"/>, in-place. The bot's
    /// behavior holds a reference to its config object, so the live runner sees the change without restart.
    /// Collections are deep-copied so caller mutations can't leak back into us.</summary>
    public void CopyFrom(BotBehaviorConfig other)
    {
        HuntClassIds = new HashSet<int>(other.HuntClassIds);
        FleeClassIds = new HashSet<int>(other.FleeClassIds);
        IgnoreClassIds = new HashSet<int>(other.IgnoreClassIds);
        KeepItemIds = new HashSet<int>(other.KeepItemIds);
        LootBlacklist = new HashSet<int>(other.LootBlacklist);
        HealingItemIds = new List<int>(other.HealingItemIds);

        MaxTargetHp = other.MaxTargetHp;
        WinMargin = other.WinMargin;
        MaxRoundsToKill = other.MaxRoundsToKill;
        FleeHpPercent = other.FleeHpPercent;
        FleeMonsterRange = other.FleeMonsterRange;
        HomeMap = other.HomeMap;
        Enabled = other.Enabled;
        StuckSeconds = other.StuckSeconds;
        Verbose = other.Verbose;
        AutoLoot = other.AutoLoot;
        LootRange = other.LootRange;
        LootCooldownSeconds = other.LootCooldownSeconds;
        HealHpPercent = other.HealHpPercent;
        HealCooldownSeconds = other.HealCooldownSeconds;
        AutoShop = other.AutoShop;
        SellWeightPercent = other.SellWeightPercent;
        SellItemStacks = other.SellItemStacks;
        JunkSellCount = other.JunkSellCount;
        RestockItemId = other.RestockItemId;
        RestockTargetCount = other.RestockTargetCount;
        RestockBelow = other.RestockBelow;
        ShopMap = other.ShopMap;
        DesiredJobId = other.DesiredJobId;
        RestWhenNoPotions = other.RestWhenNoPotions;
        RestBelowPercent = other.RestBelowPercent;
        SkillScript = other.SkillScript;
        ItemScript = other.ItemScript;
        AutoEscapeWhenStuck = other.AutoEscapeWhenStuck;
        FlyWingItemId = other.FlyWingItemId;
        PortalSafeDistance = other.PortalSafeDistance;
        ButterflyWingItemId = other.ButterflyWingItemId;
        FlyWingStuckSeconds = other.FlyWingStuckSeconds;
        ButterflyWingStuckSeconds = other.ButterflyWingStuckSeconds;
        FollowerLostFallthroughSeconds = other.FollowerLostFallthroughSeconds;
        PartyRole = other.PartyRole;
        SquadId = other.SquadId;
        IsSquadLeader = other.IsSquadLeader;
        SquadLeaderName = other.SquadLeaderName;
        SquadSlot = other.SquadSlot;
        AnnounceTargetInChat = other.AnnounceTargetInChat;
        ListenToPartyChat = other.ListenToPartyChat;
        NoAutoAttack = other.NoAutoAttack;
        ForceHuntClassIds = new HashSet<int>(other.ForceHuntClassIds);
        EnableKite = other.EnableKite;
        KiteBufferTiles = other.KiteBufferTiles;
        KiteFastMobThreshold = other.KiteFastMobThreshold;
        KiteSuspendSeconds = other.KiteSuspendSeconds;
    }
}
