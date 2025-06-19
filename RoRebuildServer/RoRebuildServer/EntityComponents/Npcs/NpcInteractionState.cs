using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Util;
using RoRebuildServer.EntitySystem;
using RoRebuildServer.Logging;
using RoRebuildServer.Networking;
using static System.Net.Mime.MediaTypeNames;
using System.Xml.Linq;
using Microsoft.Extensions.Options;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Simulation;
using System;
using System.Diagnostics;
using RebuildSharedData.ClientTypes;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.Data.Scripting;
using RoRebuildServer.Database.Requests;
using RoRebuildServer.EntityComponents.Items;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
using RoRebuildServer.Database;
using System.Numerics;

namespace RoRebuildServer.EntityComponents.Npcs;

public class NpcInteractionState
{
    public Entity NpcEntity;
    public Player? Player;
    public int Step;
    public int OptionResult = -1;
    
    public const int StorageCount = 10;
    
    public bool[] ValidOptions = new bool[10];
    public int[] ValuesInt = new int[StorageCount];
    public string?[] ValuesString = new string[StorageCount];

    public NpcInteractionResult InteractionResult { get; set; }
    public bool IsTouchEvent { get; set; }
    public bool IsBuyingFromNpc { get; set; }
    public bool AllowDiscount { get; set; }
    public string? CurrentShopCategory;


    public void Reset()
    {
        NpcEntity = Entity.Null;
        Player = null;

        for (var i = 0; i < ValuesInt.Length; i++)
        {
            ValuesInt[i] = 0;
            ValuesString[i] = null;
        }
    }

    public void BeginInteraction(ref Entity npc, Player player)
    {
#if DEBUG
        if(NpcEntity != Entity.Null || Player != null)
            ServerLogger.LogWarning($"Attempting to begin npc interaction with {npc} but an interaction already appears to exist!");
#endif

        NpcEntity = npc;
        Player = player;
        Step = 0;
        InteractionResult = NpcInteractionResult.WaitForTime;
    }

    public void CancelInteraction()
    {
        if (Player == null)
            return;

        if (!NpcEntity.IsAlive())
        {
            Player.IsInNpcInteraction = false;
            Reset();
            return;
        }

        ServerLogger.Debug($"Player {Player} had an NPC interaction cancelled.");

        var npc = NpcEntity.Get<Npc>();
        npc.CancelInteraction(Player); //this will trigger a reset on this object
    }

    public void ContinueInteraction()
    {
        if (Player == null)
            return;

        var npc = NpcEntity.Get<Npc>();
        npc.Advance(Player);
    }
    
    public void OptionInteraction(int result)
    {
        if (Player == null)
            return;

        var npc = NpcEntity.Get<Npc>();

        if (result < 10 && !ValidOptions[result])
        {
            ServerLogger.LogWarning($"Player {Player.Character.Name} tried to pick option {result} on npc {npc.FullName}, but that option is flagged as invalid.");
            CancelInteraction();
            return;
        }
        
        npc.OptionAdvance(Player, result);
    }

    public void FinishLoadingStorage(StorageLoadRequest storage)
    {
        Debug.Assert(Player != null);

        if (storage.StorageBag == null)
            Player.StorageInventory = CharacterBag.Borrow();
        else
            Player.StorageInventory = storage.StorageBag;

        Player.StorageId = storage.StorageId;

        FinishOpeningStorage();
    }

    public void OpenStorage()
    {
        if (Player == null)
            return;

        if (Player.Connection.LoadStorageRequest != null && !Player.Connection.LoadStorageRequest.IsComplete)
            return;

        if (Player.StorageInventory != null)
        {
            Player.Connection.LoadStorageRequest = null;
            return;
        }
        Player.Connection.LoadStorageRequest = new StorageLoadRequest(Player.Connection.AccountId);
        RoDatabase.EnqueueDbRequest(Player.Connection.LoadStorageRequest);

    }

    public void FinishOpeningStorage()
    {
        Debug.Assert(Player != null);

        CommandBuilder.SendNpcStorage(Player);
        InteractionResult = NpcInteractionResult.WaitForStorage;
    }

