using System;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.Data;
using RoRebuildServer.Data.Monster;
using RoRebuildServer.Data.Player;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Items;
using RoRebuildServer.EntityComponents.Npcs;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.EntitySystem;
using RoRebuildServer.Logging;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation;
using RoRebuildServer.Simulation.Skills;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.EntityComponents;

[EntityComponent(EntityType.Npc)]
public class Npc : IEntityAutoReset
{
    public Entity Entity;
    public Entity Owner;
    public WorldObject Character = null!;
    public string FullName { get; set; } = null!;
    public string Name { get; set; } = null!; //making this a property makes it accessible via npc scripting
    public string EventType = null!;

    public NpcDisplayType DisplayType { get; set; }
    public NpcEffectType EffectType { get; set; }

    public AreaOfEffect? AreaOfEffect;

    public EntityList? Mobs;
    public int MobCount => Mobs?.Count ?? 0;

    private int[]? valuesInt;
    private string[]? valuesString;

    public int[]? ParamsInt;
    public string? ParamString;

    public record WarpDestinationLink(string TargetMap, Position Destination, bool IsNpcLink);

    public List<WarpDestinationLink>? DestinationLinks;
    public List<(int id, int price)>? ItemsForSale;
    public Dictionary<int, int>? SaleItemIndexes;

    public NpcBehaviorBase Behavior = null!;

    public double TimerUpdateRate;
    public double LastTimerUpdate;
    public double TimerStart;

    public bool HasTouch;
    public bool HasInteract;
    public bool TimerActive;
    public bool IsEvent;
    public bool IsPathActive;
    public bool ExpireEventWithoutOwner;
    public NpcPathHandler? NpcPathHandler;

    private string? currentSignalTarget;

    //private SkillCastInfo? skillInfo;

    public bool IsHidden() => !Entity.Get<WorldObject>().AdminHidden;
    public Position SelfPosition => Character.Position;

    public int[] ValuesInt => valuesInt ??= ArrayPool<int>.Shared.Rent(NpcInteractionState.StorageCount);
    public string[] ValuesString => valuesString ??= ArrayPool<string>.Shared.Rent(NpcInteractionState.StorageCount);
    public bool IsOwnerAlive => Owner.IsAlive() && Owner.TryGet<WorldObject>(out var obj) && obj.State != CharacterState.Dead;
    public int EventsCount => Character.Events?.Count ?? 0;

    public void Update()
    {
        if (TimerActive && TimerStart + LastTimerUpdate + TimerUpdateRate < Time.ElapsedTime)
        {
            UpdateTimer();
            if (!Character.IsActive)
                return;
        }

        if (IsEvent && ExpireEventWithoutOwner && !Owner.IsAlive())
        {
            EndEvent();
            return;
        }

        if (IsPathActive && NpcPathHandler != null)
            NpcPathHandler.UpdatePath();

        if (Mobs != null && Mobs.Count > 0)
        {
            var count = Mobs.Count;
            Mobs.ClearInactive();
            if (count != Mobs.Count)
            {
                OnMobKill();
            }
        }
    }

    public void Reset()
    {
        if (AreaOfEffect != null)
        {
            //ServerLogger.LogWarning($"We are resetting the area of effect state of NPC '{Name}', but it shouldn't have one at this point.");
            AreaOfEffect.Reset();
            World.Instance.ReturnAreaOfEffect(AreaOfEffect);
            AreaOfEffect = null!;
        }

        if (valuesInt != null)
            ArrayPool<int>.Shared.Return(valuesInt);
        if (valuesString != null)
            ArrayPool<string>.Shared.Return(valuesString, true);

        valuesInt = null!;
        valuesString = null!;
        Entity = Entity.Null;
        Behavior = null!;
        ParamsInt = null;
        ParamString = null;
        Character = null!;
        currentSignalTarget = null;
        Name = "";
        EventType = null!;
        ItemsForSale = null; //no point in pooling these, we don't place vendor npcs during runtime usually
        SaleItemIndexes = null;
        if (NpcPathHandler != null)
        {
            NpcPathHandler.Npc = null!;
            NpcPathHandler = null;
        }

        DestinationLinks = null;

        if (Mobs != null)
            EntityListPool.Return(Mobs);
        Mobs = null;

        DisplayType = NpcDisplayType.Sprite;
        EffectType = NpcEffectType.None;
        ExpireEventWithoutOwner = false;
    }

    public bool TryGetAreaOfEffect([NotNullWhen(returnValue: true)] out AreaOfEffect? aoe)
    {
        if (AreaOfEffect == null)
        {
            aoe = null;
            return false;
        }

        aoe = AreaOfEffect;
        return true;
    }

    public void RemoveAreaOfEffect()
    {
        if (AreaOfEffect != null)
        {
            Debug.Assert(Character.Map != null);
            Character.Map.RemoveAreaOfEffect(AreaOfEffect);
            AreaOfEffect.Reset();
            World.Instance.ReturnAreaOfEffect(AreaOfEffect);
            AreaOfEffect = null;
        }
    }

