using System.Diagnostics;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Util;
using RebuildZoneServer.Networking;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Logging;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Simulation.StatusEffects.Setup;

public class CharacterStatusContainer
{
    public CombatEntity Owner = null!;
    public WorldObject Character => Owner.Character;
    public Player? Player => Owner?.Player;
    public Monster? Monster => Owner?.Character?.Monster;

    private SwapList<StatusEffectState>? statusEffects;
    private SwapList<PendingStatusEffect>? pendingStatusEffects;

    private int onAttackEffects;
    private int onHitEffects;
    private int onUpdateEffects;
    private int onEquipmentChangeEffects;
    private int onMoveEffects;

    private float nextStatusUpdate;
    private float nextExpirationCheck;

    public int TotalStatusEffectCount => statusEffects?.Count ?? 0;
    
    public void Reset()
    {
        Owner = null!;
        statusEffects?.Clear();
        pendingStatusEffects?.Clear();
        onAttackEffects = 0;
        onHitEffects = 0;
        onUpdateEffects = 0;
        onEquipmentChangeEffects = 0;
        onMoveEffects = 0;
        nextStatusUpdate = 0f;
        nextExpirationCheck = 0f;
        if (pendingStatusEffects != null)
        {
            StatusEffectPoolManager.ReturnPendingContainer(pendingStatusEffects);
            pendingStatusEffects = null;
        }
    }

    public bool HasStatusEffects() => (statusEffects?.Count > 0) || (pendingStatusEffects?.Count > 0);

    public bool HasStatusEffectOfType(CharacterStatusEffect type)
    {
        if (statusEffects == null || statusEffects.Count == 0) return false;

        for (var i = 0; i < statusEffects.Count; i++)
            if (statusEffects[i].Type == type)
                return true;

        return false;
    }

    public bool TryGetExistingStatus(CharacterStatusEffect type, out StatusEffectState outEffect)
    {
        var shareGroup = StatusEffectHandler.GetShareGroup(type);
        var hasShareGroup = !string.IsNullOrWhiteSpace(shareGroup);
        if (statusEffects != null)
        {
            for (var i = 0; i < statusEffects.Count; i++)
            {
                if (statusEffects[i].Type == type)
                {
                    outEffect = statusEffects[i];
                    return true;
                }

                if (hasShareGroup && shareGroup == StatusEffectHandler.GetShareGroup(statusEffects[i].Type))
                {
                    outEffect = statusEffects[i];
                    return true;
                }
            }
        }

        outEffect = default;

        return false;
    }

    private void RemoveIdList(ref Span<int> remove, int removeCount)
    {
        Debug.Assert(statusEffects != null);

        if (removeCount > 0)
        {
            //loop through the removals in reverse order, which should be safe
            for (var i = removeCount - 1; i >= 0; i--)
                RemoveExistingStatusEffect(remove[i]);
        }

        Owner.UpdateStats();
    }

    public void OnUpdate()
    {
        Debug.Assert(Owner != null);
        if (statusEffects == null || onUpdateEffects <= 0)
            return;

        Span<int> remove = stackalloc int[statusEffects.Count];
        var removeCount = 0;

        for (var i = 0; i < statusEffects.Count; i++)
        {
            var s = statusEffects[i];
            if (StatusEffectHandler.GetUpdateMode(s.Type).HasFlag(StatusUpdateMode.OnUpdate))
            {
                var res = StatusEffectHandler.OnUpdateTick(s.Type, Owner, ref s);
                if (res == StatusUpdateResult.EndStatus)
                {
                    remove[removeCount] = i;
                    removeCount++;
                }
                else
                    statusEffects[i] = s; //update any changed data
            }
        }

        if (removeCount > 0)
            RemoveIdList(ref remove, removeCount);
    }

    public void OnAttack(ref DamageInfo di)
    {
        Debug.Assert(Owner != null);
        if (statusEffects == null || onAttackEffects <= 0)
            return;

        Span<int> remove = stackalloc int[statusEffects.Count];
        var removeCount = 0;

        for (var i = 0; i < statusEffects.Count; i++)
        {
            var s = statusEffects[i];
            if (StatusEffectHandler.GetUpdateMode(s.Type).HasFlag(StatusUpdateMode.OnDealDamage))
            {
                var res = StatusEffectHandler.OnAttack(s.Type, Owner, ref s, ref di);
                if (res == StatusUpdateResult.EndStatus)
                {
                    remove[removeCount] = i;
                    removeCount++;
                }
                else
                    statusEffects[i] = s; //update any changed data
            }
        }

        if (removeCount > 0)
            RemoveIdList(ref remove, removeCount);
    }

