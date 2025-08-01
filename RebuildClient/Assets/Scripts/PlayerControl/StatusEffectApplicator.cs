using System;
using System.Collections.Generic;
using Assets.Scripts.Effects;
using Assets.Scripts.Effects.EffectHandlers.General;
using Assets.Scripts.Effects.EffectHandlers.Skills;
using Assets.Scripts.Effects.EffectHandlers.Skills.Custom;
using Assets.Scripts.Effects.EffectHandlers.StatusEffects;
using Assets.Scripts.Effects.PrimitiveData;
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
        //private readonly List<CharacterStatusEffect> activeStatusEffects = new();
        private readonly Dictionary<CharacterStatusEffect, float> activeStatusEffects = new();

        public static Color StoneColor = new Color(0.8f, 0.8f, 0.8f);

        public bool HasStatusEffect(CharacterStatusEffect status) => activeStatusEffects.ContainsKey(status);
        public Dictionary<CharacterStatusEffect, float> GetStatusEffects() => activeStatusEffects;

        private static void UpdateColorForStatus(ServerControllable src)
        {
            var color = Color.white;
            var priority = -1;
            
            foreach (var (s, _) in src.StatusEffectState.activeStatusEffects)
            {
                switch (s)
                {
                    //skill colors:
                    //overthrust - 250/150/150
                    //twohandquicken - 200/200/0
                    //spearquicken - 200/200/0
                    //energycoat - 180/180/250
                    
                    case CharacterStatusEffect.Bulwark:
                    case CharacterStatusEffect.EnergyCoat:
                        if(priority < 0)
                            color = new Color(0.7f, 0.7f, 0.98f);
                        priority = 0;
                        break;
                    case CharacterStatusEffect.TwoHandQuicken:
                        if(priority < 1)
                            color = new Color(1, 1, 0.7f);
                        priority = 1;
                        break;
                    case CharacterStatusEffect.Poison:
                        if(priority < 2)
                            color = new Color(1f, 0.7f, 1f);
                        priority = 2;
                        break;
                    case CharacterStatusEffect.Curse:
                        if(priority < 3)
                            color = new Color(0.5f, 0f, 0f);
                        priority = 3;
                        break;
                    case CharacterStatusEffect.Frozen:
                        if(priority < 4)
                            color = new Color(0f, 0.5f, 1f);
                        priority = 4;
                        break;
                    case CharacterStatusEffect.Stone:
                        if (priority < 5)
                            color = StoneColor;
                        priority = 5;
                        break;
                }
            }
            
            src.SpriteAnimator.Color = color;
        }
        
        public static void AddStatusToTarget(ServerControllable controllable, CharacterStatusEffect status, bool isNewEntity, float duration = 0)
        {
            if (controllable.StatusEffectState == null)
                controllable.StatusEffectState = new StatusEffectState();

            var state = controllable.StatusEffectState;

            state.activeStatusEffects[status] = duration + Time.timeSinceLevelLoad;

            UpdateColorForStatus(controllable);
            
            Debug.Log($"Character {controllable.DisplayName} gained status {status}");

            if (controllable.CharacterType == CharacterType.Player && PlayerState.Instance.IsInParty &&
                PlayerState.Instance.PartyMemberIdLookup.TryGetValue(controllable.Id, out var partyMemberId))
            {
                if(UiManager.Instance.PartyPanel.PartyEntryLookup.TryGetValue(partyMemberId, out var panel))
                    panel.AddStatusEffect(status, duration);
            }
            
            switch (status)
            {
                case CharacterStatusEffect.PushCart:
                {
                    var go = new GameObject();
                    var cart = go.AddComponent<CartFollower>();
                    cart.AttachCart(controllable, 0);
                    controllable.FollowerObject = go;
                    break;
                }
                case CharacterStatusEffect.Hiding:
                case CharacterStatusEffect.Cloaking:
                case CharacterStatusEffect.Invisible:
                    controllable.SpriteAnimator.IsHidden = true;
                    controllable.SpriteAnimator.HideShadow = !controllable.IsMainCharacter;
                    if(controllable.FollowerObject != null)
                        controllable.FollowerObject.SetActive(false); //hide cart, bird, whatever is following the player
                    if (controllable.CharacterType != CharacterType.Player)
                        controllable.HideHpBar();
                    if (controllable.CharacterType == CharacterType.Player && !controllable.IsMainCharacter)
                    {
                        if(!PlayerState.Instance.IsInParty || PlayerState.Instance.PartyName != controllable.PartyName)
                            controllable.HideHpBar();
                    }
                    if (CameraFollower.Instance.SelectedTarget == controllable)
                        CameraFollower.Instance.ClearSelected();
                    break;
                case CharacterStatusEffect.Stun:
                    StunEffect.AttachStunEffect(controllable);
                    break;
                case CharacterStatusEffect.Sleep:
                    SleepEffect.AttachSleepEffect(controllable);
                    break;
                case CharacterStatusEffect.TwoHandQuicken:
                    //color now set via UpdateColorForStatus
                    RoSpriteTrailManager.Instance.AttachTrailToEntity(controllable);
                    break;
                case CharacterStatusEffect.EnergyCoat:
                    //color now set via UpdateColorForStatus
                    RoSpriteTrailManager.Instance.AttachTrailToEntity(controllable);
                    break;
                case CharacterStatusEffect.Poison:
                    //color now set via UpdateColorForStatus
                    if(!isNewEntity)
                        AudioManager.Instance.AttachSoundToEntity(controllable.Id, "_poison.ogg", controllable.gameObject);
                    break;
                case CharacterStatusEffect.Silence:
                    controllable.AttachEffect(SilenceEffect.LaunchSilenceEffect(controllable, 999));
                    if(!isNewEntity)
                        AudioManager.Instance.AttachSoundToEntity(controllable.Id, "_silence.ogg", controllable.gameObject);
                    break;
                case CharacterStatusEffect.Frozen:
                    //color now set via UpdateColorForStatus
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
                        //we can tell if blind was previously active if BlindStrength is low enough, we use this to stop the sound from playing when you teleport
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
                case CharacterStatusEffect.Sight:
                    if(!isNewEntity)
                        AudioManager.Instance.OneShotSoundEffect(controllable.Id, $"ef_sight.ogg", controllable.transform.position);
                    controllable.AttachEffect(SightEffect.LaunchSight(controllable));
                    break;
                case CharacterStatusEffect.Ruwach:
                    if(!isNewEntity)
                        AudioManager.Instance.OneShotSoundEffect(controllable.Id, $"ef_sight.ogg", controllable.transform.position);
                    controllable.AttachEffect(RuwachEffect.LaunchRuwach(controllable));
                    break;
                case CharacterStatusEffect.Petrifying:
                    controllable.AttachEffect(PetrifyingEffect.LaunchPetrifyingEffect(controllable, duration));
                    break;
                case CharacterStatusEffect.Stone:
                    controllable.AbortActiveWalk();
                    controllable.SpriteAnimator?.PauseAnimation();
                    controllable.AttachEffect(PetrifyingEffect.LaunchPetrifyingEffect(controllable, 0));
                    break;
                case CharacterStatusEffect.Smoking:
                    if (controllable.ClassId == 4116 && controllable.SpriteAnimator != null) //hardcoded to orc warrior...
                    {
                        controllable.SpriteAnimator.ChangeMotion(SpriteMotion.Performance3);
                        controllable.SpriteAnimator.ForceLoop = true;
                    }
                    break;
                case CharacterStatusEffect.Stop:
                    controllable.AttachEffect(RoSpriteEffect.AttachSprite(controllable, "Assets/Sprites/Effects/스톱.spr", 0.65f));
                    break;
                case CharacterStatusEffect.SpecialTarget:
                    var effect = SpecialTargetMarkerEffect.Create(controllable, duration);
                    controllable.AttachEffect(effect);
                    break;
            }
        }

        public static void RemoveStatusFromTarget(ServerControllable controllable, CharacterStatusEffect status)
        {
            controllable.StatusEffectState?.activeStatusEffects.Remove(status);
            
            UpdateColorForStatus(controllable);

            Debug.Log($"Character {controllable.DisplayName} loses status {status}");
            
            if (controllable.CharacterType == CharacterType.Player && PlayerState.Instance.IsInParty &&
                PlayerState.Instance.PartyMemberIdLookup.TryGetValue(controllable.Id, out var partyMemberId))
            {
                if(UiManager.Instance.PartyPanel.PartyEntryLookup.TryGetValue(partyMemberId, out var panel))
                    panel.RemoveStatusEffect(status);
            }

            switch (status)
            {
                case CharacterStatusEffect.PushCart:
                {
                    if (controllable.FollowerObject != null)
                        GameObject.Destroy(controllable.FollowerObject);
                    break;
                }
                case CharacterStatusEffect.Hiding:
                case CharacterStatusEffect.Cloaking:
                case CharacterStatusEffect.Invisible:
                    controllable.SpriteAnimator.IsHidden = false;
                    controllable.SpriteAnimator.HideShadow = false;
                    if(controllable.FollowerObject != null)
                        controllable.FollowerObject.SetActive(true); //unhide cart/bird
                    HideEffect.AttachHideEffect(controllable.gameObject); //smoke plays when unhiding too
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
                case CharacterStatusEffect.Silence:
                    controllable.EndEffectOfType(EffectType.Silence);
                    break;
                case CharacterStatusEffect.Stone:
                    AudioManager.Instance.AttachSoundToEntity(controllable.Id, "_stone_explosion.ogg", CameraFollower.Instance.ListenerProbe, 0.8f);
                    controllable.EndEffectOfType(EffectType.Petrifying);
                    break;
                case CharacterStatusEffect.TwoHandQuicken:
                    RoSpriteTrailManager.Instance.RemoveTrailFromEntity(controllable);
                    break;
                case CharacterStatusEffect.EnergyCoat:
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
                case CharacterStatusEffect.Sight:
                    var effect = controllable.DetachExistingEffectOfType(EffectType.Sight);
                    effect.SetRemainingDurationByFrames(20);
                    effect.Flags[0] = 1;
                    break;
                case CharacterStatusEffect.Ruwach:
                    var rEffect = controllable.DetachExistingEffectOfType(EffectType.Ruwach);
                    rEffect.SetRemainingDurationByFrames(20);
                    rEffect.Flags[0] = 1;
                    break;
                case CharacterStatusEffect.Smoking:
                    if (controllable.SpriteAnimator != null)
                    {
                        controllable.SpriteAnimator.ForceLoop = false;
                        var cm = controllable.SpriteAnimator.CurrentMotion;
                        if(cm != SpriteMotion.Walk && cm != SpriteMotion.Dead)
                            controllable.SpriteAnimator.ChangeMotion(SpriteMotion.Idle, true);
                    }

                    break;
                case CharacterStatusEffect.Petrifying:
                    controllable.EndEffectOfType(EffectType.Petrifying);
                    break;
                case CharacterStatusEffect.Stop:
                    controllable.EndEffectOfType(EffectType.RoSprite, "Assets/Sprites/Effects/스톱.spr");
                    break;
                case CharacterStatusEffect.SpecialTarget:
                    var targeter = controllable.DetachExistingEffectOfType(EffectType.SpecialTargetMarker);
                    if(targeter != null)
                        ((SpecialMarkerData)targeter.EffectData).IsEnding = true;
                    var cast = controllable.DetachExistingEffectOfType(EffectType.CastLockOn);
                    cast?.SetRemainingDurationByFrames(30);
                    break;
            }
        }
    }
}