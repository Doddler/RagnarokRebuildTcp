using System.Diagnostics;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Util;
using RebuildZoneServer.Networking;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Util;
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
    
    private byte onAttackEffects;
    private byte onHitEffects;
    private byte onUpdateEffects;
    private byte onEquipmentChangeEffects;
    private byte onMoveEffects;
    private byte onCalculateDamageTakenEffects;
    private byte onPreCalculateCombatResultEffects;

    private double nextStatusUpdate;
    private double nextExpirationCheck;

    private HashSet<CharacterStatusEffect>? preserveOnDeathStatuses;

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
        onCalculateDamageTakenEffects = 0;
        onPreCalculateCombatResultEffects = 0;
        nextStatusUpdate = 0f;
        nextExpirationCheck = 0f;
        preserveOnDeathStatuses?.Clear();
        if (pendingStatusEffects != null)
        {
            StatusEffectPoolManager.ReturnPendingContainer(pendingStatusEffects);
            pendingStatusEffects = null;
        }
    }

    public void RollBackTimers(float time)
    {
        if (statusEffects != null)
        {
            for (var i = 0; i < statusEffects.Count; i++)
            {
                var s = statusEffects[i];
                s.Expiration -= time;
                statusEffects[i] = s;
            }
        }
        if (pendingStatusEffects != null)
        {
            for (var i = 0; i < pendingStatusEffects.Count; i++)
            {
                var p = pendingStatusEffects[i];
                var s = p.Effect;
                s.Expiration -= time;
                p.EffectiveTime -= time;
                p.Effect = s;
                pendingStatusEffects[i] = p;
            }
        }
    }

    public void SetStatusToKeepOnDeath(CharacterStatusEffect type)
    {
        if (preserveOnDeathStatuses == null)
            preserveOnDeathStatuses = new();

        preserveOnDeathStatuses.Add(type);
    }

    public void RemoveStatusFromKeepOnDeath(CharacterStatusEffect type)
    {
        if (preserveOnDeathStatuses == null)
            return;

        preserveOnDeathStatuses.Remove(type);
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

    public void OnPreCalculateCombatResult(CombatEntity? target, ref AttackRequest req)
    {
        if (statusEffects == null || onPreCalculateCombatResultEffects <= 0)
            return;

        Span<int> remove = stackalloc int[statusEffects.Count];
        var removeCount = 0;

        for (var i = 0; i < statusEffects.Count; i++)
        {
            var s = statusEffects[i];
            if (StatusEffectHandler.GetUpdateMode(s.Type).HasFlag(StatusUpdateMode.OnPreCalculateDamageDealt))
            {
                var res = StatusEffectHandler.OnPreCalculateDamage(s.Type, Owner, target, ref s, ref req);
                statusEffects[i] = s; //update any changed data
                if (res == StatusUpdateResult.EndStatus)
                {
                    remove[removeCount] = i;
                    removeCount++;
                }
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
                statusEffects[i] = s; //update any changed data
                if (res == StatusUpdateResult.EndStatus)
                {
                    remove[removeCount] = i;
                    removeCount++;
                }
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
                statusEffects[i] = s;
                if (res == StatusUpdateResult.EndStatus)
                {
                    remove[removeCount] = i;
                    removeCount++;
                }
            }
        }

        if (removeCount > 0)
            RemoveIdList(ref remove, removeCount);
    }

    public void OnCalculateDamageTaken(ref AttackRequest req, ref DamageInfo di)
    {
        Debug.Assert(Owner != null);
        if (statusEffects == null || onCalculateDamageTakenEffects <= 0)
            return;

        Span<int> remove = stackalloc int[statusEffects.Count];
        var removeCount = 0;

        for (var i = 0; i < statusEffects.Count; i++)
        {
            var s = statusEffects[i];
            if (StatusEffectHandler.GetUpdateMode(s.Type).HasFlag(StatusUpdateMode.OnCalculateDamageTaken))
            {
                var res = StatusEffectHandler.OnCalculateDamage(s.Type, Owner, ref s, ref req, ref di);
                statusEffects[i] = s;
                if (res == StatusUpdateResult.EndStatus)
                {
                    remove[removeCount] = i;
                    removeCount++;
                }
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
                statusEffects[i] = s;
                if (res == StatusUpdateResult.EndStatus)
                {
                    remove[removeCount] = i;
                    removeCount++;
                }
            }
        }

        if (removeCount > 0)
            RemoveIdList(ref remove, removeCount);
    }

    public void OnMove(Position src, Position dest, bool isTeleport)
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
                var res = StatusEffectHandler.OnMove(s.Type, Owner, ref s, src, dest, isTeleport);
                statusEffects[i] = s; //update any changed data
                if (res == StatusUpdateResult.EndStatus)
                {
                    remove[removeCount] = i;
                    removeCount++;
                }
            }
        }

        if (removeCount > 0)
            RemoveIdList(ref remove, removeCount);
    }

    public void OnChangeMaps()
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
                var res = StatusEffectHandler.OnChangeMaps(s.Type, Owner, ref s);
                statusEffects[i] = s;
                if (res == StatusUpdateResult.EndStatus)
                {
                    remove[removeCount] = i;
                    removeCount++;
                }
            }
        }

        if (removeCount > 0)
            RemoveIdList(ref remove, removeCount);
    }


    private bool CheckExpiredStatusEffects()
    {
        var hasUpdate = false;
        var nextExpiration = (double)float.MaxValue;
        for (var i = 0; i < statusEffects!.Count; i++)
        {
            var status = statusEffects[i];
            if (status.Expiration < Time.ElapsedTime)
            {
                hasUpdate = true;
                RemoveExistingStatusEffect(ref status);
                i--;
            }
            else if (status.Expiration < nextExpiration)
                nextExpiration = status.Expiration;
        }

        if (Time.ElapsedTime + 5f < nextExpiration)
            nextExpirationCheck = Time.ElapsedTime + 5f; //for safety's sake
        else
            nextExpirationCheck = nextExpiration;

        return hasUpdate;
    }

    public void UpdateStatusEffects()
    {
        Debug.Assert(Owner != null);

        var hasUpdate = false;
        var time = Time.ElapsedTime;
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
                if (pendingStatusEffects[i].EffectiveTime < Time.ElapsedTime)
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

    public void ExpireStatusEffectOfType(CharacterStatusEffect type)
    {
        Debug.Assert(Owner != null);
        if (statusEffects == null)
            return;

        for (var i = 0; i < statusEffects.Count; i++)
        {
            var status = statusEffects[i];
            if (status.Type == type)
            {
                status.Expiration = 0;
                statusEffects[i] = status;
                nextExpirationCheck = 0;
            }
        }
    }

    public bool RemoveStatusEffectOfType(CharacterStatusEffect type)
    {
        Debug.Assert(Owner != null);
        if (statusEffects == null)
            return false;

        var hasRemoved = false;
        for (var i = 0; i < statusEffects.Count; i++)
        {
            var status = statusEffects[i];
            if (status.Type == type)
            {
                RemoveExistingStatusEffect(ref status);
                hasRemoved = true;
                i--;
            }
        }

        return hasRemoved;
    }


    public bool RemoveStatusEffectOfGroup(string removeGroup)
    {
        Debug.Assert(Owner != null);
        if (statusEffects == null)
            return false;

        for (var i = 0; i < statusEffects.Count; i++)
        {
            var status = statusEffects[i];
            var group = StatusEffectHandler.GetShareGroup(status.Type);
            if (group == removeGroup)
            {
                RemoveExistingStatusEffect(ref status);
                return true; //we know only one status of a group can exist, so we can leave now
            }
        }

        return false;
    }


    public bool RemovePendingStatusEffectOfType(CharacterStatusEffect type)
    {
        Debug.Assert(Owner != null);
        if (pendingStatusEffects == null)
            return false;

        var hasRemoved = false;
        for (var i = 0; i < pendingStatusEffects.Count; i++)
        {
            var status = pendingStatusEffects[i];
            if (status.Effect.Type == type)
            {
                pendingStatusEffects.SwapFromBack(i);
                hasRemoved = true;
                i--;
            }
        }

        return hasRemoved;
    }

    private void RemoveUpdateModeForStatus(CharacterStatusEffect type)
    {
        var updateMode = StatusEffectHandler.GetUpdateMode(type);

        if ((updateMode & StatusUpdateMode.OnTakeDamage) > 0)
            onHitEffects--;
        if ((updateMode & StatusUpdateMode.OnDealDamage) > 0)
            onAttackEffects--;
        if ((updateMode & StatusUpdateMode.OnUpdate) > 0)
            onUpdateEffects--;
        if ((updateMode & StatusUpdateMode.OnChangeEquipment) > 0)
            onEquipmentChangeEffects--;
        if ((updateMode & StatusUpdateMode.OnMove) > 0)
            onMoveEffects--;
        if ((updateMode & StatusUpdateMode.OnCalculateDamageTaken) > 0)
            onCalculateDamageTakenEffects--;
        if ((updateMode & StatusUpdateMode.OnPreCalculateDamageDealt) > 0)
            onPreCalculateCombatResultEffects--;
    }

    private void AddUpdateModeForStatus(CharacterStatusEffect type)
    {
        var updateMode = StatusEffectHandler.GetUpdateMode(type);

        if ((updateMode & StatusUpdateMode.OnTakeDamage) > 0)
            onHitEffects++;
        if ((updateMode & StatusUpdateMode.OnDealDamage) > 0)
            onAttackEffects++;
        if ((updateMode & StatusUpdateMode.OnUpdate) > 0)
            onUpdateEffects++;
        if ((updateMode & StatusUpdateMode.OnChangeEquipment) > 0)
            onEquipmentChangeEffects++;
        if ((updateMode & StatusUpdateMode.OnMove) > 0)
            onMoveEffects++;
        if ((updateMode & StatusUpdateMode.OnCalculateDamageTaken) > 0)
            onCalculateDamageTakenEffects++;
        if ((updateMode & StatusUpdateMode.OnPreCalculateDamageDealt) > 0)
            onPreCalculateCombatResultEffects++;
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

        RemoveUpdateModeForStatus(status.Type);
    }
    
    private void RemoveExistingStatusEffect(ref StatusEffectState status, bool isRefresh = false)
    {
        Debug.Assert(Owner != null);

        if (StatusEffectHandler.GetStatusVisibility(status.Type) != StatusClientVisibility.None)
        {
            Debug.Assert(Character.Map != null);
            if (Character.Map != null)
            {
                Character.Map.AddVisiblePlayersAsPacketRecipients(Character);
                CommandBuilder.SendRemoveStatusEffect(Character, ref status, isRefresh);
                CommandBuilder.ClearRecipients();
            }
        }

        StatusEffectHandler.OnExpiration(status.Type, Owner, ref status);
        statusEffects!.Remove(ref status);

        RemoveUpdateModeForStatus(status.Type);
    }

    public void AddNewStatusEffect(StatusEffectState state, bool replaceExisting = true, bool isRestore = false)
    {
        Debug.Assert(Owner != null);

        if (statusEffects != null && TryGetExistingStatus(state.Type, out var oldEffect))
        {
            if (!replaceExisting)
                return; // do nothing
            RemoveExistingStatusEffect(ref oldEffect, oldEffect.Type == state.Type);
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

        AddUpdateModeForStatus(state.Type);
    }

    public void AddPendingStatusEffect(StatusEffectState state, bool replaceExisting, float time)
    {
        Debug.Assert(Owner != null);

        if (pendingStatusEffects == null)
            pendingStatusEffects = StatusEffectPoolManager.BorrowPendingContainer();

        var pending = new PendingStatusEffect()
        {
            Effect = state,
            EffectiveTime = Time.ElapsedTime + time,
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
            if (StatusEffectHandler.HasFlag(status.Type, StatusEffectFlags.StayOnClear) || (preserveOnDeathStatuses != null && preserveOnDeathStatuses.Contains(status.Type)))
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
                msg.Write((float)(statusEffects[i].Expiration - Time.ElapsedTime));
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
            if (StatusEffectHandler.HasFlag(status.Type, StatusEffectFlags.NoSave))
                continue;
            if(status.Type != CharacterStatusEffect.None && status.Expiration > Time.ElapsedTime)
                AddNewStatusEffect(status, true, true);
        }
    }
}