    public void OnMobKill()
    {
        Behavior.OnMobKill(this);
    }

    public void UpdateTimer()
    {
        if (!Entity.IsAlive())
        {
            TimerActive = false;
            return;
        }

        var lastTime = LastTimerUpdate;
        var newTime = (float)(Time.ElapsedTime - TimerStart);

        //update this before OnTimer since OnTimer may call ResetTimer or otherwise change the timer
        LastTimerUpdate = newTime;

        Behavior.OnTimer(this, (float)lastTime, newTime); //fix this cast at some point...
    }

    public void ResetTimer()
    {
        TimerStart = Time.ElapsedTime;
        LastTimerUpdate = 0;
    }

    public void StartTimer(int updateRate = 200)
    {
        TimerActive = true;
        TimerUpdateRate = updateRate / 1000f;
        ResetTimer();
    }

    public void SetTimerRate(int updateRate) => TimerUpdateRate = updateRate / 1000f;

    public void StopTimer() => EndTimer();

    public void EndTimer()
    {
        TimerActive = false;
    }

    public void CancelInteraction(Player player)
    {
        if (!player.IsInNpcInteraction)
            return;

        if (player.NpcInteractionState.InteractionResult == NpcInteractionResult.WaitForStorage)
            player.WriteCharacterStorageToDatabase();

        Behavior.OnCancel(this, player, player.NpcInteractionState);

        player.IsInNpcInteraction = false;
        player.NpcInteractionState.Reset();

        CommandBuilder.SendNpcEndInteraction(player);
    }

    public void OnTouch(Player player)
    {
        if (player.IsInNpcInteraction)
        {
            Behavior.OnCancel(player.NpcInteractionState.NpcEntity.Get<Npc>(), player, player.NpcInteractionState);
            player.NpcInteractionState.Reset();
        }

        player.IsInNpcInteraction = true;
        player.NpcInteractionState.IsTouchEvent = true;
        player.NpcInteractionState.BeginInteraction(ref Entity, player);

        var res = Behavior.OnTouch(this, player, player.NpcInteractionState);
        player.NpcInteractionState.InteractionResult = res;

        if (res == NpcInteractionResult.EndInteraction)
        {
            player.IsInNpcInteraction = false;
            player.NpcInteractionState.Reset();
        }
    }

    public void OnInteract(Player player)
    {
        player.IsInNpcInteraction = true;
        player.NpcInteractionState.IsTouchEvent = false;
        player.NpcInteractionState.BeginInteraction(ref Entity, player);

        var res = Behavior.OnClick(this, player, player.NpcInteractionState);
        player.NpcInteractionState.InteractionResult = res;

        if (res == NpcInteractionResult.EndInteraction)
        {
            player.IsInNpcInteraction = false;
            player.NpcInteractionState.Reset();
            CommandBuilder.SendNpcEndInteraction(player);
        }
    }

    public void StartPath()
    {
        NpcPathHandler ??= new NpcPathHandler(this);
        NpcPathHandler.Step = 0;
        IsPathActive = true;
    }

    public void PausePath()
    {
        Debug.Assert(NpcPathHandler != null);
        IsPathActive = false;
    }

    public void ResumePath()
    {
        Debug.Assert(NpcPathHandler != null);
        IsPathActive = true;
    }

    public void EndPath()
    {
        IsPathActive = false;
        if (NpcPathHandler != null)
            NpcPathHandler.Step = 0;
    }

    public void Advance(Player player)
    {
        player.NpcInteractionState.InteractionResult = NpcInteractionResult.None;
        NpcInteractionResult res;

        if (player.NpcInteractionState.IsTouchEvent)
            res = Behavior.OnTouch(this, player, player.NpcInteractionState);
        else
            res = Behavior.OnClick(this, player, player.NpcInteractionState);

        player.NpcInteractionState.InteractionResult = res;

        if (res == NpcInteractionResult.EndInteraction)
        {
            player.IsInNpcInteraction = false;
            player.NpcInteractionState.Reset();
            CommandBuilder.SendNpcEndInteraction(player);
        }
    }
    public void OptionAdvance(Player player, int result)
    {
        player.NpcInteractionState.InteractionResult = NpcInteractionResult.None;
        player.NpcInteractionState.OptionResult = result;
        NpcInteractionResult res;

        if (player.NpcInteractionState.IsTouchEvent)
            res = Behavior.OnTouch(this, player, player.NpcInteractionState);
        else
            res = Behavior.OnClick(this, player, player.NpcInteractionState);

        player.NpcInteractionState.InteractionResult = res;

        if (res == NpcInteractionResult.EndInteraction)
        {
            player.IsInNpcInteraction = false;
            player.NpcInteractionState.Reset();
            CommandBuilder.SendNpcEndInteraction(player);
        }
    }

    public void RegisterSignal(string signal)
    {
        if (Character.Map == null)
            throw new Exception($"Could not perform function AssignSignalTarget on npc {FullName}, it is not currently assigned to a map!");

        RemoveSignal();

        Character.Map?.Instance.NpcNameLookup.TryAdd(signal, Entity);
        currentSignalTarget = signal;
    }

