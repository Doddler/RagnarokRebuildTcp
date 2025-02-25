using System;
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
            var priority = -1;
            
            foreach (var s in src.StatusEffectState.activeStatusEffects)
            {
                switch (s)
                {
                    case CharacterStatusEffect.TwoHandQuicken:
                        if(priority < 0)
                            color = new Color(1, 1, 0.7f);
                        priority = 0;
                        break;
                    case CharacterStatusEffect.Poison:
                        if(priority < 1)
                            color = new Color(1f, 0.7f, 1f);
                        priority = 1;
                        break;
                    case CharacterStatusEffect.Curse:
                        if(priority < 2)
                            color = new Color(0.5f, 0f, 0f);
                        priority = 2;
                        break;
                    case CharacterStatusEffect.Frozen:
                        if(priority < 3)
                            color = new Color(0f, 0.5f, 1f);
                        priority = 3;
                        break;
                }
            }
            
            src.SpriteAnimator.Color = color;
        }
        

        public static void AddStatusToTarget(ServerControllable controllable, CharacterStatusEffect status, bool isNewEntity)
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
                case CharacterStatusEffect.Sleep:
                    SleepEffect.AttachSleepEffect(controllable);
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
                    FreezeEffect.AttachFreezeEffect(controllable);
                    if(!isNewEntity)
                        AudioManager.Instance.OneShotSoundEffect(controllable.Id, "_stonecurse.ogg", controllable.transform.position, 0.8f);
                    break;
                case CharacterStatusEffect.Curse:
                    AudioManager.Instance.AttachSoundToEntity(controllable.Id, "_curse.ogg", controllable.gameObject);
                    CurseEffect.AttachCurseEffect(controllable);
                    break;
                case CharacterStatusEffect.PowerUp:
                    var powerUp = ExplosiveAuraEffect.AttachExplosiveAura(controllable.gameObject, 2, new Color(1f, 20/255f, 20/255f));
                    controllable.AttachEffect(powerUp);
                    break;
                case CharacterStatusEffect.Blind:
                    if (controllable.IsMainCharacter)
                    {
                        Shader.EnableKeyword("BLINDEFFECT_ON");
                        if (CameraFollower.Instance.BlindStrength > 100)
                        {
                            CameraFollower.Instance.BlindStrength = 200f;
                            AudioManager.Instance.AttachSoundToEntity(controllable.Id, "_blind.ogg", CameraFollower.Instance.ListenerProbe, 1.2f);
                        }

                        CameraFollower.Instance.IsBlindActive = true;
                    }
                    else
                    {
                        if(!isNewEntity)
                            AudioManager.Instance.AttachSoundToEntity(controllable.Id, "_blind.ogg", CameraFollower.Instance.ListenerProbe, 0.8f); //quieter
                    }

                    break;
                case CharacterStatusEffect.Hallucination:
                    CameraFollower.Instance.GetComponent<ScreenEffectHandler>().StartHallucination();
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
                case CharacterStatusEffect.Sleep:
                    controllable.EndEffectOfType(EffectType.Sleep);
                    break;
                case CharacterStatusEffect.Curse:
                    controllable.EndEffectOfType(EffectType.Curse);
                    break;
                case CharacterStatusEffect.Stone:
                    AudioManager.Instance.AttachSoundToEntity(controllable.Id, "_stone_explosion.ogg", CameraFollower.Instance.ListenerProbe, 0.8f);
                    break;
                case CharacterStatusEffect.TwoHandQuicken:
                    RoSpriteTrailManager.Instance.RemoveTrailFromEntity(controllable);
                    break;
                case CharacterStatusEffect.Frozen:
                    controllable.SpriteAnimator.Unpause();
                    var freeze = controllable.GetExistingEffectOfType(EffectType.Freeze);
                    if(freeze != null)
                        freeze.EffectHandler.OnEvent(freeze, null);
                    break;
                case CharacterStatusEffect.PowerUp:
                    controllable.EndEffectOfType(EffectType.ExplosiveAura);
                    break;
                case CharacterStatusEffect.Blind:
                    if(controllable.IsMainCharacter)
                        CameraFollower.Instance.IsBlindActive = false;
                    break;
                case CharacterStatusEffect.Hallucination:
                    CameraFollower.Instance.GetComponent<ScreenEffectHandler>().EndHallucination();
                    break;
            }
        }
    }
}