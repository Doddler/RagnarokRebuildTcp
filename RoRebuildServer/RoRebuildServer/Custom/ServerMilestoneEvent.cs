using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.Data;
using RoRebuildServer.Data.Monster;
using RoRebuildServer.Data.Scripting;
using RoRebuildServer.Data.ServerConfigScript;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Items;
using RoRebuildServer.Logging;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation;

namespace RoRebuildServer.Custom;

internal class PlayerLevelRecord
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Job { get; set; }
    public int Level { get; set; }
}

internal class PlayerFirstToLevelRecord
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public int Level { get; set; }
    public DateTime Time { get; set; }
}

internal class PlayerKillRecord
{
    public string Name { get; set; }
    public string Party { get; set; }
    public string Boss { get; set; }
    public DateTime Time { get; set; }
}

[JsonSerializable(typeof(List<PlayerLevelRecord>))]
internal partial class PlayerLevelRecordContext : JsonSerializerContext
{
}

[JsonSerializable(typeof(List<PlayerFirstToLevelRecord>))]
internal partial class PlayerFirstToLevelRecordContext : JsonSerializerContext
{
}

[JsonSerializable(typeof(List<PlayerKillRecord>))]
internal partial class PlayerKillRecordContext : JsonSerializerContext
{
}

public class ServerMilestoneEvent : ServerConfigScriptHandlerBase
{
    private record MilestoneInfo(string MonsterName, int BaseExp, int JobExp, List<string>? Rewards = null, bool IsMvp = false);

