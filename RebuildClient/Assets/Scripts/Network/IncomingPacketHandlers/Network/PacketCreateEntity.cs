using System;
using System.Collections.Generic;
using Assets.Scripts.Data;
using Assets.Scripts.Effects;
using Assets.Scripts.Effects.EffectHandlers.General;
using Assets.Scripts.Misc;
using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.Network.IncomingPacketHandlers.Party;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.Sprites;
using Assets.Scripts.UI.Hud;
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
            var sp = 0;
            var maxSp = 0;
            Dictionary<CharacterStatusEffect, float> statuses = null; 

            if (type == CharacterType.Player || type == CharacterType.Monster || type == CharacterType.PlayerLikeNpc)
            {
                lvl = (int)msg.ReadByte();
                maxHp = (int)msg.ReadInt32();
                hp = (int)msg.ReadInt32();

                while(msg.ReadBoolean()) //has status effects
                {
                    statuses ??= new();
                    var status = (CharacterStatusEffect)msg.ReadByte();
                    var duration = msg.ReadFloat();
                    statuses.Add(status, duration);
                    
                    //Debug.Log($"{classId} has a status effect! {status}");

                    if (id == Network.PlayerId)
                        StatusEffectPanel.Instance.AddStatusEffect(status, duration);
                }
            }

            if (Network.EntityList.TryGetValue(id, out var oldEntity))
            {
                //if for some reason we try to spawn an entity that already exists, we kill the old one.
                oldEntity.FadeOutAndVanish(0.1f);
                Network.EntityList.Remove(id);
            }

            ServerControllable controllable;
            if (type == CharacterType.Player || type == CharacterType.PlayerLikeNpc)
            {
                var headFacing = (HeadFacing)msg.ReadByte();
                var headId = msg.ReadByte();
                var hairDyeId = msg.ReadByte();
                var weapon = msg.ReadByte();
                var isMale = msg.ReadBoolean();
                var name = msg.ReadString();
                var head1 = msg.ReadInt32();
                var head2 = msg.ReadInt32();
                var head3 = msg.ReadInt32();
                var weaponId = msg.ReadInt32();
                var shieldId = msg.ReadInt32();
                sp = msg.ReadInt32();
                maxSp = msg.ReadInt32();
                var partyId = 0;
                var partyName = "";
                
                if(type == CharacterType.Player && msg.ReadByte() == 1)
                {
                    partyId = msg.ReadInt32();
                    partyName = msg.ReadString();
                }

                var follower = (PlayerFollower)msg.ReadByte();
                
                var isMain = Network.PlayerId == id;
                if (isMain)
                {
                    State.EntityId = id;
                    State.PlayerName = name;
                    State.IsValid = true;
                    UiManager.Instance.SkillHotbar.LoadHotBarData(name);

                    // State.PartyId = partyId;
                    // State.PartyName = partyName;
                    // State.IsInParty = partyId > 0;
                    //PacketAcceptPartyInvite.LoadPartyMemberDetails(msg);
                    // Debug.Log($"You are in party: {partyName}");
                }
                
                Debug.Log("Name: " + name );
                Debug.Log($"New player entity: {name} Headgear: {head1} {head2} {head3} WeaponClass:{weapon} WeaponId: {weaponId} Shield:{shieldId}");

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
                    Sp = sp,
                    MaxSp = maxSp,
                    WeaponClass = weapon,
                    IsMainCharacter = isMain,
                    CharacterStatusEffects = statuses,
                    Headgear1 = head1,
                    Headgear2 = head2,
                    Headgear3 = head3,
                    Weapon = weaponId,
                    Shield = shieldId,
                    PartyId = partyId,
                    PartyName = partyName,
                    Follower = follower
                };

                controllable = ClientDataLoader.Instance.InstantiatePlayer(ref playerData);

                if (id == Network.PlayerId)
                {
                    State.Level = lvl;

                    var max = CameraFollower.Instance.ExpForLevel(controllable.Level);
                    Camera.UpdatePlayerExp(State.Exp, max);
                    controllable.IsHidden = State.IsAdminHidden;
                    State.JobId = classId;
                    State.IsMale = isMale;
                    State.HairStyleId = headId;
                    State.HairColorId = hairDyeId;
                    State.HasCart = (follower & PlayerFollower.AnyCart) > 0;
                    
                    UiManager.Instance.SkillManager.UpdateAvailableSkills();
                    UiManager.Instance.EquipmentWindow.UpdateCharacterDisplay(head1, head2, head3);
                    UiManager.Instance.EquipmentWindow.RefreshEquipmentWindow(); //follower state might have changed
                }
            }
            else
            {
                var interactable = false;
                var name = string.Empty;
                var displayType = NpcDisplayType.Sprite;
                var effectType = NpcEffectType.None;
                var owner = -1;
                
                if (type == CharacterType.NPC)
                {
                    name = msg.ReadString();
                    displayType = (NpcDisplayType)msg.ReadByte();
                    interactable = msg.ReadBoolean();
                    effectType = (NpcEffectType)msg.ReadByte();
                    //Debug.Log(name);

                    if (displayType == NpcDisplayType.VendingProxy)
                        owner = msg.ReadInt32();
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

                if (displayType == NpcDisplayType.VendingProxy)
                {
                    monData.Name = "Vend Shop";
                    controllable = ClientDataLoader.Instance.InstantiateEffect(ref monData, NpcEffectType.None, true);
                    Network.EntityList.Add(id, controllable);
                    UiManager.VendAndChatManager.CreateVendDialog(id, owner, controllable.gameObject, name);
                    return controllable;
                }
                
                if (displayType == NpcDisplayType.Effect)
                {
                    
                    controllable = ClientDataLoader.Instance.InstantiateEffect(ref monData, effectType);
                    Network.EntityList.Add(id, controllable);
                    return controllable;
                }

                controllable = ClientDataLoader.Instance.InstantiateMonster(ref monData, type);    
            }

            controllable.EnsureFloatingDisplayCreated().SetUp(controllable, controllable.Name, maxHp, hp, type == CharacterType.Player, controllable.IsMainCharacter);
            if (controllable.IsMainCharacter)
            {
                Camera.UpdatePlayerHP(hp, maxHp);
                controllable.SetHp(hp, maxHp);
                //CameraFollower.UpdatePlayerSP(100, 100);
                
                if(State.Sp > 0 && State.MaxSp > 0)
                    controllable.SetSp(State.Sp, State.MaxSp);
            }

            if (maxSp > 0)
            {
                if(controllable.IsMainCharacter)
                    Camera.UpdatePlayerSP(sp, maxSp);
                controllable.SetSp(sp, maxSp);
            }

            if (type != CharacterType.NPC)
                controllable.IsInteractable = true;

            controllable.SetHp(hp);

            Network.EntityList.Add(id, controllable);

            if (controllable.SpriteMode == ClientSpriteType.Prefab)
                return controllable;
            
            // if(statuses != null && statuses.Contains(CharacterStatusEffect.PushCart))
            //     StatusEffectState.AddStatusToTarget(controllable, CharacterStatusEffect.PushCart);

            try
            {
                if (state == CharacterState.Moving)
                    Network.LoadMoveData2(msg, controllable);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load move data for type {type} id {classId}: {controllable.Name}");
                Debug.LogException(e);
            }

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