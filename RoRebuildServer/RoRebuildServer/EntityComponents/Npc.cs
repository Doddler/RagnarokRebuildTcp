using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents.Npcs;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.EntitySystem;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.EntityComponents;

[EntityComponent(EntityType.Npc)]
public class Npc : IEntityAutoReset
{
    public Entity Entity;
    public string Name { get; set; } = null!; //making this a property makes it accessible via npc scripting
    
    public AreaOfEffect? AreaOfEffect;

    public EntityList? Mobs;

    [EntityIgnoreNullCheck] public int[] ValuesInt = new int[NpcInteractionState.StorageCount];
    [EntityIgnoreNullCheck] public string[] ValuesString = new string[NpcInteractionState.StorageCount];

    public NpcBehaviorBase Behavior = null!;

    public float TimerUpdateRate;
    public float LastTimerUpdate;
    public double TimerStart;

    public bool HasTouch;
    public bool HasInteract;
    public bool TimerActive;


    public void Update()
    {
        if (TimerActive && TimerStart + LastTimerUpdate + TimerUpdateRate < Time.ElapsedTime)
        {
            UpdateTimer();
        }
    }

    public void Reset()
    {
        if (AreaOfEffect != null)
        {
            AreaOfEffect.Reset();
            World.Instance.ReturnAreaOfEffect(AreaOfEffect);
            AreaOfEffect = null!;
        }

        for (var i = 0; i < NpcInteractionState.StorageCount; i++)
        {
            ValuesInt[i] = 0;
            ValuesString[i] = String.Empty;
        }

        Entity = Entity.Null;
        Behavior = null!;
        Mobs = null;
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

        LastTimerUpdate = newTime; //update this before OnTimer since OnTimer may call ResetTimer

        Behavior.OnTimer(this, lastTime, newTime);
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

    public void StopTimer() => EndTimer();

    public void EndTimer()
    {
        TimerActive = false;
    }

    public void CancelInteraction(Player player)
    {
        if (!player.IsInNpcInteraction)
            return;

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


    public void SummonMobsNearby(int count, string name, int width = 0, int height = 0, int offsetX = 0, int offsetY = 0)
    {
        var chara = Entity.Get<WorldObject>();

        var monster = DataManager.MonsterCodeLookup[name];

        var area = Area.CreateAroundPoint(chara.Position + new Position(offsetX, offsetY), width, height);

        var mobs = Mobs;
        if (mobs == null)
        {
            mobs = new EntityList(count);
            Mobs = mobs;
        }
        else
            mobs.ClearInactive();

        for (int i = 0; i < count; i++)
            mobs.Add(World.Instance.CreateMonster(chara.Map, monster, area, null));
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
                mon.Die();
        }
    }
}