    private readonly Dictionary<string, MilestoneInfo> milestoneTargetList = new()
    {
        //miniboss
        { "VOCAL", new MilestoneInfo("Vocal", 2000, 1000, ["1_Carat_Diamond", "Old_Blue_Box"]) },
        { "ECLIPSE", new MilestoneInfo("Eclipse", 2000, 1000, ["1_Carat_Diamond", "Old_Blue_Box"]) },
        { "MASTERING", new MilestoneInfo("Mastering", 2000, 1000, ["1_Carat_Diamond", "Old_Blue_Box"]) },
        { "TOAD", new MilestoneInfo("Toad", 2000, 1000, ["1_Carat_Diamond", "Old_Blue_Box"]) },
        { "DRAGON_FLY", new MilestoneInfo("Dragon Fly", 3000, 1500, ["1_Carat_Diamond", "Old_Blue_Box"]) },
        { "VAGABOND_WOLF", new MilestoneInfo("Vagabond Wolf", 5000, 2500, ["2_Carat_Diamond", "Old_Blue_Box"]) },
        { "ANGELING", new MilestoneInfo("Angeling", 10000, 5000, ["2_Carat_Diamond", "Old_Blue_Box"]) },
        { "DEVILING", new MilestoneInfo("Deviling", 10000, 5000, ["2_Carat_Diamond", "Old_Purple_Box"]) },
        { "GHOSTRING", new MilestoneInfo("Ghostring", 10000, 5000, ["2_Carat_Diamond", "Old_Blue_Box"]) },
        { "GOBLIN_LEADER", new MilestoneInfo("Goblin Leader", 10000, 5000, ["2_Carat_Diamond", "Old_Blue_Box"]) },
        { "KOBOLD_LEADER", new MilestoneInfo("Kobold Leader", 15000, 7500, ["2_Carat_Diamond", "Old_Purple_Box"]) },
        { "CHEPET", new MilestoneInfo("Chepet", 15000, 7500, ["2_Carat_Diamond", "Old_Blue_Box"]) },
        { "ARCHANGELING", new MilestoneInfo("Archangeling", 20000, 10000, ["2_Carat_Diamond", "Old_Purple_Box"]) },
        //mvp
        { "MAYA", new MilestoneInfo("Maya", 25_000, 12_500, ["3_Carat_Diamond", "Old_Card_Album"], true) },
        { "EDDGA", new MilestoneInfo("Eddga", 25_000, 12_500, ["3_Carat_Diamond", "Old_Card_Album"], true) },
        { "PHREEONI", new MilestoneInfo("Phreeoni", 25_000, 12_500, ["3_Carat_Diamond", "Old_Card_Album"], true) },
        { "GOLDEN_BUG", new MilestoneInfo("Golden Thief Bug", 25_000, 12_500, ["3_Carat_Diamond", "Old_Purple_Box", "Old_Card_Album"], true) },
        { "MOONLIGHT", new MilestoneInfo("Moonlight Flower", 25_000, 12_500, ["3_Carat_Diamond", "Old_Purple_Box", "Old_Card_Album"], true) },
        { "ORK_HERO", new MilestoneInfo("Orc Hero", 30_000, 15_000, ["Gold", "Old_Card_Album"], true) },
        { "ORC_LORD", new MilestoneInfo("Orc Lord", 35_000, 17_500, ["Gold", "Old_Card_Album"], true) },
        { "DOPPELGANGER", new MilestoneInfo("Doppelganger", 35_000, 17_500, ["Gold", "Old_Purple_Box", "Old_Card_Album"], true) },
        { "PHARAOH", new MilestoneInfo("Pharaoh", 35_000, 17_500, ["Gold", "Old_Purple_Box", "Old_Card_Album"], true) },
        { "OSIRIS", new MilestoneInfo("Osiris", 35_000, 17_500, ["Gold", "Old_Card_Album"], true) },
        { "DRAKE", new MilestoneInfo("Drake", 35_000, 17_500, ["Gold", "Old_Card_Album"], true) },
        { "AMON_RA", new MilestoneInfo("AmonRa", 40_000, 20_000, ["Gold", "Old_Purple_Box", "Old_Card_Album"], true) },
        { "KNIGHT_OF_WINDSTORM", new MilestoneInfo("Stormy Knight", 40_000, 20_000, ["Gold", "Old_Card_Album"], true) },
        { "INCANTATION_SAMURAI", new MilestoneInfo("Samurai Spectre", 40_000, 20_000, ["Gold", "Old_Card_Album"], true) },
        { "GARM", new MilestoneInfo("Garm", 40_000, 20_000, ["Gold", "Old_Card_Album"], true) },
        { "TURTLE_GENERAL", new MilestoneInfo("Turtle General", 50_000, 25_000, ["Gold", "Old_Purple_Box", "Old_Card_Album"], true) },
        { "DARK_LORD", new MilestoneInfo("Dark Lord", 60_000, 30_000, ["Treasure_Box", "Old_Purple_Box", "Old_Card_Album"], true) },
        { "BAPHOMET", new MilestoneInfo("Baphomet", 150_000, 75_000, ["Treasure_Box", "Old_Purple_Box", "Old_Card_Album", "Old_Card_Album"], true) },
        { "RANDGRIS", new MilestoneInfo("Valkyrie Randgris", 1_000_000, 500_000, [], true) },
    };

    private static int highestLevelPlayer;
    private bool isKafraVisible;
    private bool isRanchVendorVisible;
    private bool isGeneralVendorVisible;
    private bool isAlchemistVisible;
    private bool isMixerVisible;
    private bool isBlacksmithVisible;
    private bool isFletcherVisible;
    private bool isScholarVisible;
    private bool isHeadgearCraftsmanVisible;
    private bool isHighLevelWeaponsVisible;
    private bool isValkyrieVisible;
    private bool isOkolnirCleared;

    private const int KafraVisibleLevel = 10;
    private const int RanchVendorVisibleLevel = 20;
    private const int GeneralVendorVisibleLevel = 30;
    private const int AlchemistVendorVisibleLevel = 40;
    private const int FletcherVisibleLevel = 50;
    private const int MixerVisibleLevel = 60;
    private const int ScholarVisibleLevel = 80;
    private const int ValkyrieVisibleLevel = 85;
    private const int ValkyrieVisibleMvpReq = 8;
    private const int HeadgearCraftsmanVisibleLevel = 7;
    private const int SmithRevealAchievements = 4;
    private const int HighLevelWeaponsMvpLevel = 2;