    public void RemoveSignal()
    {
        if (Character.Map == null)
        {
            if (!string.IsNullOrWhiteSpace(currentSignalTarget))
                throw new Exception($"Npc '{FullName}' is attempting to remove the signal '{currentSignalTarget}' while not assigned to a map! The signal pointer is probably dangling.");

            return;
        }

        if (!string.IsNullOrWhiteSpace(currentSignalTarget) && Character.Map.Instance.NpcNameLookup.ContainsKey(currentSignalTarget))
            Character.Map?.Instance.NpcNameLookup.Remove(currentSignalTarget);
    }

    public void OnSignal(Npc srcNpc, string signal, int value1 = 0, int value2 = 0, int value3 = 0, int value4 = 0)
    {
        Behavior.OnSignal(this, srcNpc, signal, value1, value2, value3, value4);
    }

    public void RevealAvatar(string name)
    {

    }

    public void RegisterAsWarpNpc()
    {
        Character.IsImportant = true;
        Character.DisplayType = CharacterDisplayType.WarpNpc;
        Character.Map?.RegisterImportantEntity(Character);
    }

    public void RegisterAsKafra()
    {
        Character.IsImportant = true;
        Character.DisplayType = CharacterDisplayType.Kafra;
        Character.Map?.RegisterImportantEntity(Character);
    }

    public void SetPlayerAppearance(int level, string job, string gender, int head, int hair, string top, string mid, string bottom)
    {
        if (!DataManager.JobIdLookup.TryGetValue(job, out var jobId))
        {
            ServerLogger.LogWarning($"Npc {Name} unable to take appearance of job {job}, that classId could not be found in JobIdTable.");
            return;
        }

        var costume = Character.OverrideAppearanceState;
        if (costume == null)
        {
            costume = new PlayerLikeAppearanceState();
            Character.OverrideAppearanceState = costume;
        }

        var topId = -1;
        var midId = -1;
        var bottomId = -1;


        bool isMale = !(!string.IsNullOrWhiteSpace(gender) && gender.Length > 0 && (gender[0] == 'f' || gender[0] == 'F'));
        Character.ClassId = jobId;
        costume.Level = level;
        costume.HeadType = head;
        costume.HairColor = hair;
        costume.HeadFacing = HeadFacing.Center;
        costume.WeaponClass = 0;

        if (!string.IsNullOrWhiteSpace(top) && !DataManager.ItemIdByName.TryGetValue(top, out topId))
            ServerLogger.LogWarning($"Npc {Name} could not set the headgear to '{top}' as it could not be found.");
        if (!string.IsNullOrWhiteSpace(mid) && !DataManager.ItemIdByName.TryGetValue(mid, out midId))
            ServerLogger.LogWarning($"Npc {Name} could not set the headgear to '{mid}' as it could not be found.");
        if (!string.IsNullOrWhiteSpace(bottom) && !DataManager.ItemIdByName.TryGetValue(bottom, out bottomId))
            ServerLogger.LogWarning($"Npc {Name} could not set the headgear to '{bottom}' as it could not be found.");
        costume.HeadTop = topId;
        costume.HeadMid = midId;
        costume.HeadBottom = bottomId;
    }

    public void AttachCart()
    {
        if (Character.OverrideAppearanceState == null)
        {
            ServerLogger.LogWarning($"Attempting to AttachCart on npc {Character.Name} but it isn't set to use a player appearance.");
            return;
        }

        Character.OverrideAppearanceState.HasCart = true;
    }

    public void SetSittingState(bool isSitting)
    {

        if (!Character.StateCanSit || Character.Map == null)
            return;

        if (isSitting)
            Character.State = CharacterState.Sitting;
        else
            Character.State = CharacterState.Idle;

        Character.Map.AddVisiblePlayersAsPacketRecipients(Character);
        CommandBuilder.ChangeSittingMulti(Character);
        CommandBuilder.ClearRecipients();
    }


    private void EnsureMobListCreated()
    {
        if (Mobs == null)
            Mobs = EntityListPool.Get();
        else
            Mobs.ClearInactive();
    }

    public void SummonMobWithType(string name, string type, int x = -1, int y = -1, int width = 0, int height = 0)
    {
        if (x < 0 || y < 0)
        {
            x = Character.Position.X;
            y = Character.Position.Y;
        }

        if (Character.Map == null)
            return;

        var monster = DataManager.MonsterCodeLookup[name];

        var area = Area.CreateAroundPoint(new Position(x, y), width, height);

        EnsureMobListCreated();
        var mob = CreateSingleMonsterWithAutoOwnership(Character.Map, monster, area, MonsterAiType.AiEmpty);

        var m = mob.Get<Monster>();
        m.ChangeAiSkillHandler(type);
    }

