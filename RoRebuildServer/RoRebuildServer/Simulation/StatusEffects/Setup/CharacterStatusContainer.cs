using System.Diagnostics;
using System.Runtime.CompilerServices;
using RebuildSharedData.Enum;
using RebuildZoneServer.Networking;
using RoRebuildServer.EntityComponents;
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

    public void Reset()
    {
        Owner = null!;
        statusEffects?.Clear();
        pendingStatusEffects?.Clear();
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
        if (statusEffects != null)
        {
            for (var i = 0; i < statusEffects.Count; i++)
            {
                if (statusEffects[i].Type == type)
                {
                    outEffect = statusEffects[i];
                    return true;
                }
            }
        }

        outEffect = default;

        return false;
    }

    public void UpdateStatusEffects()
    {
        var hasUpdate = false;

        if (statusEffects != null)
        {
            for (var i = 0; i < statusEffects.Count; i++)
            {
                var status = statusEffects[i];
                if (status.Expiration < Time.ElapsedTimeFloat)
                {
                    hasUpdate = true;
                    RemoveExistingStatusEffect(ref status);
                    i--;
                }
            }
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
    }

    public void RemoveStatusEffectOfType(CharacterStatusEffect type)
    {
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

    private void RemoveExistingStatusEffect(ref StatusEffectState status)
    {
        if (StatusEffectHandler.GetStatusVisibility(status.Type) != StatusClientVisibility.None)
        {
            Debug.Assert(Character.Map != null);
            Character.Map.AddVisiblePlayersAsPacketRecipients(Character);
            CommandBuilder.SendRemoveStatusEffect(Character, ref status);
            CommandBuilder.ClearRecipients();
        }

        StatusEffectHandler.OnExpiration(status.Type, Owner, ref status);
        statusEffects!.Remove(ref status);
    }

    public void AddNewStatusEffect(StatusEffectState state, bool replaceExisting = true)
    {
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

        StatusEffectHandler.OnApply(state.Type, Owner, ref state);
        statusEffects ??= new SwapList<StatusEffectState>(5);
        statusEffects.Add(ref state);

        if (Character.Map != null && StatusEffectHandler.GetStatusVisibility(state.Type) != StatusClientVisibility.None)
        {
            Character.Map.AddVisiblePlayersAsPacketRecipients(Character);
            CommandBuilder.SendApplyStatusEffect(Character, ref state);
            CommandBuilder.ClearRecipients();
        }

        Owner.UpdateStats();
    }

    public void AddPendingStatusEffect(StatusEffectState state, bool replaceExisting, float time)
    {
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

}