    public void OnTakeDamage(ref DamageInfo di)
    {
        Debug.Assert(Owner != null);
        if (statusEffects == null || onHitEffects <= 0)
            return;

        Span<int> remove = stackalloc int[statusEffects.Count];
        var removeCount = 0;

        for (var i = 0; i < statusEffects.Count; i++)
        {
            var s = statusEffects[i];
            if (StatusEffectHandler.GetUpdateMode(s.Type).HasFlag(StatusUpdateMode.OnTakeDamage))
            {
                var res = StatusEffectHandler.OnTakeDamage(s.Type, Owner, ref s, ref di);
                if (res == StatusUpdateResult.EndStatus)
                {
                    remove[removeCount] = i;
                    removeCount++;
                }
                else
                    statusEffects[i] = s;
            }
        }

        if (removeCount > 0)
            RemoveIdList(ref remove, removeCount);
    }

    public void OnChangeEquipment()
    {
        Debug.Assert(Owner != null);
        if (statusEffects == null || onEquipmentChangeEffects <= 0)
            return;

        Span<int> remove = stackalloc int[statusEffects.Count];
        var removeCount = 0;

        for (var i = 0; i < statusEffects.Count; i++)
        {
            var s = statusEffects[i];
            if (StatusEffectHandler.GetUpdateMode(s.Type).HasFlag(StatusUpdateMode.OnChangeEquipment))
            {
                var res = StatusEffectHandler.OnChangeEquipment(s.Type, Owner, ref s);
                if (res == StatusUpdateResult.EndStatus)
                {
                    remove[removeCount] = i;
                    removeCount++;
                }
                else
                    statusEffects[i] = s;
            }
        }

        if (removeCount > 0)
            RemoveIdList(ref remove, removeCount);
    }

    public void OnMove(Position src, Position dest)
    {
        Debug.Assert(Owner != null);
        if (statusEffects == null || onMoveEffects <= 0)
            return;

        Span<int> remove = stackalloc int[statusEffects.Count];
        var removeCount = 0;

        for (var i = 0; i < statusEffects.Count; i++)
        {
            var s = statusEffects[i];
            if (StatusEffectHandler.GetUpdateMode(s.Type).HasFlag(StatusUpdateMode.OnMove))
            {
                var res = StatusEffectHandler.OnMove(s.Type, Owner, ref s, src, dest);
                if (res == StatusUpdateResult.EndStatus)
                {
                    remove[removeCount] = i;
                    removeCount++;
                }
                else
                    statusEffects[i] = s; //update any changed data
            }
        }

        if (removeCount > 0)
            RemoveIdList(ref remove, removeCount);
    }

    private bool CheckExpiredStatusEffects()
    {
        var hasUpdate = false;
        var nextExpiration = float.MaxValue;
        for (var i = 0; i < statusEffects!.Count; i++)
        {
            var status = statusEffects[i];
            if (status.Expiration < Time.ElapsedTimeFloat)
            {
                hasUpdate = true;
                RemoveExistingStatusEffect(ref status);
                i--;
            }
            else if (status.Expiration < nextExpiration)
                nextExpiration = status.Expiration;
        }

        if (Time.ElapsedTimeFloat + 5f < nextExpiration)
            nextExpirationCheck = Time.ElapsedTimeFloat + 5f; //for safety's sake
        else
            nextExpirationCheck = nextExpiration;

        return hasUpdate;
    }

    public void UpdateStatusEffects()
    {
        Debug.Assert(Owner != null);

        var hasUpdate = false;
        var time = Time.ElapsedTimeFloat;
        var performUpdateTick = false;

        if (statusEffects != null)
        {
            if (onUpdateEffects > 0 && nextStatusUpdate < time)
            {
                performUpdateTick = true;
                nextStatusUpdate += 1f;
                if (nextStatusUpdate < time)
                    nextStatusUpdate = time + 1f;
            }

            if (nextExpirationCheck < time)
                hasUpdate = CheckExpiredStatusEffects();
        }
        
        if (pendingStatusEffects != null)
        {
            for (var i = 0; i < pendingStatusEffects.Count; i++)
            {
                if (pendingStatusEffects[i].EffectiveTime < Time.ElapsedTimeFloat)
                {
                    AddNewStatusEffect(pendingStatusEffects[i].Effect, pendingStatusEffects[i].OverwriteExisting);
                    pendingStatusEffects.Remove(i);
                    i--;
                }
            }

            if (pendingStatusEffects.Count <= 0)
            {
                StatusEffectPoolManager.ReturnPendingContainer(pendingStatusEffects);
                pendingStatusEffects = null;
            }
        }

        if (hasUpdate)
            Owner.UpdateStats();

        if(performUpdateTick)
            OnUpdate();
    }