    public void SummonMobs(int count, string name, int x, int y, int width = 0, int height = 0)
    {
        var chara = Entity.Get<WorldObject>();

        if (chara.Map == null)
            return;

        var monster = DataManager.MonsterCodeLookup[name];

        var area = Area.CreateAroundPoint(new Position(x, y), width, height);

        EnsureMobListCreated();

        for (int i = 0; i < count; i++)
            CreateSingleMonsterWithAutoOwnership(chara.Map, monster, area);
    }

    public void SummonMobsNearby(int count, string name, int width = 0, int height = 0, int offsetX = 0, int offsetY = 0)
    {
        var chara = Entity.Get<WorldObject>();

        Debug.Assert(chara.Map != null, $"Npc {Name} cannot summon mobs {name} nearby, it is not currently attached to a map.");

        var monster = DataManager.MonsterCodeLookup[name];

        var area = Area.CreateAroundPoint(chara.Position + new Position(offsetX, offsetY), width, height);

        EnsureMobListCreated();

        for (int i = 0; i < count; i++)
            CreateSingleMonsterWithAutoOwnership(chara.Map, monster, area);
    }

    private Entity CreateSingleMonsterWithAutoOwnership(Map map, MonsterDatabaseInfo monster, Area spawnArea, MonsterAiType aiType = MonsterAiType.AiMinion)
    {
        var m = World.Instance.CreateMonster(map, monster, spawnArea, null);

        if (IsEvent && Owner.TryGet<WorldObject>(out var owner) && owner.Type == CharacterType.Monster)
            owner.Monster.AddChild(ref m, aiType);
        else
        {
            EnsureMobListCreated();
            Mobs!.Add(m);
        }

        return m;
    }

    public void TossSummonMonster(int count, string name, int width = 0, int height = 0, int offsetX = 0, int offsetY = 0)
    {
        Debug.Assert(Character.Map != null, $"Npc {Character.Name} cannot summon mobs {name} nearby, it is not currently attached to a map.");

        var monsterDef = DataManager.MonsterCodeLookup[name];

        var area = Area.CreateAroundPoint(Character.Position + new Position(offsetX, offsetY), width, height);

        for (var i = 0; i < count; i++)
        {
            var minion = World.Instance.CreateMonster(Character.Map, monsterDef, area, null, false);
            var minionMonster = minion.Get<Monster>();
            minionMonster.ResetAiUpdateTime();
            if (IsOwnerAlive)
                minionMonster.SetMaster(Owner); //these monsters have masters but are not minions of the parent

            Character.Map.AddEntityWithEvent(ref minion, CreateEntityEventType.Toss, Character.Position);
        }
    }

    public bool CheckMonstersOfTypeInRange(string name, int x, int y, int distance)
    {
        var chara = Entity.Get<WorldObject>();
        Debug.Assert(chara.Map != null, $"Npc {Name} cannot check monsters of type {name} nearby, it is not currently attached to a map.");
        return chara.Map.HasMonsterOfTypeInRange(new Position(x, y), distance, DataManager.MonsterCodeLookup[name]);
    }

    public void EndAllEvents()
    {
        var chara = Entity.Get<WorldObject>();
        var npc = chara.Npc;


        if (chara.Events == null)
            return;

        chara.Events.ClearInactive();

        for (var i = 0; i < chara.Events.Count; i++)
            chara.Events[i].Get<Npc>().EndEvent();

        chara.Events.Clear();
        //OnMobKill();
    }

    //finish npc shop purchase. We don't use the array sizes directly because they're borrowed and might be larger than expected
    public void SubmitPlayerPurchaseFromNpc(Player player, int[] itemIds, int[] itemCounts, int numItems, bool allowDiscount)
    {
        if (ItemsForSale == null || SaleItemIndexes == null || SaleItemIndexes.Count <= 0)
            return;

        var totalCost = 0;
        var totalWeight = 0;
        var addItemCount = 0;
        var inventory = player.Inventory;

        var dcLevel = player.MaxLearnedLevelOfSkill(CharacterSkill.Discount);
        var discount = allowDiscount && dcLevel > 0 ? 5 + dcLevel * 2 : 0;
        if (discount > 24)
            discount = 24;

        for (var i = 0; i < numItems; i++)
        {
            var id = itemIds[i];
            var count = itemCounts[i];
            if (count <= 0)
                goto Error;
            if (!SaleItemIndexes.TryGetValue(id, out var saleItemId))
            {
                ServerLogger.LogWarning($"Player {player} is attempting to buy item id {id} from {Character.Name}, but that item is not for sale.");
                goto Error; //lol goto
            }
            if (inventory == null || !inventory.HasItem(id))
                addItemCount++;
            var info = DataManager.GetItemInfoById(id);
            if (info == null)
            {
                ServerLogger.LogWarning($"Player {player} is attempting to buy item id {id} from {Character.Name}, but that item is not a valid item.");
                goto Error;
            }

            var saleEntry = ItemsForSale[saleItemId];

            var dcValue = saleEntry.Item2 * discount / 100;
            totalCost += (saleEntry.Item2 - dcValue) * count;
            totalWeight += info.Weight * count;
        }

        var zeny = player.GetData(PlayerStat.Zeny);
        if (totalCost > zeny)
        {
            CommandBuilder.ErrorMessage(player, $"Could not complete purchase, you don't have enough zeny.");
            return;
        }

        var existingWeight = inventory != null ? inventory.BagWeight : 0;
        var existingCount = inventory != null ? inventory.UsedSlots : 0;

        if (existingWeight + totalWeight > player.GetStat(CharacterStat.WeightCapacity))
        {
            CommandBuilder.ErrorMessage(player, $"Could not complete purchase, you can't carry that much weight.");
            return;
        }

        if (existingCount + addItemCount > CharacterBag.MaxBagSlots)
        {
            CommandBuilder.ErrorMessage(player, $"Could not complete purchase, you don't have enough free space.");
            return;
        }

        //we got this far, lets go
        player.SetData(PlayerStat.Zeny, zeny - totalCost); //do this first
        for (var i = 0; i < numItems; i++)
        {
            var item = new ItemReference(itemIds[i], itemCounts[i]);
            player.CreateItemInInventory(item);
        }

        CommandBuilder.SendServerEvent(player, ServerEvent.TradeSuccess, -totalCost);

        player.WriteCharacterToDatabase(); //save immediately, no shenanigans
        CommandBuilder.SendUpdatePlayerData(player, true, false);

        return;
    Error:
        CommandBuilder.ErrorMessage(player, $"Could not complete purchase.");
    }