    private const string KafraSignalName = "MilestoneKafra";
    private const string RanchVendorSignalName = "MilestoneRanchVendor";
    private const string GeneralVendorSignalName = "MilestoneGeneralVendor";
    private const string AlchemistSignalName = "MilestoneAlchemist";
    private const string MixerSignalName = "MilestoneMixer";
    private const string SmithSignalName = "MilestoneSmith";
    private const string FletcherSignalName = "MilestoneFletcher";
    private const string ScholarSignalName = "MilestoneScholar";
    private const string HeadgearCrafterSignalName = "MilestoneHeadgear";
    private const string HighLevelWeaponsSignalName = "MilestoneHighLevelWeapons";
    private const string ValkyrieSignalName = "MilestoneValkyrie";
    private const string OkolnirClearSignal = "OkolnirFirstClear";

    private const string GlobalTop10Json = "Top10ListJson";
    private const string GlobalTop10Text = "Top10List";

    private const string FirstToLevelJson = "LevelMilestonesJson";
    private const string FirstToLevelText = "LevelMilestones";

    private const string BossMilestonesJson = "BossMilestonesJson";

    private readonly Dictionary<Guid, PlayerLevelRecord> levelRecord = new();
    private List<PlayerLevelRecord> topTenLevels = new();
    private readonly Stack<PlayerLevelRecord> unusedLevelRecords = new();

    private List<PlayerFirstToLevelRecord> firstLevelRecords = new();
    private readonly Dictionary<int, PlayerFirstToLevelRecord> firstLevelAchievements = new();

    private List<PlayerKillRecord> playerKillRecords = new();
    private readonly Dictionary<string, PlayerKillRecord> playerKillLookup = new();

    private int minibossMilestones;
    private int mvpMilestones;

    private static HashSet<int> firstAchievementLevels = [10, 20, 30, 40, 50, 60, 70, 80, 90, 95, 99];

    private Object rankListLock = new();

    public override void PostServerStartEvent()
    {
        if (!ServerConfig.OperationConfig.ActiveEvents?.Contains("MilestoneEvent") ?? false)
            return;

        ServerLogger.Log("MilestoneEvent is currently enabled!");

        Monster.OnMonsterDieEvent = OnKillMonster;
        Player.OnLevelUpEvent = OnLevelUp;

        var topTenRecord = ScriptGlobalManager.StringValue(GlobalTop10Json);
        if (!string.IsNullOrWhiteSpace(topTenRecord))
        {
            var top10 = JsonSerializer.Deserialize<List<PlayerLevelRecord>>(topTenRecord, PlayerLevelRecordContext.Default.ListPlayerLevelRecord);
            if (top10 != null)
            {
                topTenLevels = top10;
                foreach (var entry in top10)
                    levelRecord.Add(entry.Id, entry);
            }
        }

        var firstToLevelRecord = ScriptGlobalManager.StringValue(FirstToLevelJson);
        if (!string.IsNullOrWhiteSpace(firstToLevelRecord))
        {
            var records = JsonSerializer.Deserialize<List<PlayerFirstToLevelRecord>>(firstToLevelRecord, PlayerFirstToLevelRecordContext.Default.ListPlayerFirstToLevelRecord);
            if (records != null)
            {
                firstLevelRecords = records;
                foreach (var record in records)
                    firstLevelAchievements.Add(record.Level, record);
            }
        }

        var killRecords = ScriptGlobalManager.StringValue(BossMilestonesJson);
        mvpMilestones = 0;
        minibossMilestones = 0;
        if (!string.IsNullOrWhiteSpace(killRecords))
        {
            var records = JsonSerializer.Deserialize<List<PlayerKillRecord>>(killRecords, PlayerKillRecordContext.Default.ListPlayerKillRecord);
            if (records != null)
            {
                playerKillRecords = records;
                foreach (var record in records)
                    playerKillLookup.Add(record.Boss, record);
                foreach (var (code, info) in milestoneTargetList)
                {
                    if (playerKillLookup.ContainsKey(code))
                    {
                        if (info.IsMvp)
                            mvpMilestones++;
                        else
                            minibossMilestones++;
                    }
                }
            }
        }

        isOkolnirCleared = ScriptGlobalManager.IntValue(OkolnirClearSignal) > 0;

        Top10ListToText();
        RefreshFirstToLevelString();
        UpdateTargetKillString(false);
        UpdateTargetKillString(true);

        highestLevelPlayer = ScriptGlobalManager.IntValue("HighestLevelPlayer");
        CheckAndRevealForLevelRank(true);
        CheckAndRevealMinibossNpcs(true);
        CheckAndRevealMvpNpcs(true);
    }