    public void RemoveStatusEffectOfType(CharacterStatusEffect type)
    {
        Debug.Assert(Owner != null);
        if (statusEffects == null)
            return;

        for (var i = 0; i < statusEffects.Count; i++)
        {
            var status = statusEffects[i];
            if (status.Type == type)
            {
                RemoveExistingStatusEffect(ref status);
                i--;
            }
        }
    }

    private void RemoveExistingStatusEffect(int id)
    {
        Debug.Assert(Owner != null && statusEffects != null);
        var status = statusEffects[id];

        if (StatusEffectHandler.GetStatusVisibility(status.Type) != StatusClientVisibility.None)
        {
            Debug.Assert(Character.Map != null);
            Character.Map.AddVisiblePlayersAsPacketRecipients(Character);
            CommandBuilder.SendRemoveStatusEffect(Character, ref status);
            CommandBuilder.ClearRecipients();
        }

        StatusEffectHandler.OnExpiration(status.Type, Owner, ref status);
        statusEffects!.Remove(id);

        var updateMode = StatusEffectHandler.GetUpdateMode(status.Type);
        if (updateMode.HasFlag(StatusUpdateMode.OnTakeDamage))
            onHitEffects--;
        if (updateMode.HasFlag(StatusUpdateMode.OnDealDamage))
            onAttackEffects--;
        if (updateMode.HasFlag(StatusUpdateMode.OnUpdate))
            onUpdateEffects--;
        if (updateMode.HasFlag(StatusUpdateMode.OnChangeEquipment))
            onEquipmentChangeEffects--;
        if (updateMode.HasFlag(StatusUpdateMode.OnMove))
            onMoveEffects--;
    }
    
    private void RemoveExistingStatusEffect(ref StatusEffectState status)
    {
        Debug.Assert(Owner != null);

        if (StatusEffectHandler.GetStatusVisibility(status.Type) != StatusClientVisibility.None)
        {
            Debug.Assert(Character.Map != null);
            if (Character.Map != null)
            {
                Character.Map.AddVisiblePlayersAsPacketRecipients(Character);
                CommandBuilder.SendRemoveStatusEffect(Character, ref status);
                CommandBuilder.ClearRecipients();
            }
        }

        StatusEffectHandler.OnExpiration(status.Type, Owner, ref status);
        statusEffects!.Remove(ref status);

        var updateMode = StatusEffectHandler.GetUpdateMode(status.Type);
        if (updateMode.HasFlag(StatusUpdateMode.OnTakeDamage))
            onHitEffects--;
        if (updateMode.HasFlag(StatusUpdateMode.OnDealDamage))
            onAttackEffects--;
        if (updateMode.HasFlag(StatusUpdateMode.OnUpdate))
            onUpdateEffects--;
        if (updateMode.HasFlag(StatusUpdateMode.OnChangeEquipment))
            onEquipmentChangeEffects--;
        if (updateMode.HasFlag(StatusUpdateMode.OnMove))
            onMoveEffects--;
    }

    public void AddNewStatusEffect(StatusEffectState state, bool replaceExisting = true, bool isRestore = false)
    {
        Debug.Assert(Owner != null);

        if (statusEffects != null && TryGetExistingStatus(state.Type, out var oldEffect))
        {
            if (!replaceExisting)
                return; // do nothing
            RemoveExistingStatusEffect(ref oldEffect);
        }

        if (statusEffects?.Count > 30)
        {
            ServerLogger.LogWarning($"Attempting to attach status effect {state.Type} to character {Owner.Character.Name}, but it would exceed 30 status effects!");
            return;
        }

        if (!isRestore)
            StatusEffectHandler.OnApply(state.Type, Owner, ref state);
        else
            StatusEffectHandler.OnRestore(state.Type, Owner, ref state);

        statusEffects ??= new SwapList<StatusEffectState>(5);
        statusEffects.Add(ref state);
        nextExpirationCheck = 0f; //this will force it to determine when the next expiration check happens


        if (Character.Map != null && StatusEffectHandler.GetStatusVisibility(state.Type) != StatusClientVisibility.None)
        {
            Character.Map.AddVisiblePlayersAsPacketRecipients(Character);
            CommandBuilder.SendApplyStatusEffect(Character, ref state);
            CommandBuilder.ClearRecipients();
        }

        Owner.UpdateStats();

        var updateMode = StatusEffectHandler.GetUpdateMode(state.Type);
        if (updateMode.HasFlag(StatusUpdateMode.OnTakeDamage))
            onHitEffects++;
        if (updateMode.HasFlag(StatusUpdateMode.OnDealDamage))
            onAttackEffects++;
        if (updateMode.HasFlag(StatusUpdateMode.OnUpdate))
            onUpdateEffects++;
        if (updateMode.HasFlag(StatusUpdateMode.OnChangeEquipment))
            onEquipmentChangeEffects++;
        if (updateMode.HasFlag(StatusUpdateMode.OnMove))
            onMoveEffects++;
    }