    public void SubmitPlayerSellItemsToNpc(Player player, int[] itemIds, int[] itemCounts, int numItems)
    {
        var inventory = player.Inventory;
        if (inventory == null)
            throw new Exception($"Player {player} is attempting to sell items when it has no inventory!");
        if (numItems > inventory.UsedSlots)
            return;
        var ocLevel = player.MaxLearnedLevelOfSkill(CharacterSkill.Overcharge);
        var overCharge = ocLevel > 0 ? 5 + player.MaxLearnedLevelOfSkill(CharacterSkill.Overcharge) * 2 : 0;
        if (overCharge > 24)
            overCharge = 24;

        var zenyGained = 0;
        for (var i = 0; i < numItems; i++)
        {
            if (!inventory.GetItem(itemIds[i], out var item))
            {
                CommandBuilder.ErrorMessage(player, $"Could not complete sale, one or more items that you have tried to sell are not available.");
                return;
            }
            if (item.Count < itemCounts[i])
            {
                CommandBuilder.ErrorMessage(player, $"Could not complete sale, the items in your request do not match the number you have in your inventory.");
                return;
            }

            if (player.Equipment.IsItemEquipped(itemIds[i]))
            {
                CommandBuilder.ErrorMessage(player, $"Could not complete sale, you are unable to sell items you currently have equipped.");
                return;
            }

            var info = DataManager.GetItemInfoById(item.Id)!;
            var value = info.SellToStoreValue * (100 + overCharge) / 100;
            if (info.ItemClass != ItemClass.Ammo) //ammo always sells to the NPC for 0, allows NPCs to sell quivers and the like without worry of exploit.
                zenyGained += value * itemCounts[i];
        }

        //all good, lets get to discarding
        for (var i = 0; i < numItems; i++)
        {
            inventory.RemoveItemByBagId(itemIds[i], itemCounts[i]);
        }

        ServerLogger.Log($"Player {player} sold {numItems} types of items to the NPC {Character.Name} for a total of {zenyGained}z.");
        CommandBuilder.SendServerEvent(player, ServerEvent.TradeSuccess, zenyGained);

        var curZeny = player.GetData(PlayerStat.Zeny);
        player.SetData(PlayerStat.Zeny, zenyGained + curZeny);

        player.WriteCharacterToDatabase(); //save immediately, no shenanigans
        CommandBuilder.SendUpdatePlayerData(player, true, false);
    }

    public virtual void SellItem(string itemName)
    {
        ItemsForSale ??= new List<(int, int)>();
        SaleItemIndexes ??= new Dictionary<int, int>();
        if (DataManager.ItemIdByName.TryGetValue(itemName, out var id))
        {
            if (DataManager.ItemList.TryGetValue(id, out var info))
            {
                SaleItemIndexes.TryAdd(id, ItemsForSale.Count);
                ItemsForSale.Add((id, info.Price));
            }
            else
                ServerLogger.LogWarning($"Npc {FullName} unable to sell item {itemName} it's item details could not be found in the item list.");
        }
        else
            ServerLogger.LogWarning($"Npc {FullName} unable to sell item {itemName} as it could not be found.");
    }

    public virtual void SellItem(string itemName, int price)
    {
        ItemsForSale ??= new List<(int, int)>();
        SaleItemIndexes ??= new Dictionary<int, int>();
        if (DataManager.ItemIdByName.TryGetValue(itemName, out var id))
        {
            if (DataManager.ItemList.TryGetValue(id, out var info))
            {
                var sellValue = info.SellToStoreValue;
                var profit = sellValue - price * 0.5f;
                if (profit > 0)
                {
                    throw new Exception($"Npc {Character.Name} is selling {info.Code} for {price:N0}z when it could be sold back for {sellValue:N0}z, for a profit of {profit:N0}z!");
                }

                SaleItemIndexes.TryAdd(id, ItemsForSale.Count);
                ItemsForSale.Add((id, price));
            }
            else
                ServerLogger.LogWarning($"Npc {FullName} unable to sell item {itemName} it's item details could not be found in the item list.");
        }
        else
            ServerLogger.LogWarning($"Npc {FullName} unable to sell item {itemName} as it could not be found.");
    }