    private void CheckAndRevealMinibossNpcs(bool isServerUp)
    {
        if (!isBlacksmithVisible && minibossMilestones >= SmithRevealAchievements)
            isBlacksmithVisible = RevealNpc(SmithSignalName, !isServerUp);

        if (!isHeadgearCraftsmanVisible && minibossMilestones >= HeadgearCraftsmanVisibleLevel)
            isHeadgearCraftsmanVisible = RevealNpc(HeadgearCrafterSignalName, !isServerUp);
    }

    private void CheckAndRevealMvpNpcs(bool isServerUp)
    {
        if (!isHighLevelWeaponsVisible && mvpMilestones >= HighLevelWeaponsMvpLevel)
            isHighLevelWeaponsVisible = RevealNpc(HighLevelWeaponsSignalName, !isServerUp);

        if (!isValkyrieVisible && mvpMilestones >= ValkyrieVisibleMvpReq && highestLevelPlayer >= ValkyrieVisibleLevel)
            isValkyrieVisible = RevealNpc(ValkyrieSignalName, !isServerUp);
    }

    private void CheckAndRevealForLevelRank(bool isServerUp)
    {
        if (!isKafraVisible && highestLevelPlayer >= KafraVisibleLevel)
            isKafraVisible = RevealNpc(KafraSignalName, !isServerUp);

        if (!isRanchVendorVisible && highestLevelPlayer >= RanchVendorVisibleLevel)
            isRanchVendorVisible = RevealNpc(RanchVendorSignalName, !isServerUp);

        if (!isGeneralVendorVisible && highestLevelPlayer >= GeneralVendorVisibleLevel)
            isGeneralVendorVisible = RevealNpc(GeneralVendorSignalName, !isServerUp);

        if (!isAlchemistVisible && highestLevelPlayer >= AlchemistVendorVisibleLevel)
            isAlchemistVisible = RevealNpc(AlchemistSignalName, !isServerUp);

        if (!isMixerVisible && highestLevelPlayer >= MixerVisibleLevel)
            isMixerVisible = RevealNpc(MixerSignalName, !isServerUp);

        if (!isFletcherVisible && highestLevelPlayer >= FletcherVisibleLevel)
            isFletcherVisible = RevealNpc(FletcherSignalName, !isServerUp);

        if (!isScholarVisible && highestLevelPlayer >= ScholarVisibleLevel)
            isScholarVisible = RevealNpc(ScholarSignalName, !isServerUp);

        if (!isValkyrieVisible && mvpMilestones >= ValkyrieVisibleMvpReq && highestLevelPlayer >= ValkyrieVisibleLevel)
            isValkyrieVisible = RevealNpc(ValkyrieSignalName, !isServerUp);
    }