    public void SetFlag(string name, int value)
    {
        if (name.Length > 16)
            throw new Exception($"Npc flag '{name}' is too long! It must be 16 or fewer characters in length.");
        Player?.SetNpcFlag(name, value);
    }
    public int GetFlag(string name) => Player?.GetNpcFlag(name) ?? 0;
    public int Level => Player?.GetStat(CharacterStat.Level) ?? 0;
    public int JobLevel => Player?.GetData(PlayerStat.JobLevel) ?? 0;
    public int UnusedSkillPoints => Player?.GetData(PlayerStat.SkillPoints) ?? 99;
    public int JobId => Player?.GetData(PlayerStat.Job) ?? 0;
    public int InventoryItemCount => Player?.Inventory?.UsedSlots ?? 0;
    public int MaxItemCount => CharacterBag.MaxBagSlots;
    public void ChangePlayerJob(int jobId) => Player?.ChangeJob(jobId);
    public void SkillReset() => Player?.SkillReset();
    public void StatPointReset() => Player?.StatPointReset();
    public bool HasLearnedSkill(CharacterSkill skill, int level = 1) => Player?.DoesCharacterKnowSkill(skill, level) ?? false;
    public bool HasCart => Player?.GetData(PlayerStat.PushCart) > 0;

    public void FocusNpc()
    {
        if (Player == null)
            return;

        CommandBuilder.SendFocusNpc(Player, NpcEntity.Get<Npc>(), true);
    }

    public void ReleaseFocus()
    {
        if (Player == null)
            return;

        CommandBuilder.SendFocusNpc(Player, NpcEntity.Get<Npc>(), false);
    }

    public void GiveZeny(int val)
    {
        if (Player == null) return;
        Player.AddZeny(val);
        CommandBuilder.SendServerEvent(Player, ServerEvent.GetZeny, val);
    }

    public void GiveItem(string itemName, int count = 1)
    {
        if (Player == null) return;

        if (!DataManager.ItemIdByName.TryGetValue(itemName, out var item))
        {
            ServerLogger.LogWarning($"NPC {NpcEntity.Get<WorldObject>()} attempted to give player {Player} item {itemName}, but that item doesn't seem to be valid.");
            return;
        }

        var itemRef = new ItemReference(item, count);
        var bagId = Player.AddItemToInventory(itemRef);

        itemRef.Count = Player.Inventory?.GetItemCount(item) ?? 0;
        
        CommandBuilder.AddItemToInventory(Player, itemRef, bagId, count);
    }

    public bool TakeItem(string itemName, int count = 1)
    {
        if (Player == null) return false;
        var npc = NpcEntity.Get<Npc>();

        if (!DataManager.ItemIdByName.TryGetValue(itemName, out var itemId))
        {
            ServerLogger.LogWarning($"NPC {NpcEntity.Get<WorldObject>()} attempted to take {count} {itemName} from player {Player}, but that item doesn't seem to be valid.");
            return false;
        }

        var item = DataManager.GetItemInfoById(itemId);
        if(item == null) 
            return false;

        if (item.IsUnique)
        {
            ServerLogger.LogWarning($"NPC {NpcEntity.Get<WorldObject>()} attempted to take {count} {itemName} from player {Player}, but TakeItem doesn't work with unique items yet.");
            return false;
        }

        return Player.TryRemoveItemFromInventory(itemId, count, true);
    }

    public bool HasJobType(string jobName)
    {
        if (Player == null) return false;
        return DataManager.IsJobInEquipGroup(jobName, Player.GetData(PlayerStat.Job));
    }

    public void ShowEffectOnPlayer(string effectName)
    {
        if (Player == null || Player.Character.Map == null) return;
        if (!NpcEntity.TryGet<WorldObject>(out var npc)) return;

        var id = DataManager.EffectIdForName[effectName];

        Player.Character.Map.AddVisiblePlayersAsPacketRecipients(Player.Character);
        CommandBuilder.SendEffectOnCharacterMulti(Player.Character, id);
        CommandBuilder.ClearRecipients();
    }

