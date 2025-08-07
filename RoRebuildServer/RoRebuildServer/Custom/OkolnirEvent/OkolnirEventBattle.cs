using Microsoft.Net.Http.Headers;
using RebuildSharedData.Data;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents.Npcs;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Simulation.Util;
using Tomlyn.Syntax;
using RoRebuildServer.Database.Domain;
using RoRebuildServer.EntitySystem;
using RoRebuildServer.Networking;
using System.Xml.Linq;
using System;
using RoRebuildServer.EntityComponents.Items;

namespace RoRebuildServer.Custom.OkolnirEvent;

public class OkolnirEventBattle : INpcLoader
{
    public void Load()
    {
        DataManager.RegisterEvent("OkolnirDamageZone", new OkolnirDamageZoneObjectEvent());
        DataManager.RegisterEvent("ExaflareControlEvent", new ExaflareControlEvent());
        DataManager.RegisterEvent("ExaflareRowEvent", new ExaflareRowEvent());
        DataManager.RegisterEvent("ExaflareBlastEvent", new ExaflareBlastEvent());
        DataManager.RegisterEvent("EarthShakerCastEvent", new EarthShakerSkillEvent());

        DataManager.NpcManager.RegisterSpecialDeathEvent("OkolnirEnd", OnOkolnirEnd);
    }

    record OkolnirDamageList(Entity Entity, int Damage);
    
    private static void OnOkolnirEnd(Monster boss)
    {
        if (boss.Character.Map == null)
            return;
        boss.TotalDamageReceived?.ClearDeadAndNotOnMap(boss.Character.Map);
        if (boss.TotalDamageReceived == null || boss.TotalDamageReceived.Count == 0)
            return;

        var list = new List<OkolnirDamageList>(boss.TotalDamageReceived.Count);

        var maxChance = 10;
        foreach (var (entity, dmg) in boss.TotalDamageReceived)
            list.Add(new OkolnirDamageList(entity, dmg));

        CommandBuilder.AddRecipients(boss.Character.Map.Players);
        CommandBuilder.SendServerMessage("You have received an item reward for your contribution to the event.", "", false);
        CommandBuilder.ClearRecipients();

        var count = 0;
        list.Sort((a,b) => b.Damage.CompareTo(a.Damage));
        foreach (var d in list)
        {
            if (!d.Entity.TryGet<Player>(out var player))
                continue;

            var max = 10 + count * 10;
            var rnd = GameRandom.Next(0, max);
            var item = rnd switch
            {
                < 15 => 616, //oca
                < 20 => 969, //gold
                < 40 => 617, //old purple box
                _ => 603 //old blue box
            };

            //first damage contributor: guaranteed oca
            //second damage contributor: 75% oca, 25% gold
            //third damage contributor: 50% oca, 16% gold, 33% opb
            //fourth damage contributor: 37% oca, 12% gold, 50% opb
            //fifth damage contributor: 30% oca, 10% gold, 40% opb, 10% obb
            //...10th damage contributor: 15% oca, 5% gold, 20% opb, 60% obb

            player.CreateItemInInventory(new ItemReference(item, 1));
        }

    }
}