    private bool RevealNpc(string signalName, bool doMessage)
    {
        World.Instance.MainThreadActions.Push(() =>
        {
            if (World.Instance.GetGlobalSignalTarget(signalName).TryGet<Npc>(out var npc))
            {
                if (!npc.Character.AdminHidden)
                    return;

                if (doMessage)
                    npc.OnSignal(npc.Character, "Reveal");
                else
                    npc.OnSignal(npc.Character, "RevealOnServerUp");
            }
            else
                ServerLogger.LogWarning($"ServerMilestone handler could not reveal npc for signal {signalName}, no global signal handler exists.");
        });
        return true;
    }

    private void AddPlayerToTop10(Player p, int level)
    {
        var jobName = p.JobId switch
        {
            0 => "Novice",
            1 => "Swordsman",
            2 => "Archer",
            3 => "Mage",
            4 => "Acolyte",
            5 => "Thief",
            6 => "Merchant",
            _ => "Unknown Job"
        };


        if (levelRecord.TryGetValue(p.Id, out var existing))
        {
            existing.Level = level;
            existing.Job = jobName; //they might have changed jobs
            topTenLevels.Sort((a, b) => b.Level.CompareTo(a.Level));
        }
        else
        {

            if (!unusedLevelRecords.TryPop(out var rec))
                rec = new PlayerLevelRecord() { Id = p.Id, Level = level, Name = p.Name, Job = jobName };
            else
            {
                rec.Id = p.Id;
                rec.Name = p.Name;
                rec.Level = level;
                rec.Job = jobName;
            }

            topTenLevels.Add(rec);
            levelRecord.Add(p.Id, rec);
            topTenLevels.Sort((a, b) => b.Level.CompareTo(a.Level));
        }

        if (topTenLevels.Count > 10)
        {
            var last = topTenLevels[^1];
            topTenLevels.RemoveAt(topTenLevels.Count - 1);
            levelRecord.Remove(last.Id);
            unusedLevelRecords.Push(last);
        }

        var serialized = JsonSerializer.Serialize(topTenLevels, PlayerLevelRecordContext.Default.ListPlayerLevelRecord);
        ScriptGlobalManager.SetString(GlobalTop10Json, serialized);

        Top10ListToText();
    }

    private void Top10ListToText()
    {
        var sb = new StringBuilder();
        for (var i = 0; i < 10; i++)
        {
            if (i < topTenLevels.Count)
            {
                var rec = topTenLevels[i];
                if (string.IsNullOrWhiteSpace(rec.Job))
                    sb.AppendLine($"#{i + 1} : Level {rec.Level} - {rec.Name}");
                else
                    sb.AppendLine($"#{i + 1} : Level {rec.Level} - {rec.Name} ({rec.Job})");
            }
            else
                sb.AppendLine($"#{i + 1} : --");
        }
        ScriptGlobalManager.SetString(GlobalTop10Text, sb.ToString());
    }

    public void OnLevelUp(Player player)
    {
        var level = player.GetData(PlayerStat.Level);

        var addToTop10 = topTenLevels.Count < 10 || level > topTenLevels[^1].Level;
        var isAchievementLevel = firstAchievementLevels.Contains(level) && !firstLevelAchievements.ContainsKey(level);
        var isHighestPlayer = level > highestLevelPlayer;

        if (!addToTop10 && !isAchievementLevel && !isHighestPlayer)
            return;

        lock (rankListLock)
        {
            if (addToTop10)
                AddPlayerToTop10(player, level);

            if (isAchievementLevel)
                SetPlayerAsFirstToLevel(player, level);

            if (level < highestLevelPlayer)
                return;

            highestLevelPlayer = level;
            ScriptGlobalManager.SetInt("HighestLevelPlayer", highestLevelPlayer);

            CheckAndRevealForLevelRank(false);
        }
    }