    public void KillMyMobs()
    {
        var chara = Entity.Get<WorldObject>();
        var npc = chara.Npc;

        if (npc.Mobs == null)
            return;

        for (var i = 0; i < npc.Mobs.Count; i++)
        {
            if (npc.Mobs[i].TryGet(out Monster mon))
                mon.Die(false);
        }

        npc.Mobs.Clear();
        //OnMobKill();
    }

    public void KillMyMobsWithExp()
    {
        var chara = Entity.Get<WorldObject>();
        var npc = chara.Npc;

        if (npc.Mobs == null)
            return;

        for (var i = 0; i < npc.Mobs.Count; i++)
        {
            if (npc.Mobs[i].TryGet(out Monster mon))
                mon.Die();
        }

        npc.Mobs.Clear();
        //OnMobKill();
    }

    public void HideNpc()
    {
        var chara = Entity.Get<WorldObject>();

        if (chara.AdminHidden)
            return; //npc already hidden

        if (chara.Map == null)
            throw new Exception($"Npc {FullName} attempting to execute HideNpc, but the npc is not currently attached to a map.");

        //this puts the npc in a weird state where it still exists in the Instance, but not on the map
        chara.Map.RemoveEntity(ref Entity, CharacterRemovalReason.OutOfSight, false);
        chara.AdminHidden = true;

        if (HasTouch && AreaOfEffect != null)
            chara.Map.RemoveAreaOfEffect(AreaOfEffect);
    }

    public void ShowNpc(string? name = null)
    {
        var chara = Entity.Get<WorldObject>();

        if (name != null)
        {
            chara.Name = name;
            Name = name;
        }

        if (!chara.AdminHidden)
            return; //npc is already visible

        if (chara.Map == null)
            throw new Exception($"Npc {FullName} attempting to execute ShowNpc, but the npc is not currently attached to a map.");

        chara.AdminHidden = false;
        if (!IsEvent)
            chara.Map.AddEntity(ref Entity, false);
        else
            chara.Map.SendAddEntityAroundCharacter(ref Entity, Character);

        if (HasTouch && AreaOfEffect != null)
            chara.Map.CreateAreaOfEffect(AreaOfEffect);
    }

    public void ChangeNpcClass(string className)
    {
        if (!Character.AdminHidden)
            ServerLogger.LogWarning($"Changing NPC class on a non hidden NPC! This hasn't been implemented, so you shouldn't do it.");

        if (!DataManager.MonsterCodeLookup.TryGetValue(className, out var monInfo))
        {
            ServerLogger.LogError($"NPC {Character} not change NPC class to {className} as that class could not be found");
            return;
        }

        Character.ClassId = monInfo.Id;
    }

    public void RevealAsEffect(NpcEffectType type, string? name = null)
    {
        ChangeNpcClass("EFFECT");
        DisplayType = NpcDisplayType.Effect;
        EffectType = type;

        ShowNpc(name);
    }

    public void ChangeEffectType(NpcEffectType type)
    {
        DisplayType = NpcDisplayType.Effect;
        EffectType = type;

        if (Character.Map == null)
            return;

        Character.Map.AddVisiblePlayersAsPacketRecipients(Character);
        CommandBuilder.SendRemoveEntityMulti(Character, CharacterRemovalReason.OutOfSight);
        CommandBuilder.SendCreateEntityMulti(Character);
        CommandBuilder.ClearRecipients();
    }

    public void SignalMyEvents(string signal, int value1 = 0, int value2 = 0, int value3 = 0, int value4 = 0)
    {
        var events = Character.Events;
        if (events == null)
            return;

        for (var i = 0; i < events.Count; i++)
        {
            var evt = events[i];
            if (!evt.IsAlive())
                continue;
            var npc = events[i].Get<Npc>();
            npc.OnSignal(this, signal, value1, value2, value3, value4);
        }
    }

    public void SignalNpc(string npcName, string signal, int value1 = 0, int value2 = 0, int value3 = 0, int value4 = 0)
    {
        var chara = Entity.Get<WorldObject>();

        if (chara.Map == null)
            throw new Exception($"Npc {FullName} attempting to signal npc {npcName}, but the npc is not currently attached to a map.");

        if (!chara.Map.Instance.NpcNameLookup.TryGetValue(npcName, out var destNpc) || !destNpc.IsAlive())
        {
            ServerLogger.LogWarning($"Npc {FullName} attempted to signal npc {npcName}, but that npc could not be found.");
            return;
        }

        var npc = destNpc.Get<Npc>();
        npc.OnSignal(this, signal, value1, value2, value3, value4);
    }