    public void AddPendingStatusEffect(StatusEffectState state, bool replaceExisting, float time)
    {
        Debug.Assert(Owner != null);

        if (pendingStatusEffects == null)
            pendingStatusEffects = StatusEffectPoolManager.BorrowPendingContainer();

        var pending = new PendingStatusEffect()
        {
            Effect = state,
            EffectiveTime = Time.ElapsedTimeFloat + time,
            OverwriteExisting = replaceExisting
        };

        pendingStatusEffects.Add(pending);
    }

    //doing this in an unsafe context is kinda bad but it should be safe to stackalloc effect states to hold onto them. probably.
    public unsafe void RemoveAll()
    {
        Debug.Assert(Owner != null);

        if (statusEffects == null)
            return;

        var retainLimit = 30;
        var maxRetain = statusEffects.Count < retainLimit ? statusEffects.Count : retainLimit;
        var retain = stackalloc StatusEffectState[maxRetain];
        var retainCount = 0;

        for (var i = 0; i < statusEffects.Count; i++)
        {
            var status = statusEffects[i];
            if (StatusEffectHandler.HasFlag(status.Type, StatusEffectFlags.StayOnClear))
            {
                if (retainCount >= retainLimit - 1)
                {
                    ServerLogger.LogWarning($"Could not persist status {status.Type} on character {Owner.Character.Name} as it is attempting to persist {retainLimit} effects at once.");
                    continue;
                }
                retain[retainCount] = status;
                retainCount++;
                continue;
            }
            if (StatusEffectHandler.GetStatusVisibility(status.Type) != StatusClientVisibility.None)
            {
                Debug.Assert(Character.Map != null);
                Character.Map.AddVisiblePlayersAsPacketRecipients(Character);
                CommandBuilder.SendRemoveStatusEffect(Character, ref status);
                CommandBuilder.ClearRecipients();
            }

            StatusEffectHandler.OnExpiration(status.Type, Owner, ref status);
        }
        statusEffects.Clear();

        for (var i = 0; i < retainCount; i++)
            statusEffects.Add(retain[i]);

        //In theory, you can lose StayOnClear effects if they are pending when you die but in practice it's probably not a big deal?
        if (pendingStatusEffects != null)
        {
            pendingStatusEffects.Clear();
            StatusEffectPoolManager.ReturnPendingContainer(pendingStatusEffects);
            pendingStatusEffects = null;
        }
    }

    public void ClearAllWithoutRemoveHandler() => statusEffects?.Clear();

    public void PrepareCreateEntityMessage(OutboundMessage msg)
    {
        if (statusEffects == null)
        {
            msg.Write(false);
            return;
        }

        for (var i = 0; i < statusEffects.Count; i++)
        {
            if (StatusEffectHandler.GetStatusVisibility(statusEffects[i].Type) != StatusClientVisibility.None)
            {
                msg.Write(true);
                msg.Write((byte)statusEffects[i].Type);
                msg.Write(statusEffects[i].Expiration - Time.ElapsedTimeFloat);
            }
        }

        msg.Write(false);
    }

    public int Serialize(IBinaryMessageWriter bw)
    {
        if (statusEffects == null)
            return 0;

        var saveCount = 0;
        foreach (var status in statusEffects)
        {
            if (StatusEffectHandler.HasFlag(status.Type, StatusEffectFlags.NoSave))
                continue;
            status.Serialize(bw);
            saveCount++;
        }

        return saveCount;
    }

    public void Deserialize(IBinaryMessageReader br, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var status = StatusEffectState.Deserialize(br);
            if(status.Type != CharacterStatusEffect.None && status.Expiration > Time.ElapsedTimeFloat)
                AddNewStatusEffect(status, true, true);
        }
    }
}