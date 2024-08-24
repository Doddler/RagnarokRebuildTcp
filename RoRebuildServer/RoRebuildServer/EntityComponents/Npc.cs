using System;
using System.Buffers;
using System.Diagnostics;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.Data;
using RoRebuildServer.Data.Monster;
using RoRebuildServer.EntityComponents.Character;
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

    public AreaOfEffect? AreaOfEffect;

    public EntityList? Mobs;
    public int MobCount => Mobs?.Count ?? 0;

    public EntityList? Events;
    public int EventsCount => Events?.Count ?? 0;

    private int[]? valuesInt;
    private string[]? valuesString;

    public int[]? ParamsInt;
    public string? ParamString;

    public NpcBehaviorBase Behavior = null!;

    public float TimerUpdateRate;
    public float LastTimerUpdate;
    public double TimerStart;

    public bool HasTouch;
    public bool HasInteract;
    public bool TimerActive;
    public bool IsEvent;

    private string? currentSignalTarget;

    //private SkillCastInfo? skillInfo;

    public bool IsHidden() => !Entity.Get<WorldObject>().Hidden;
    public Position SelfPosition => Character.Position;

    public int[] ValuesInt => valuesInt ??= ArrayPool<int>.Shared.Rent(NpcInteractionState.StorageCount);
    public string[] ValuesString => valuesString ??= ArrayPool<string>.Shared.Rent(NpcInteractionState.StorageCount);

    public void Update()
    {
        if (TimerActive && TimerStart + LastTimerUpdate + TimerUpdateRate < Time.ElapsedTime)
        {
            UpdateTimer();
        }

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
            AreaOfEffect.Reset();
            World.Instance.ReturnAreaOfEffect(AreaOfEffect);
            AreaOfEffect = null!;
        }

        if (Events != null)
        {
            Events.Clear();
            EntityListPool.Return(Events);
            Events = null;
        }

        if (valuesInt != null)
            ArrayPool<int>.Shared.Return(valuesInt);
        if (valuesString != null)
            ArrayPool<string>.Shared.Return(valuesString);

        valuesInt = null!;
        valuesString = null!;
        Entity = Entity.Null;
        Behavior = null!;
        Mobs = null;
        ParamsInt = null;
        ParamString = null;
        Character = null!;
        currentSignalTarget = null;
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

    private void EnsureMobListCreated(int capacity = 4)
    {

        var mobs = Mobs;
        if (mobs == null)
        {
            mobs = new EntityList(capacity);
            Mobs = mobs;
        }
        else
            mobs.ClearInactive();
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

        EnsureMobListCreated(1);
        var mob = CreateSingleMonsterWithAutoOwnership(Character.Map, monster, area);

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

        EnsureMobListCreated(count);

        for (int i = 0; i < count; i++)
            CreateSingleMonsterWithAutoOwnership(chara.Map, monster, area);
    }

    public void SummonMobsNearby(int count, string name, int width = 0, int height = 0, int offsetX = 0, int offsetY = 0)
    {
        var chara = Entity.Get<WorldObject>();

        Debug.Assert(chara.Map != null, $"Npc {Name} cannot summon mobs {name} nearby, it is not currently attached to a map.");

        var monster = DataManager.MonsterCodeLookup[name];

        var area = Area.CreateAroundPoint(chara.Position + new Position(offsetX, offsetY), width, height);

        EnsureMobListCreated(count);

        for (int i = 0; i < count; i++)
            CreateSingleMonsterWithAutoOwnership(chara.Map, monster, area);
    }

    private Entity CreateSingleMonsterWithAutoOwnership(Map map, MonsterDatabaseInfo monster, Area spawnArea)
    {
        var m = World.Instance.CreateMonster(map, monster, spawnArea, null);

        if (IsEvent && Owner.TryGet<WorldObject>(out var owner) && owner.Type == CharacterType.Monster)
            owner.Monster.AddChild(ref m);
        else
        {
            Mobs.Add(m);
        }

        return m;
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

        if (npc.Events == null)
            return;

        npc.Events.ClearInactive();

        for (var i = 0; i < npc.Events.Count; i++)
            npc.Events[i].Get<Npc>().EndEvent();

        npc.Events.Clear();
        //OnMobKill();
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

        if (chara.Hidden)
            return; //npc already hidden

        if (chara.Map == null)
            throw new Exception($"Npc {FullName} attempting to execute HideNpc, but the npc is not currently attached to a map.");

        //this puts the npc in a weird state where it still exists in the Instance, but not on the map
        chara.Map.RemoveEntity(ref Entity, CharacterRemovalReason.OutOfSight, false);
        chara.Hidden = true;

        if (HasTouch && AreaOfEffect != null)
            chara.Map.RemoveAreaOfEffect(AreaOfEffect);
    }

    public void ShowNpc(string? name = null)
    {
        var chara = Entity.Get<WorldObject>();

        if (name != null)
            chara.Name = name;

        if (!chara.Hidden)
            return; //npc is already visible

        if (chara.Map == null)
            throw new Exception($"Npc {FullName} attempting to execute ShowNpc, but the npc is not currently attached to a map.");

        chara.Hidden = false;
        if (!IsEvent)
            chara.Map.AddEntity(ref Entity, false);
        else
            chara.Map.SendAddEntityAroundCharacter(ref Entity, Character);

        if (HasTouch && AreaOfEffect != null)
            chara.Map.CreateAreaOfEffect(AreaOfEffect);
    }

    public void ChangeNpcClass(string className)
    {
        if (!Character.Hidden)
            ServerLogger.LogWarning($"Changing NPC class on a non hidden NPC! This hasn't been implemented, so you shouldn't do it.");

        if (!DataManager.MonsterCodeLookup.TryGetValue(className, out var monInfo))
        {
            ServerLogger.LogError($"NPC {Character} not change NPC class to {className} as that class could not be found");
            return;
        }

        Character.ClassId = monInfo.Id;
    }

    public void SignalMyEvents(string signal, int value1 = 0, int value2 = 0, int value3 = 0, int value4 = 0)
    {
        if (Events == null)
            return;

        for (var i = 0; i < Events.Count; i++)
        {
            var evt = Events[i];
            if (!evt.IsAlive())
                continue;
            var npc = Events[i].Get<Npc>();
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
            var pos = Character.Map.GetRandomVisiblePositionInArea(Character.Position, distance / 2, distance);
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

        var eventObj = World.Instance.CreateEvent(chara.Map, eventName, new Position(x, y), value1, value2, value3, value4, valueString);
        if (Owner.IsAlive())
            eventObj.Get<Npc>().Owner = Owner;
        Events ??= EntityListPool.Get();
        Events.ClearInactive();
        Events.Add(eventObj);
    }

    public void EndEvent()
    {
        if (!IsHidden())
            HideNpc();

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

    public void DebugMessage(string message)
    {
#if DEBUG
        CommandBuilder.AddAllPlayersAsRecipients();
        CommandBuilder.SendServerMessage(message);
        CommandBuilder.ClearRecipients();
#endif
    }
}