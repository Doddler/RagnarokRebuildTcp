using System.Collections.Generic;
using Assets.Scripts.Effects;
using Assets.Scripts.Effects.EffectHandlers.StatusEffects;
using Assets.Scripts.Misc;
using Assets.Scripts.Network;
using Assets.Scripts.Sprites;
using RebuildSharedData.Enum;
using UnityEngine;

namespace Assets.Scripts.PlayerControl
{
    public class StatusEffectState
    {
        private readonly List<CharacterStatusEffect> activeStatusEffects = new();

        public bool HasStatusEffect(CharacterStatusEffect status) => activeStatusEffects.Contains(status);
        
        public static void AddStatusToTarget(ServerControllable controllable, CharacterStatusEffect status)
        {
            if (controllable.StatusEffectState == null)
                controllable.StatusEffectState = new StatusEffectState();

            var state = controllable.StatusEffectState;
            
            if(!state.activeStatusEffects.Contains(status))
                state.activeStatusEffects.Add(status);
            
            Debug.Log($"Character {controllable.DisplayName} gained status {status}");
            
            switch (status)
            {
                case CharacterStatusEffect.PushCart:
                {
                    var go = new GameObject();
                    var cart = go.AddComponent<CartFollower>();
                    cart.AttachCart(controllable);
                    controllable.FollowerObject = go;
                    break;
                }
                case CharacterStatusEffect.Stun:
                    StunEffect.AttachStunEffect(controllable);
                    // controllable.SpriteAnimator.Color = new Color(1, 0.5f, 0.5f);
                    // if(controllable.SpriteAnimator.CurrentMotion is SpriteMotion.Idle or SpriteMotion.Standby)
                    //     controllable.SpriteAnimator.AnimSpeed = 2f;
                    break;
                case CharacterStatusEffect.TwoHandQuicken:
                    controllable.SpriteAnimator.Color = new Color(1, 1, 0.5f);
                    RoSpriteTrailManager.Instance.AttachTrailToEntity(controllable);
                    break;
            }
        }
        
        public static void RemoveStatusFromTarget(ServerControllable controllable, CharacterStatusEffect status)
        {
            controllable.StatusEffectState?.activeStatusEffects.Remove(status);
            
            Debug.Log($"Character {controllable.DisplayName} loses status {status}");

            switch (status)
            {
                case CharacterStatusEffect.PushCart:
                {
                    if(controllable.FollowerObject != null)
                        GameObject.Destroy(controllable.FollowerObject);
                    break;
                }
                case CharacterStatusEffect.Stun:
                    controllable.EndEffectOfType(EffectType.Stun);
                    // controllable.SpriteAnimator.Color = new Color(1, 1, 1);
                    // if(controllable.SpriteAnimator.CurrentMotion == SpriteMotion.Idle)
                    //     controllable.SpriteAnimator.AnimSpeed = 1;
                    break;
                case CharacterStatusEffect.TwoHandQuicken:
                    controllable.SpriteAnimator.Color = new Color(1, 1, 1f);
                    RoSpriteTrailManager.Instance.RemoveTrailFromEntity(controllable);
                    break;
            }
        }
    }
}