    public void ChangePlayerGender()
    {
        if (Player == null || Player.Character.Map == null) return;
        var gender = Player.GetData(PlayerStat.Gender);
        Player.SetData(PlayerStat.Gender, gender == 0 ? 1 : 0);
        Player.Character.Map.RefreshEntity(Player.Character);
        Player.UpdateStats();
    }

    public void ChangePlayerHairToRandom()
    {
        if (Player == null || Player.Character.Map == null) return;
        Player.SetData(PlayerStat.Head, GameRandom.NextInclusive(0, 28));
        Player.Character.Map.RefreshEntity(Player.Character);
        Player.UpdateStats();
    }

    public void ChangePlayerAppearanceToRandom()
    {
        if (Player == null || Player.Character.Map == null) return;
        Player.SetData(PlayerStat.Gender, GameRandom.NextInclusive(0, 1));
        Player.SetData(PlayerStat.Head, GameRandom.NextInclusive(0, 28));
        Player.Character.Map.RefreshEntity(Player.Character);
        Player.UpdateStats();
    }

    public void ResetCharacterToInitialState()
    {
        if (Player == null || Player.Character.Map == null) return;
        Player.ChangeJob(0);
        Player.JumpToLevel(1);
        Player.SkillReset();
        Player.StatPointReset();
        Player.Init();
        Player.Character.Map.RefreshEntity(Player.Character);
        Player.UpdateStats();
    }

    public int GetLearnedLevelOfSkill(string skillName)
    {
        if (Player == null) return 0;

        if(!Enum.TryParse(skillName, true, out CharacterSkill skill))
            return 0;

        return Player.MaxLearnedLevelOfSkill(skill);
    }


    public void ShowSprite(string spriteName, int pos)
    {
        //Console.WriteLine("ShowSprite " + spriteName);

        if (Player == null)
            return;
        
        CommandBuilder.SendNpcShowSprite(Player, spriteName, pos);
    }
    
    public void Option(params string[] options)
    {
        //Console.WriteLine("Option");
        //foreach(var s in options)
        //    Console.WriteLine(" - " + s);

        if (Player == null)
            return;

        var optCount = options.Length;
        if (optCount > 10)
        {
            var npc = NpcEntity.Get<Npc>();
            ServerLogger.LogWarning($"Npc interaction with {npc.FullName} attempting to create an Option with more than 10 options!");
            optCount = 10;
        }

        for (var i = 0; i < 10; i++)
            ValidOptions[i] = i < optCount && !string.IsNullOrWhiteSpace(options[i]); //an empty string can't be selected by the user

        CommandBuilder.SendNpcOption(Player, options);
    }

    public void PromptForCount(string requestDesc, int maxCount)
    {

    }

    public void OpenRefineDialog()
    {
        if (Player == null) return;
        CommandBuilder.SendNpcOpenRefineDialog(Player);
    }

    public void OpenShop(bool hasDiscount = true)
    {
        if (Player == null)
            return;
        CommandBuilder.SendNpcOpenShop(Player, NpcEntity.Get<Npc>(), hasDiscount);
        IsBuyingFromNpc = true;
        AllowDiscount = hasDiscount;
    }

    public void StartItemTrade(string setName = "Default")
    {
        if (Player == null || !NpcEntity.TryGet<Npc>(out var npc))
        {
            CancelInteraction();
            return;
        }

        if (npc.TradeItemSets == null || !npc.TradeItemSets.TryGetValue(setName, out var set))
        {
            ServerLogger.LogWarning($"Attempting to StartItemTrade for NPC {npc.FullName} item set {setName}, but that set does not exist.");
            CancelInteraction();
            return;
        }

        CurrentShopCategory = setName;
        CommandBuilder.SendNpcBeginTrading(Player, npc, set);
    }

