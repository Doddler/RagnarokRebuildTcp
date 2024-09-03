using System.Diagnostics;
using RebuildSharedData.Enum;
using RebuildZoneServer.Networking;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Simulation.StatusEffects.Setup
{
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

            if (HasStatusEffectOfType(CharacterStatusEffect.Stun))
                replaceExisting = replaceExisting;

            StatusEffectHandler.OnApply(state.Type, Owner, ref state);
            statusEffects ??= new SwapList<StatusEffectState>(5);
            statusEffects.Add(ref state);

            if (StatusEffectHandler.GetStatusVisibility(state.Type) != StatusClientVisibility.None)
            {
                Debug.Assert(Character.Map != null);
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

        public void RemoveAll()
        {
            if (statusEffects == null)
                return;

            for (var i = 0; i < statusEffects.Count; i++)
            {
                var status = statusEffects[i];
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
}
