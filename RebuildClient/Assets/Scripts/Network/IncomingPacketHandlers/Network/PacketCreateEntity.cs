using System.Collections.Generic;
using Assets.Scripts.Effects;
using Assets.Scripts.Misc;
using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.Sprites;
using Assets.Scripts.Utility;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using UnityEngine;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Network
{
    [ClientPacketHandler(PacketType.CreateEntity)]
    public class PacketCreateEntity : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var control = SpawnEntity(msg);
            var eventType = (CreateEntityEventType)msg.ReadByte();
            if (eventType == CreateEntityEventType.Toss)
            {
                var startCell = msg.ReadPosition();
                var arc = control.SpriteAnimator.gameObject.AddComponent<ArcPathObject>();
                var startPos = (startCell.ToWorldPosition() - control.transform.position);
                var dist = Vector3.Distance(startPos, Vector3.zero);
                arc.Init(control.transform, startPos, Vector3.zero, dist/2f, dist/10f);
            }
        }
        
        private ServerControllable SpawnEntity(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();
            var type = (CharacterType)msg.ReadByte();
            var classId = msg.ReadInt16();
            var pos = msg.ReadPosition();
            var facing = (Direction)msg.ReadByte();
            var state = (CharacterState)msg.ReadByte();

            var lvl = -1;
            var maxHp = 0;
            var hp = 0;
            List<CharacterStatusEffect> statuses = null; 

            if (type == CharacterType.Player || type == CharacterType.Monster)
            {
                lvl = (int)msg.ReadByte();
                maxHp = (int)msg.ReadInt32();
                hp = (int)msg.ReadInt32();

                while(msg.ReadBoolean()) //has status effects
                {
                    statuses = new List<CharacterStatusEffect>();
                    statuses.Add((CharacterStatusEffect)msg.ReadByte());
                    msg.ReadFloat(); //duration, we're ignoring this for now
                }
            }

            if (Network.EntityList.TryGetValue(id, out var oldEntity))
            {
                //if for some reason we try to spawn an entity that already exists, we kill the old one.
                oldEntity.FadeOutAndVanish(0.1f);
                Network.EntityList.Remove(id);
            }

            ServerControllable controllable;
            if (type == CharacterType.Player)
            {
                var headFacing = (HeadFacing)msg.ReadByte();
                var headId = msg.ReadByte();
                var hairDyeId = msg.ReadByte();
                var weapon = msg.ReadByte();
                var isMale = msg.ReadBoolean();
                var name = msg.ReadString();
                var isMain = Network.PlayerId == id;
                if (isMain)
                    State.EntityId = id;

                Debug.Log("Name: " + name);

                var playerData = new PlayerSpawnParameters()
                {
                    ServerId = id,
                    ClassId = classId,
                    Facing = facing,
                    Position = pos,
                    State = state,
                    HeadFacing = headFacing,
                    HeadId = headId,
                    HairDyeId = hairDyeId,
                    IsMale = isMale,
                    Name = name,
                    Level = lvl,
                    MaxHp = maxHp,
                    Hp = hp,
                    WeaponClass = weapon,
                    IsMainCharacter = isMain,
                    CharacterStatusEffects = statuses,
                };

                controllable = ClientDataLoader.Instance.InstantiatePlayer(ref playerData);

                if (id == Network.PlayerId)
                {
                    State.Level = lvl;

                    var max = CameraFollower.Instance.ExpForLevel(controllable.Level);
                    Camera.UpdatePlayerExp(State.Exp, max);
                    controllable.IsHidden = State.IsAdminHidden;
                    State.JobId = classId;
                    UiManager.Instance.SkillManager.UpdateAvailableSkills();
                }
            }
            else
            {
                var interactable = false;
                var name = string.Empty;
                var displayType = NpcDisplayType.Sprite;
                var effectType = NpcEffectType.None;
                
                if (type == CharacterType.NPC)
                {
                    name = msg.ReadString();
                    interactable = msg.ReadBoolean();
                    displayType = (NpcDisplayType)msg.ReadByte();
                    effectType = (NpcEffectType)msg.ReadByte();
                    //Debug.Log(name);
                }

                var monData = new MonsterSpawnParameters()
                {
                    ServerId = id,
                    ClassId = classId,
                    Name = name,
                    Facing = facing,
                    Position = pos,
                    State = state,
                    Level = lvl,
                    MaxHp = maxHp,
                    Hp = hp,
                    Interactable = interactable,
                    CharacterStatusEffects = statuses,
                };

                if (displayType == NpcDisplayType.Effect)
                {
                    
                    controllable = ClientDataLoader.Instance.InstantiateEffect(ref monData, effectType);
                    Network.EntityList.Add(id, controllable);
                    return controllable;
                }

                controllable = ClientDataLoader.Instance.InstantiateMonster(ref monData, type);    
            }

            controllable.EnsureFloatingDisplayCreated().SetUp(controllable.Name, maxHp, hp, type == CharacterType.Player, controllable.IsMainCharacter);
            if (controllable.IsMainCharacter)
            {
                Camera.UpdatePlayerHP(hp, maxHp);
                //CameraFollower.UpdatePlayerSP(100, 100);
            }

            controllable.SetHp(hp);
            if (type != CharacterType.NPC)
                controllable.IsInteractable = true;

            Network.EntityList.Add(id, controllable);

            if (controllable.SpriteMode == ClientSpriteType.Prefab)
                return controllable;

            if (state == CharacterState.Moving)
                Network.LoadMoveData2(msg, controllable);
            if (state == CharacterState.Sitting)
            {
                controllable.SpriteAnimator.ChangeMotion(SpriteMotion.Sit);
                controllable.SpriteAnimator.State = SpriteState.Sit;
            }

            if (State.EntityId == controllable.Id)
            {
                Camera.Target = controllable.gameObject;
                //Debug.Log($"Player entity sent, we're at position {pos}");

                if (!CameraFollower.Instance.CinemachineMode)
                    SceneTransitioner.Instance.FadeIn();
                CameraFollower.Instance.SnapLookAt();
            }

            return controllable;
        }
    }
}