    public void FinalizeItemTrade(int itemEntry, int tradeCount, Span<int> submittedBagIds)
    {
        if (Player == null || Player.Inventory == null || !NpcEntity.TryGet<Npc>(out var npc) || npc.TradeItemSets == null || string.IsNullOrWhiteSpace(CurrentShopCategory) || tradeCount <= 0)
            return;

        if (!npc.TradeItemSets.TryGetValue(CurrentShopCategory, out var tradeSet))
        {
            ServerLogger.LogWarning($"Could not FinalizeItemTrade, the current shop category '{CurrentShopCategory}' was not found on the npc.");
            return;
        }

        if (itemEntry < 0 || itemEntry >= tradeSet.Count)
        {
            CommandBuilder.ErrorMessage(Player, $"Could not complete the trade.");
            ServerLogger.LogWarning($"Player submitted a FinalizeTradeItem request for item {itemEntry} in category '{CurrentShopCategory}', but that ID is out of bounds.");
            return;
        }

        var trade = tradeSet[itemEntry];
        if (trade.IsCrafted && tradeCount != 1)
        {
            CommandBuilder.ErrorMessage(Player, $"Could not complete the trade.");
            ServerLogger.LogWarning($"Player submitted a FinalizeTradeItem request for item {tradeCount}x {itemEntry} in category '{CurrentShopCategory}', but you can't get more than 1 of an item.");
            return;
        }

        if (Player.GetZeny() < tradeCount * trade.ZenyCost)
        {
            CommandBuilder.ErrorMessage(Player, $"You don't have enough zeny to complete this trade.");
            return;
        }

        //link the submitted list of equipment items to their proper item types
        var bagIdCount = submittedBagIds.Length;
        Span<int> resolvedBagItemTypes = stackalloc int[submittedBagIds.Length];

        for (var i = 0; i < submittedBagIds.Length; i++)
        {
            if (!Player.Inventory.GetItem(submittedBagIds[i], out var item))
            {
                CommandBuilder.ErrorMessage(Player, $"Could not complete the trade.");
                ServerLogger.LogWarning($"Player submitted FinalizeTradeItem and provided a bagId item of {submittedBagIds[i]}, but they don't have that item in their inventory.");
                return;
            }

            resolvedBagItemTypes[i] = item.UniqueItem.Id;
        }

        Span<int> removeIds = stackalloc int[trade.ItemRequirements.Count]; //a combined list of all the bagIds we want to remove
        Span<int> removeCounts = stackalloc int[trade.ItemRequirements.Count]; //the counts for each of the bag items
        Span<int> equippedItems = stackalloc int[trade.ItemRequirements.Count]; //any items we need to take off if the trade succeeds
        var equipCount = 0;

        //validate we have all the items for the request
        for (var i = 0; i < trade.ItemRequirements.Count; i++)
        {
            var (req, count) = trade.ItemRequirements[i];
            var item = DataManager.GetItemInfoById(req);
            if (item == null)
                throw new Exception($"Could not process FinalizeTradeItem, the item requirement {req} is invalid!");
            if (item.IsUnique && tradeCount > 1)
                throw new Exception($"Could not process FinalizeTradeItem, you cannot require unique items for a trade with a count above 1!");
            count *= tradeCount;
            if (!item.IsUnique)
            {
                removeIds[i] = req;
                removeCounts[i] = count;
                if (Player.Inventory.GetItemCount(req) >= count) continue;
                CommandBuilder.ErrorMessage(Player, $"Insufficient items available to complete trade.");
                return;
            }

            var hasMatch = false;
            for (var j = 0; j < bagIdCount; j++)
            {
                if (req == resolvedBagItemTypes[j])
                {
                    hasMatch = true;
                    //we need to keep track of which items are equipped so we can take them off first
                    if (Player.Equipment.IsItemEquipped(submittedBagIds[j]))
                    {
                        equippedItems[equipCount] = submittedBagIds[j];
                        equipCount++;
                    }

                    //add this unique item to the list of items we'll remove
                    removeIds[i] = submittedBagIds[j];
                    removeCounts[i] = 1;
                    //remove the current entry from the list so it can't be used again
                    resolvedBagItemTypes[j] = resolvedBagItemTypes[bagIdCount - 1];
                    submittedBagIds[j] = submittedBagIds[bagIdCount - 1];
                    bagIdCount--;
                    break;
                }
            }

            if (!hasMatch)
            {
                CommandBuilder.ErrorMessage(Player, $"Insufficient items are available to complete the trade.");
                return;
            }
        }

        if (bagIdCount > 0)
        {
            CommandBuilder.ErrorMessage(Player, $"Could not complete the trade.");
            ServerLogger.LogWarning($"Player attempted to trade npc {npc.Character.Name} for item {trade.ItemId}, but they submitted {bagIdCount} more items than expected.");
            return;
        }

        for(var i = 0; i < equippedItems.Length; i++)
            Player.Equipment.UnEquipItem(equippedItems[i]);

        //we have everything we need, take the stuff out of our inventory and give us our item
        for (var i = 0; i < trade.ItemRequirements.Count; i++)
        {
            Player.Inventory.RemoveItemByBagId(removeIds[i], removeCounts[i]);
            CommandBuilder.RemoveItemFromInventory(Player, removeIds[i], removeCounts[i]);
        }
        Player.DropZeny(tradeCount * trade.ZenyCost);
        Player.CreateItemInInventory(new ItemReference(trade.CombinedItem.Id, trade.CombinedItem.Count * tradeCount));
        CommandBuilder.SendServerEvent(Player, ServerEvent.TradeSuccess);
    }