    public Position RandomFreeTileInRange(int range)
    {
        if (Character.Map != null && Character.Map.WalkData.FindWalkableCellInArea(Area.CreateAroundPoint(Character.Position, range), out var pos))
            return pos;

        return Character.Position;
    }

    public bool IsMoving => Character.IsMoving;

    public void StartWalkToRandomTile(int distance, int speed)
    {
        Character.MoveSpeed = speed / 1000f;
        for (var d = distance; d > 0; d--)
        {
            var pos = Character.Map!.GetRandomVisiblePositionInArea(Character.Position, distance / 2, distance);
            if (pos == Character.Position) continue;
            if (Character.TryMove(pos, 0))
                return;
        }
    }

    public void StartWalkDirect(int x, int y, int speed)
    {
        Character.MoveSpeed = speed / 1000f;
        Character.TryMove(new Position(x, y), 0);
    }

    public void RegisterNpcLink(string mapName, int x, int y) => RegisterLink(mapName, x, y, true);

    public void RegisterLink(string mapName, int x, int y, bool isNpcLink = false)
    {
        if (DestinationLinks == null)
            DestinationLinks = new List<WarpDestinationLink>();
        DestinationLinks.Add(new WarpDestinationLink(mapName, new Position(x, y), isNpcLink));
    }

    private void RemoveWarpNpcNoValidLinks()
    {
        var name = !string.IsNullOrWhiteSpace(FullName) ? FullName : currentSignalTarget;
        if (DestinationLinks == null || DestinationLinks.Count == 0)
            ServerLogger.Debug($"Removing the warp npc {name} on map {Character.Map?.Name} as it has no valid links.");
        else
            ServerLogger.Debug($"Removing the warp npc {name} on map {Character.Map?.Name} as it's link to {DestinationLinks[0].TargetMap} is invalid.");
        EndEvent();
    }

    public void RemoveIfLinksInvalid()
    {
        if (DestinationLinks == null)
        {
            RemoveWarpNpcNoValidLinks();
            return;
        }

        foreach (var link in DestinationLinks)
        {
            if (World.Instance.TryGetWorldMapByName(link.TargetMap, out var _))
                return; //we're valid if we have any valid links
        }
        RemoveWarpNpcNoValidLinks();
    }

    public void CreateEvent(string eventName, Position pos, string? valueString = null) => CreateEvent(eventName, pos.X, pos.Y, 0, 0, 0, 0, valueString);
    public void CreateEvent(string eventName, Position pos, int value1, string? valueString = null) => CreateEvent(eventName, pos.X, pos.Y, value1, 0, 0, 0, valueString);
    public void CreateEvent(string eventName, Position pos, int value1, int value2, string? valueString = null) => CreateEvent(eventName, pos.X, pos.Y, value1, value2, 0, 0, valueString);
    public void CreateEvent(string eventName, Position pos, int value1, int value2, int value3, string? valueString = null) => CreateEvent(eventName, pos.X, pos.Y, value1, value2, value3, 0, valueString);
    public void CreateEvent(string eventName, Position pos, int value1, int value2, int value3, int value4, string? valueString = null) => CreateEvent(eventName, pos.X, pos.Y, value1, value2, value3, value4, valueString);

    public void CreateEvent(string eventName, int x, int y, string? valueString = null) => CreateEvent(eventName, x, y, 0, 0, 0, 0, valueString);
    public void CreateEvent(string eventName, int x, int y, int value1, string? valueString = null) => CreateEvent(eventName, x, y, value1, 0, 0, 0, valueString);
    public void CreateEvent(string eventName, int x, int y, int value1, int value2, string? valueString = null) => CreateEvent(eventName, x, y, value1, value2, 0, 0, valueString);
    public void CreateEvent(string eventName, int x, int y, int value1, int value2, int value3, string? valueString = null) => CreateEvent(eventName, x, y, value1, value2, value3, 0, valueString);

    public void CreateEvent(string eventName, int x, int y, int value1, int value2, int value3, int value4, string? valueString = null)
    {
        var chara = Entity.Get<WorldObject>();
        if (chara.Map == null)
            throw new Exception($"Npc {FullName} attempting to create event, but the npc is not currently attached to a map.");

        var eventObj = World.Instance.CreateEvent(Character.Entity, chara.Map, eventName, new Position(x, y), value1, value2, value3, value4, valueString);
        if (Owner.IsAlive())
            eventObj.Get<Npc>().Owner = Owner;
        Character.Events ??= EntityListPool.Get();
        Character.Events.ClearInactive();
        Character.Events.Add(eventObj);
    }

    public void EndEvent()
    {
        if (!IsHidden())
            HideNpc();

        if (AreaOfEffect != null)
        {
            Character.Map?.RemoveAreaOfEffect(AreaOfEffect);
            AreaOfEffect.Reset();
            World.Instance.ReturnAreaOfEffect(AreaOfEffect);
            AreaOfEffect = null;
        }

        Owner = Entity.Null;
        World.Instance.FullyRemoveEntity(ref Entity);
    }