    private void SetPlayerAsFirstToLevel(Player player, int level)
    {
        ServerWideMessage($"<color=#99CCFF>Player '{player.Name}' was the first to reach level {level}!</color>");
        var req = new PlayerFirstToLevelRecord() { Id = player.Id, Level = level, Name = player.Name, Time = DateTime.Now };
        firstLevelAchievements.Add(level, req);
        firstLevelRecords.Add(req);
        firstLevelRecords.Sort((a, b) => a.Level.CompareTo(b.Level));
        RefreshFirstToLevelString();

        var serialized = JsonSerializer.Serialize(firstLevelRecords, PlayerFirstToLevelRecordContext.Default.ListPlayerFirstToLevelRecord);
        ScriptGlobalManager.SetString(FirstToLevelJson, serialized);
    }

    private void RefreshFirstToLevelString()
    {
        var sb = new StringBuilder();
        foreach (var level in firstAchievementLevels)
        {
            if (firstLevelAchievements.TryGetValue(level, out var req))
                sb.AppendLine($"Level {level}: {req.Name} ({req.Time:MMMM dd} at {req.Time:hh:ss tt})");
            else
                sb.AppendLine($"Level {level}: --");
        }

        ScriptGlobalManager.SetString(FirstToLevelText, sb.ToString());
    }

    public void OnKillMonster(Monster monster)
    {
        try
        {
            HandleMonsterKillEvent(monster);
        }
        catch (Exception e)
        {
            ServerLogger.LogWarning($"Failed to handle OnKillMonster event!\n{e}");
        }
    }

    private void UpdateTargetKillString(bool isMvp)
    {
        var sb = new StringBuilder();

        foreach (var (target, info) in milestoneTargetList)
        {
            if (info.IsMvp != isMvp)
                continue;

            if (!playerKillLookup.TryGetValue(target, out var kill))
            {
                var monster = DataManager.MonsterCodeLookup[target];
                var name = DataManager.MonsterCodeLookup[target].Name;
                if (monster.Code == "RANDGRIS")
                    name = "??????";
                sb.AppendLine($"{name}: <i>Not yet killed</i>");
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(kill.Party))
                    sb.AppendLine($"{DataManager.MonsterCodeLookup[target].Name}: {kill.Name} of party [{kill.Party}] ({kill.Time:MMMM dd} at {kill.Time:hh:ss tt})");
                else
                    sb.AppendLine($"{DataManager.MonsterCodeLookup[target].Name}: {kill.Name} ({kill.Time:MMMM dd} at {kill.Time:hh:ss tt})");
            }
        }