    public void StartSellToNpc()
    {
        if (Player == null)
            return;
        CommandBuilder.SendNpcSellToShop(Player);
        IsBuyingFromNpc = false;
    }

    public void OpenShop(string[] items)
    {

    }

    public void MoveTo(string mapName, int x, int y)
    {
        MoveTo(mapName, x, y, 1, 1);
    }
    
    public void MoveTo(string mapName, int x, int y, int width, int height)
    {
        //ServerLogger.Log("Warp to " + mapName);
        if (Player == null)
            return;

        Player.Character.StopMovingImmediately();

        var ch = Player.Character;
        var ce = Player.CombatEntity;

        if (ch.Map == null)
            return;

        ServerLogger.Log($"Moving player {Player.Name} via npc or warp to map {mapName} {x},{y}");

        if (!Player.WarpPlayer(mapName, x, y, 1, 1, false))
            ServerLogger.LogWarning($"Failed to move player to {mapName}!");
    }

    public void Dialog(string name, string text, bool isBig = false)
    {
        if (Player == null)
            return;

        //Console.WriteLine($"Dialog {name}: {text}");
        CommandBuilder.SendNpcDialog(Player, name, text, isBig);
    }

    public void DialogBig(string name, string text)
    {
        if (Player == null)
            return;

        //Console.WriteLine($"Dialog {name}: {text}");
        CommandBuilder.SendNpcDialog(Player, name, text, true);
    }


    public int GetItemCount(string str)
    {
        if (Player?.Inventory == null || !DataManager.ItemIdByName.TryGetValue(str, out var itemId))
            return 0;

        return Player.Inventory.GetItemCount(itemId);
    }

    public bool HasItem(string str)
    {
        if (Player?.Inventory == null || !DataManager.ItemIdByName.TryGetValue(str, out var itemId))
            return false;

        return Player.Inventory.HasItem(itemId);
    }

    public bool DropItem(string str, int count)
    {
        if (Player?.Inventory == null || !DataManager.ItemIdByName.TryGetValue(str, out var itemId))
            return false;

        return Player.TryRemoveItemFromInventory(itemId, count, true);
    }

    public int GetZeny()
    {
        if (Player == null)
            return 0;
        return Player.GetZeny();
    }

    public void DropZeny(int zeny)
    {
        if (Player == null)
            return;
        Player.DropZeny(zeny);
    }

    public void EquipPushCart()
    {
        if (Player == null)
            return;

        var cartStyle = Level switch
        {
            < 41 => 0,
            < 66 => 1,
            < 81 => 2,
            < 91 => 3,
            _ => 4
        };
        var follower = cartStyle switch
        {
            1 => PlayerFollower.Cart1,
            2 => PlayerFollower.Cart2,
            3 => PlayerFollower.Cart3,
            4 => PlayerFollower.Cart4,
            _ => PlayerFollower.Cart0
        };

        Player.SetData(PlayerStat.PushCart, cartStyle);
        Player.PlayerFollower = follower;

        CommandBuilder.UpdatePlayerFollowerStateAutoVis(Player);
    }
}