    public void AreaSkillIndirect(Position pos, CharacterSkill skill, int lvl)
    {
        if (!Owner.IsAlive())
            return;
        var caster = Owner.Get<WorldObject>();
        if (caster.Type == CharacterType.NPC || caster.State == CharacterState.Dead)
            return;

        var info = new SkillCastInfo()
        {
            CastTime = 0,
            IsIndirect = true,
            Skill = skill,
            Level = lvl,
            TargetedPosition = pos,
            Range = 99,
            TargetEntity = Entity.Null
        };

        caster.CombatEntity.ExecuteIndirectSkillAttack(info);
    }

    public void StartCastCircle(Position pos, int size, int duration, bool isAlly = false)
    {
        var chara = Entity.Get<WorldObject>();
        if (chara.Map == null)
            throw new Exception($"Npc {FullName} attempting to play effect, but the npc is not currently attached to a map.");

        chara.Map.AddVisiblePlayersAsPacketRecipients(chara);
        CommandBuilder.StartCastCircleMulti(pos, size + 1, duration / 1000f, isAlly, false);
        CommandBuilder.ClearRecipients();
    }

    public void StartCastCircleWithSound(Position pos, int size, int duration, bool isAlly = false)
    {
        var chara = Entity.Get<WorldObject>();
        if (chara.Map == null)
            throw new Exception($"Npc {FullName} attempting to play effect, but the npc is not currently attached to a map.");

        chara.Map.AddVisiblePlayersAsPacketRecipients(chara);
        CommandBuilder.StartCastCircleMulti(pos, size + 1, duration / 1000f, isAlly, true);
        CommandBuilder.ClearRecipients();
    }

    public void PlayEffectAtMyLocation(string effect, int facing = 0)
    {
        var chara = Entity.Get<WorldObject>();
        if (chara.Map == null)
            throw new Exception($"Npc {FullName} attempting to play effect, but the npc is not currently attached to a map.");

        var id = DataManager.EffectIdForName[effect];

        chara.Map.AddVisiblePlayersAsPacketRecipients(chara);
        CommandBuilder.SendEffectAtLocationMulti(id, chara.Position, facing);
        CommandBuilder.ClearRecipients();
    }

    public void PlaySoundEffect(string fileName)
    {
        var chara = Entity.Get<WorldObject>();
        if (chara.Map == null)
            throw new Exception($"Npc {FullName} attempting to play effect, but the npc is not currently attached to a map.");

        chara.Map.AddVisiblePlayersAsPacketRecipients(chara);
        CommandBuilder.SendPlaySoundAtLocationMulti(fileName, chara.Position);
        CommandBuilder.ClearRecipients();
    }

    public void MoveNpcRelative(int x, int y)
    {
        var chara = Entity.Get<WorldObject>();
        if (chara.Map == null)
            throw new Exception($"Npc {FullName} attempting to play effect, but the npc is not currently attached to a map.");

        x = chara.Position.X + x;
        y = chara.Position.Y + y;

        //DebugMessage($"Moving npc {Name} to {x},{y}");

        chara.Map.ChangeEntityPosition3(chara, chara.Position, new Position(x, y), false);
    }

    public void DamagePlayersNearby(int damage, int area, int hitCount = 1)
    {
        var chara = Entity.Get<WorldObject>();
        if (chara.Map == null)
            throw new Exception($"Npc {FullName} attempting to deal damage, but the npc is not currently attached to a map.");

        var el = EntityListPool.Get();

        chara.Map.GatherPlayersInRange(chara.Position, area, el, false);

        foreach (var e in el)
        {
            if (e.Type == EntityType.Player)
            {
                var ch = e.Get<WorldObject>();
                var ce = e.Get<CombatEntity>();

                if (!ce.IsValidTarget(null))
                    continue;

                var di = new DamageInfo()
                { Damage = (short)damage, HitCount = (byte)hitCount, KnockBack = 0, Source = Entity, Target = e, Time = 0.3f };

                chara.Map.AddVisiblePlayersAsPacketRecipients(ch);
                CommandBuilder.TakeDamageMulti(ch, di);
                CommandBuilder.ClearRecipients();

                ch.CombatEntity.QueueDamage(di);
            }
        }

        EntityListPool.Return(el);
    }

    public void SetTimer(int timer)
    {
        var prevStart = TimerStart;
        TimerStart = Time.ElapsedTime - timer / 1000f;
        LastTimerUpdate = (timer - 1) / 1000f;

        //DebugMessage($"Setting npc {Name} timer from {prevStart} to {TimerStart} (current server time is {Time.ElapsedTime})");
    }

    public void MakeGroundTileWater(int x, int y)
    {
        Character.Map?.WalkData.MakeTileWater(new Position(x, y));
    }

    public void DebugMessage(string message)
    {
#if DEBUG
        CommandBuilder.AddAllPlayersAsRecipients();
        CommandBuilder.SendServerMessage(message);
        CommandBuilder.ClearRecipients();
#endif
    }
}