        if (isMvp)
            ScriptGlobalManager.SetString("MvpMilestones", sb.ToString());
        else
            ScriptGlobalManager.SetString("MinibossMilestones", sb.ToString());
    }

    private void HandleMonsterKillEvent(Monster monster)
    {
        if (playerKillLookup.ContainsKey(monster.MonsterBase.Code))
            return;

        if (!milestoneTargetList.TryGetValue(monster.MonsterBase.Code, out var milestone))
            return;

        var killer = monster.GetTopContributor();
        if (killer == null || killer.Type != CharacterType.Player)
            return;

        var player = killer.Player;
        var rec = new PlayerKillRecord() { Name = player.Name, Boss = monster.MonsterBase.Code, Time = DateTime.Now, Party = "" };

        string msg;
        string storedMessage;
        if (monster.MonsterBase.Code != "RANDGRIS")
        {
            if (player.Party != null)
            {
                rec.Party = player.Party.PartyName;
                if (!milestone.IsMvp)
                    msg = $"<color=#99CCFF>Player '{player.Name}' of party [{player.Party.PartyName}] was the first to kill {milestone.MonsterName}!</color>";
                else
                    msg = $"<color=#99CCFF>Player '{player.Name}' of party [{player.Party.PartyName}] was the first to kill the MVP boss {milestone.MonsterName}!</color>";
            }
            else
            {
                if (!milestone.IsMvp)
                    msg = $"<color=#99CCFF>Player '{player.Name}' was the first to kill {milestone.MonsterName}!</color>";
                else
                    msg = $"<color=#99CCFF>Player '{player.Name}' was the first to kill the MVP boss {milestone.MonsterName}!</color>";
            }

            msg += $"\n<color=#99CCFF><i>As a reward for achieving a server first, all online players will be rewarded {milestone.BaseExp:N0} exp (up to one full level)!</i></color>";
        }
        else
        {
            if (player.Party != null)
            {
                rec.Party = player.Party.PartyName;
                msg = $"<color=#99CCFF>The Okolnir challenge dungeon has been completed! '{player.Name}' of party [{player.Party.PartyName}] was the first to kill the MVP boss Valkyrie Randgris!</color>";
            }
            else
                msg = $"<color=#99CCFF>The Okolnir challenge dungeon has been completed! '{player.Name}' was the first to kill the MVP boss {milestone.MonsterName}!</color>";
            msg += $"\n<color=#99CCFF><i>As a reward for achieving a server first, all online players will be rewarded {milestone.BaseExp:N0} exp (up to one full level)!</i></color>";

            ScriptGlobalManager.SetInt(OkolnirClearSignal, 1);
            RevealNpc(OkolnirClearSignal, false);

        }

        ServerWideMessage(msg);

        World.Instance.MainThreadActions.Push(() => GiveAllPlayersExp(milestone.BaseExp, milestone.JobExp));

        if (milestone.Rewards != null && milestone.Rewards.Count > 0)
        {
            foreach (var reward in milestone.Rewards)
            {
                if (DataManager.ItemIdByName.TryGetValue(reward, out var item))
                {
                    var itemData = DataManager.ItemList[item];
                    player.CreateItemInInventory(new ItemReference(item, 1));
                }
                else
                    ServerLogger.LogWarning($"Failed to create achievement item {reward}!");
            }

            CommandBuilder.AddRecipient(player.Connection);
            if (milestone.Rewards.Count == 1)
                CommandBuilder.SendServerMessage($"<color=#66FFAA>You have received an item for a server first achievement.</color>", "");
            else
                CommandBuilder.SendServerMessage($"<color=#66FFAA>You have received items for a server first achievement.</color>", "");
            CommandBuilder.ClearRecipients();
        }

        playerKillLookup.Add(rec.Boss, rec);
        playerKillRecords.Add(rec);

        if (milestone.IsMvp)
            mvpMilestones++;
        else
            minibossMilestones++;

        UpdateTargetKillString(milestone.IsMvp);

        var serialized = JsonSerializer.Serialize(playerKillRecords, PlayerKillRecordContext.Default.ListPlayerKillRecord);
        ScriptGlobalManager.SetString(BossMilestonesJson, serialized);

        CheckAndRevealMinibossNpcs(false);
        CheckAndRevealMvpNpcs(false);
    }

    private void ServerWideMessage(string msg)
    {
        CommandBuilder.AddAllPlayersAsRecipients();
        CommandBuilder.SendServerMessage(msg, "Server", true);
        CommandBuilder.ClearRecipients();
    }

    private void GiveAllPlayersExp(int baseExp, int jobExp)
    {
        foreach (var (id, connection) in NetworkManager.ConnectedAccounts)
        {
            if (!connection.IsConnectedAndInGame)
                continue;

            var player = connection.Player;
            if (player == null)
                continue;

            player.GainBaseExp(baseExp);
            player.GainJobExp(jobExp);
        }
    }

    public override void OnSetMonsterSpawnTime(MonsterDatabaseInfo monster, string mapName, ref int minTime, ref int maxTime)
    {
        if (DataManager.MvpMonsterCodes.Contains(monster.Code))
        {
            minTime = 14 * 60 * 1000;
            maxTime = 15 * 60 * 1000;

        }
    }
}

