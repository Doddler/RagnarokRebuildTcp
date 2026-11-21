using Assets.Scripts.Effects.EffectHandlers.General;
using Assets.Scripts.Misc;
using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.Sprites;
using Assets.Scripts.Utility;
using MemoryPack;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using RebuildSharedData.Packets;
using System;
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

            //summons carry the summoner's cell, where their entry animation starts
            var hasStartCell = eventType == CreateEntityEventType.Toss || eventType == CreateEntityEventType.Descend;
            var startCell = hasStartCell ? msg.ReadPosition() : Vector2Int.zero;

            var control = SpawnEntity(msg, eventType);
            if (control == null)
                return;

            switch (eventType)
            {
                case CreateEntityEventType.Toss:
                {
                    var arc = control.SpriteAnimator.gameObject.AddComponent<ArcPathObject>();
                    var startPos = (startCell.ToWorldPosition() - control.transform.position);
                    var dist = Vector3.Distance(startPos, Vector3.zero);
                    arc.Init(control.transform, startPos, Vector3.zero, dist/2f, dist/10f);
                    break;
                }
                case CreateEntityEventType.Descend:
                {
                    var spr = control.SpriteAnimator.gameObject;
                    spr.transform.localPosition = new Vector3(0, 20f, 0);
                    var lt = LeanTween.moveLocalY(spr, 0, 0.5f);
                    lt.setEaseOutQuad();
                    break;
                }
                case CreateEntityEventType.EnterServer:
                case CreateEntityEventType.Warp:
                    if (!control.IsMainCharacter && !control.IsHidden)
                        EntryEffect.LaunchEntryAtLocation(control.transform.position,
                            control.CharacterType == CharacterType.Monster ? 0.3f : 0.7f);
                    break;
            }
        }

        private ServerControllable SpawnEntity(ClientInboundMessage msg, CreateEntityEventType eventType)
        {
            var entity = msg.MemoryPackDeserializeWithLength<EntitySpawnParameters>();
 
            //Debug.Log($"PacketCreateEntity2 {entity.ClassId}:{entity.Name}");

            var isMain = Network.PlayerId == entity.ServerId;
            if (isMain)
            {
                entity.IsMainCharacter = true;
                //entity.OverrideClassId = 6015;
                State.EntityId = entity.ServerId;
                State.IsValid = true;
                UiManager.Instance.SkillHotbar.LoadHotBarData(entity.Name);
            }
            
            //a refresh (look, job or mount change) for an entity already shown
            Network.EntityList.TryGetValue(entity.ServerId, out var oldEntity);

            ServerControllable controllable;
            switch (entity.Type)
            {
                case CharacterType.Player:
                {
                    var player = msg.MemoryPackDeserializeWithLength<PlayerSpawnParameters>();

                    if (eventType == CreateEntityEventType.Refresh && oldEntity != null
                        && EntityBuilder.Instance.TryRefreshPlayer(oldEntity, ref entity, ref player))
                    {
                        //updated in place to keep its statuses, movement and camera; a job change can change anyone's max hp
                        if (isMain)
                            UpdateMainCharacterState(oldEntity);
                        oldEntity.SetHp(entity.Hp, entity.MaxHp, false);
                        return oldEntity;
                    }

                    controllable = EntityBuilder.Instance.LoadPlayer(ref entity, ref player);
                    if (isMain)
                    {
                        //bound before the hp and statuses below, so they reach PlayerState
                        State.BindCharacter(controllable);
                        UpdateMainCharacterState(controllable);
                    }
                    break;
                }
                case CharacterType.PlayerLikeNpc:
                {
                    var player = msg.MemoryPackDeserializeWithLength<PlayerSpawnParameters>();
                    // var npc = msg.MemoryPackDeserializeWithLength<NpcSpawnParameters>();

                    controllable = EntityBuilder.Instance.LoadPlayer(ref entity, ref player);

                    //Debug.LogError($"No handler for creating PlayerLikeNpc!!");
                    break;
                }
                case CharacterType.Monster:
                    controllable = EntityBuilder.Instance.LoadMonster(ref entity);
                    break;
                case CharacterType.NPC:
                case CharacterType.BattleNpc:
                {
                    var npc = msg.MemoryPackDeserializeWithLength<NpcSpawnParameters>();
                    controllable = EntityBuilder.Instance.LoadNpc(ref entity, ref npc);
                    break;
                }
                default:
                    controllable = null;
                    break;
            }

            //replaces the shown object; AddEntity hands its selection and links to the new one
            if (oldEntity != null)
                oldEntity.FadeOutAndVanish(0.1f);

            if (controllable == null)
            {
                Network.RemoveEntity(entity.ServerId);
                Debug.LogError($"SpawnEntity failed to create a valid server controllable (Type:{entity.Type} ClassId:{entity.ClassId} Name:{entity.Name})");
                return null;
            }

            //the builders copied the hp; this tells PlayerState and shows the player's own bar
            if (controllable.IsMainCharacter)
                controllable.SetHp(entity.Hp, entity.MaxHp);

            if (entity.Type != CharacterType.NPC && entity.Type != CharacterType.BattleNpc)
                controllable.IsInteractable = true;

            Network.AddEntity(controllable);

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
            
            if (isMain)
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

        private void UpdateMainCharacterState(ServerControllable controllable)
        {
            controllable.IsHidden = State.IsAdminHidden;
        }
    }
}
