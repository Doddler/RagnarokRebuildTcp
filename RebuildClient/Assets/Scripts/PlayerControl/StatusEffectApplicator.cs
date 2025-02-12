using System.Collections.Generic;
using Assets.Scripts.Effects;
using Assets.Scripts.Effects.EffectHandlers.General;
using Assets.Scripts.Effects.EffectHandlers.StatusEffects;
using Assets.Scripts.Misc;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using Assets.Scripts.Sprites;
using RebuildSharedData.Enum;
using UnityEngine;

namespace Assets.Scripts.PlayerControl
{
    public class StatusEffectState
    {
        private readonly List<CharacterStatusEffect> activeStatusEffects = new();

        public bool HasStatusEffect(CharacterStatusEffect status) => activeStatusEffects.Contains(status);

        private static void UpdateColorForStatus(ServerControllable src)
        {
            var color = Color.white;
            
            foreach (var s in src.StatusEffectState.activeStatusEffects)
            {
                switch (s)
                {
                    case CharacterStatusEffect.TwoHandQuicken:
                        color = new Color(1, 1, 0.7f);
                        break;
                    case CharacterStatusEffect.Poison:
                        color = new Color(1f, 0.7f, 1f);
                        break;
                    case CharacterStatusEffect.Frozen:
                        color = new Color(0.3f, 0.7f, 1f);
                        break;
                }
            }
            
            src.SpriteAnimator.Color = color;
        }
        

        public static void AddStatusToTarget(ServerControllable controllable, CharacterStatusEffect status)
        {
            if (controllable.StatusEffectState == null)
                controllable.StatusEffectState = new StatusEffectState();

            var state = controllable.StatusEffectState;

            if (!state.activeStatusEffects.Contains(status))
                state.activeStatusEffects.Add(status);

            UpdateColorForStatus(controllable);
            
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
                case CharacterStatusEffect.Hiding:
                    controllable.SpriteAnimator.IsHidden = true;
                    controllable.SpriteAnimator.HideShadow = controllable.IsMainCharacter;
                    if (controllable.CharacterType != CharacterType.Player)
                        controllable.HideHpBar();
                    if (CameraFollower.Instance.SelectedTarget == controllable)
                        CameraFollower.Instance.ClearSelected();
                    break;
                case CharacterStatusEffect.Stun:
                    StunEffect.AttachStunEffect(controllable);
                    // controllable.SpriteAnimator.Color = new Color(1, 0.5f, 0.5f);
                    // if(controllable.SpriteAnimator.CurrentMotion is SpriteMotion.Idle or SpriteMotion.Standby)
                    //     controllable.SpriteAnimator.AnimSpeed = 2f;
                    break;
                case CharacterStatusEffect.TwoHandQuicken:
                    // controllable.SpriteAnimator.Color = new Color(1, 1, 0.7f);
                    RoSpriteTrailManager.Instance.AttachTrailToEntity(controllable);
                    break;
                case CharacterStatusEffect.Poison:
                    // controllable.SpriteAnimator.Color = new Color(1f, 0.7f, 1f);
                    AudioManager.Instance.AttachSoundToEntity(controllable.Id, "ef_poisonattack.ogg", controllable.gameObject);
                    break;
                case CharacterStatusEffect.Frozen:
                    // controllable.SpriteAnimator.Color = new Color(0.3f, 0.7f, 1f);
                    controllable.AbortActiveWalk();
                    controllable.SpriteAnimator?.PauseAnimation();
                    break;
                case CharacterStatusEffect.PowerUp:
                    var powerUp = ExplosiveAuraEffect.AttachExplosiveAura(controllable.gameObject, 2, new Color(1f, 20/255f, 20/255f));
                    controllable.AttachEffect(powerUp);
                    break;
            }
        }

        public static void RemoveStatusFromTarget(ServerControllable controllable, CharacterStatusEffect status)
        {
            controllable.StatusEffectState?.activeStatusEffects.Remove(status);
            
            UpdateColorForStatus(controllable);

            Debug.Log($"Character {controllable.DisplayName} loses status {status}");

            switch (status)
            {
                case CharacterStatusEffect.PushCart:
                {
                    if (controllable.FollowerObject != null)
                        GameObject.Destroy(controllable.FollowerObject);
                    break;
                }
                case CharacterStatusEffect.Hiding:
                    controllable.SpriteAnimator.IsHidden = false;
                    controllable.SpriteAnimator.HideShadow = false;
                    HideEffect.AttachHideEffect(controllable.gameObject);
                    break;
                case CharacterStatusEffect.Stun:
                    controllable.EndEffectOfType(EffectType.Stun);
                    break;
                case CharacterStatusEffect.TwoHandQuicken:
                    RoSpriteTrailManager.Instance.RemoveTrailFromEntity(controllable);
                    break;
                case CharacterStatusEffect.Frozen:
                    controllable.SpriteAnimator.Unpause();
                    break;
                case CharacterStatusEffect.PowerUp:
                    controllable.EndEffectOfType(EffectType.ExplosiveAura);
                    break;
            }
        }
    }
}