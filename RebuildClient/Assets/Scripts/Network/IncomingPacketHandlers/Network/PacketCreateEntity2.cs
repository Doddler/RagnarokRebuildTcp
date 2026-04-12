using Assets.Scripts.Effects.EffectHandlers.General;
using Assets.Scripts.Misc;
using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.Sprites;
using Assets.Scripts.UI.Hud;
using Assets.Scripts.Utility;
using MemoryPack;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using RebuildSharedData.Packets;
using System;
using System.Collections.Generic;
using UnityEngine;
using PlayerSpawnParameters = RebuildSharedData.Packets.PlayerSpawnParameters;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Network
{
    [ClientPacketHandler(PacketType.CreateEntity2)]
    public class PacketCreateEntity2 : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var eventType = (CreateEntityEventType)msg.ReadByte();
            var startCell = Vector2Int.zero;

            if (eventType == CreateEntityEventType.Toss)
                startCell = msg.ReadPosition();
            
            var control = SpawnEntity(msg, eventType);
            
            if (eventType == CreateEntityEventType.Toss)
            {
                var arc = control.SpriteAnimator.gameObject.AddComponent<ArcPathObject>();
                var startPos = (startCell.ToWorldPosition() - control.transform.position);
                var dist = Vector3.Distance(startPos, Vector3.zero);
                arc.Init(control.transform, startPos, Vector3.zero, dist/2f, dist/10f);
            }

            if (eventType == CreateEntityEventType.Descend)
            {
                var spr = control.SpriteAnimator.gameObject;
                spr.transform.localPosition = new Vector3(0, 20f, 0);
                var lt = LeanTween.moveLocalY(spr, 0, 0.5f);
                lt.setEaseOutQuad();
            }

            if (eventType == CreateEntityEventType.EnterServer || eventType == CreateEntityEventType.Warp)
            {
                if (control.IsMainCharacter || control.IsHidden)
                    return;
                
                EntryEffect.LaunchEntryAtLocation(control.transform.position, control.CharacterType == CharacterType.Monster ? 0.3f : 0.7f);
            }
        }

        private ServerControllable SpawnEntity(ClientInboundMessage msg, CreateEntityEventType eventType)
        {
            var entity = msg.MemoryPackDeserializeWithLength<EntitySpawnParameters>();
 
            //Debug.Log($"PacketCreateEntity2 {entity.ClassId}:{entity.Name}");
            
            ServerControllable controllable = null;
            
            var isMain = Network.PlayerId == entity.ServerId;
            if (isMain)
            {
                entity.IsMainCharacter = true;
                //entity.OverrideClassId = 6015;
                State.EntityId = entity.ServerId;
                State.PlayerName = entity.Name;
                State.IsValid = true;
                UiManager.Instance.SkillHotbar.LoadHotBarData(entity.Name);
            }
            
            if (Network.EntityList.TryGetValue(entity.ServerId, out var oldEntity))
            {
                //if for some reason we try to spawn an entity that already exists, we kill the old one.
                oldEntity.FadeOutAndVanish(0.1f);
                Network.EntityList.Remove(entity.ServerId);
            }

            if (entity.Type == CharacterType.Player)
            {
                var player = msg.MemoryPackDeserializeWithLength<PlayerSpawnParameters>();

                controllable = EntityBuilder.Instance.LoadPlayer(ref entity, ref player);
                
                if (entity.ServerId == Network.PlayerId)
                {
                    State.Level = entity.Level;

                    var max = CameraFollower.Instance.ExpForLevel(entity.Level);
                    Camera.UpdatePlayerExp(State.Exp, max);
                    controllable.IsHidden = State.IsAdminHidden;
                    controllable.IsMainCharacter = true;
                    State.JobId = entity.ClassId;
                    State.IsMale = player.IsMale;
                    State.HairStyleId = player.HeadType;
                    State.HairColorId = player.HairColor;
                    State.HasCart = (player.Follower & CharacterFollowerState.AnyCart) > 0;
                    State.HasBird = (player.Follower & CharacterFollowerState.Falcon) > 0;
                    State.WeaponClass = player.WeaponClass;
                    
                    UiManager.Instance.SkillManager.UpdateAvailableSkills();
                    UiManager.Instance.EquipmentWindow.UpdateCharacterDisplay(player.Headgear1, player.Headgear2, player.Headgear3);
                    UiManager.Instance.EquipmentWindow.RefreshEquipmentWindow(); //follower state might have changed
                    UiManager.Instance.StatusWindow.UpdateCharacterStats();

                    if (entity.CharacterStatusEffects == null)
                        entity.CharacterStatusEffects = new Dictionary<CharacterStatusEffect, float>();
                    
                    foreach(var (status, duration) in entity.CharacterStatusEffects)
                        StatusEffectPanel.Instance.AddStatusEffect(status, duration);
                }
            }

            if (entity.Type == CharacterType.Monster)
            {
                controllable = EntityBuilder.Instance.LoadMonster(ref entity);
            }

            if (entity.Type == CharacterType.NPC || entity.Type == CharacterType.BattleNpc)
            {
                var npc = msg.MemoryPackDeserializeWithLength<NpcSpawnParameters>();

                controllable = EntityBuilder.Instance.LoadNpc(ref entity, ref npc);

            }

            if (entity.Type == CharacterType.PlayerLikeNpc)
            {
                var player = msg.MemoryPackDeserializeWithLength<PlayerSpawnParameters>();
                // var npc = msg.MemoryPackDeserializeWithLength<NpcSpawnParameters>();
                
                controllable = EntityBuilder.Instance.LoadPlayer(ref entity, ref player);
                
                Debug.LogError($"No handler for creating PlayerLikeNpc!!");
            }

            if (controllable == null)
            {
                Debug.LogError($"SpawnEntity failed to create a valid server controllable (Type:{entity.Type} ClassId:{entity.ClassId} Name:{entity.Name})");
                return null;
            }
            
            controllable.EnsureFloatingDisplayCreated().SetUp(controllable, controllable.Name, entity.MaxHp, entity.MaxSp, entity.Type == CharacterType.Player, controllable.IsMainCharacter);
            if (controllable.IsMainCharacter)
            {
                Camera.UpdatePlayerHP(entity.Hp, entity.MaxHp);
                controllable.SetHp(entity.Hp, entity.MaxHp);
                //CameraFollower.UpdatePlayerSP(100, 100);
                
                if(State.Sp > 0 && State.MaxSp > 0)
                    controllable.SetSp(State.Sp, State.MaxSp);
            }

            if (entity.MaxSp > 0)
            {
                if(controllable.IsMainCharacter)
                    Camera.UpdatePlayerSP(entity.Sp, entity.MaxSp);
                controllable.SetSp(entity.Sp, entity.MaxSp);
            }
            
            if (entity.Type != CharacterType.NPC && entity.Type != CharacterType.BattleNpc)
                controllable.IsInteractable = true;
            
            Network.EntityList.Add(entity.ServerId, controllable);
            
            if (entity.CharacterStatusEffects != null)
                foreach (var (s, duration) in entity.CharacterStatusEffects)
                    StatusEffectState.AddStatusToTarget(controllable, s, true, duration);

            //load walk data
            
            try
            {
                if (entity.State == CharacterState.Moving)
                    Network.LoadMoveData2(msg, controllable);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load move data for type {entity.Type} id {entity.ClassId}: {controllable.Name}");
                Debug.LogException(e);
            }
            
            if (State.EntityId == controllable.Id)
            {
                Camera.Target = controllable.gameObject;
                //Debug.Log($"Player entity sent, we're at position {pos}");

                if (eventType != CreateEntityEventType.Refresh)
                {
                    if (!CameraFollower.Instance.CinemachineMode)
                        SceneTransitioner.Instance.FadeIn();
                    CameraFollower.Instance.SnapLookAt();
                }
            }

            return controllable;
        }
    }
}