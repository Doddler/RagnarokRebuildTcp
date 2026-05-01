using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Effects.EffectHandlers.Environment;
using Assets.Scripts.Effects.EffectHandlers.General;
using Assets.Scripts.Effects.EffectHandlers.Skills;
using Assets.Scripts.Effects.EffectHandlers.Skills.Assassin;
using Assets.Scripts.Effects.EffectHandlers.Skills.Priest;
using Assets.Scripts.Misc;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using Assets.Scripts.Sprites;
using Assets.Scripts.Utility;
using RebuildSharedData.ClientTypes;
using RebuildSharedData.Enum;
using RebuildSharedData.Packets;
using UnityEngine;
using UnityEngine.Rendering;
using PlayerSpawnParameters = RebuildSharedData.Packets.PlayerSpawnParameters;

namespace Assets.Scripts.PlayerControl
{
    public class EntityBuilder : MonoBehaviour
    {
        public static EntityBuilder Instance;
        
        private ClientDataLoader dataLoader;

        public void Initialize()
        {
            if (dataLoader != null)
                return;
            
            Instance = this;
            dataLoader = GetComponent<ClientDataLoader>();
        }
        
        public ServerControllable LoadPlayer(ref EntitySpawnParameters spawn, ref PlayerSpawnParameters player)
        {
            var go = new GameObject(spawn.Name);
            go.layer = LayerMask.NameToLayer("Characters");
            go.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
            var control = go.AddComponent<ServerControllable>();
            
            var pData = dataLoader.PlayerClassLookup[0]; //novice
            if (dataLoader.PlayerClassLookup.TryGetValue(spawn.ClassId, out pData))
            {
                go.name = $"[{pData.Name}]{spawn.Name}";
            }
            else
            {
                Debug.LogWarning($"Failed to player class for player {spawn.Name} with classId of {spawn.ClassId}");
            }
            
            if (spawn.OverrideClassId > 0 && dataLoader.MonsterClassLookup.TryGetValue(spawn.OverrideClassId, out var monData))
            {
                
                if (monData.SpriteName.Contains(".prefab"))
                    InstantiatePrefabEntity(control, ref spawn, monData.SpriteName);
                else
                    InstantiateMonsterSprite(control, ref spawn, monData);
                
                if (player.Follower != CharacterFollowerState.None)
                    ChangeFollowerState(control, player.Follower);
                
                if (spawn.IsMainCharacter)
                {
                    var state = PlayerState.Instance;
                    control.IsMainCharacter = true;
                    state.PlayerName = control.Name;
                    state.UpdatePlayerName();
                    state.WeaponClass = player.WeaponClass;
                    CameraFollower.Instance.CharacterDetailBox.CharacterJob.text = pData.Name;
                    CameraFollower.Instance.CharacterDetailBox.BaseLvlDisplay.text = $"Base Lv. {control.Level}";
                }
                
                control.ConfigureEntity(spawn.ServerId, spawn.Position.ToVector2Int(), spawn.Facing);
                control.Name = spawn.Name;
                control.Hp = spawn.Hp;
                control.MaxHp = spawn.MaxHp;
                return control;
            }

            InstantiatePlayerSprite(control, ref spawn, ref player, pData);

            return control;
        }

        public ServerControllable LoadMonster(ref EntitySpawnParameters spawn)
        {
            var go = new GameObject(spawn.Name);
            go.layer = LayerMask.NameToLayer("Characters");
            go.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
            var control = go.AddComponent<ServerControllable>();
            
            var mData = dataLoader.MonsterClassLookup[4000]; //poring
            if (dataLoader.MonsterClassLookup.TryGetValue(spawn.ClassId, out var lookupData))
                mData = lookupData;
            else
                Debug.LogWarning("Failed to find monster with id of " + spawn.ClassId);

            if (mData.SpriteName.Contains(".prefab"))
            {
                InstantiatePrefabEntity(control, ref spawn, mData.SpriteName);
                return control;
            }
            
            InstantiateMonsterSprite(control, ref spawn, mData);
            
            return control;
        }

        public ServerControllable LoadNpc(ref EntitySpawnParameters spawn, ref NpcSpawnParameters npc)
        {
            var go = new GameObject(spawn.Name);
            go.layer = LayerMask.NameToLayer("Characters");
            go.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
            var control = go.AddComponent<ServerControllable>();

            if (npc.DisplayType == NpcDisplayType.VendingProxy)
            {
                InstantiateEffect(control, ref spawn, ref npc, NpcEffectType.None);
                UiManager.Instance.VendAndChatManager.CreateVendDialog(spawn.ServerId, npc.OwnerId, go, name);
                return control;
            }

            if (npc.DisplayType == NpcDisplayType.Effect)
            {
                InstantiateEffect(control, ref spawn, ref npc, npc.EffectType);
                return control;
            }
            
            var mData = dataLoader.MonsterClassLookup[4000]; //poring
            if (dataLoader.MonsterClassLookup.TryGetValue(spawn.ClassId, out var lookupData))
                mData = lookupData;
            else
                Debug.LogWarning("Failed to find monster with id of " + spawn.ClassId);

            if (mData.SpriteName.Contains(".prefab"))
            {
                InstantiatePrefabEntity(control, ref spawn, mData.SpriteName);
                control.IsInteractable = npc.Interactable;
                return control;
            }

            InstantiateMonsterSprite(control, ref spawn, mData);
            control.IsInteractable = npc.Interactable;

            return control;
        }
        
        private void InstantiatePlayerSprite(ServerControllable control, ref EntitySpawnParameters spawn, ref PlayerSpawnParameters player, PlayerClassData data)
        {
            bool isMounted = (player.Follower & CharacterFollowerState.Mounted) > 0 && (spawn.ClassId == 7 || spawn.ClassId == 13);
            var displayData = data;
            if (isMounted)
                dataLoader.PlayerClassLookup.TryGetValue(spawn.ClassId, out displayData);
            
            var go = control.gameObject;
            var billboard = go.AddComponent<BillboardObject>();
            billboard.Style = BillboardStyle.Character;
            go.AddComponent<SortingGroup>();

            var body = new GameObject("Sprite");
            body.layer = LayerMask.NameToLayer("Characters");
            body.transform.SetParent(go.transform, false);
            body.transform.localPosition = Vector3.zero;
            body.AddComponent<SortingGroup>();

            var head = new GameObject("Head");
            head.layer = LayerMask.NameToLayer("Characters");
            head.transform.SetParent(go.transform, false);
            head.transform.localPosition = Vector3.zero;

            var bodySprite = body.AddComponent<RoSpriteAnimator>();
            var headSprite = head.AddComponent<RoSpriteAnimator>();

            control.ClassId = spawn.ClassId;
            control.OverrideClassId = isMounted ? spawn.ClassId + 500 : control.ClassId;
            control.SpriteAnimator = bodySprite;
            control.CharacterType = CharacterType.Player;
            control.SpriteMode = ClientSpriteType.Sprite;
            control.IsAlly = true;
            control.IsMainCharacter = spawn.IsMainCharacter;
            control.IsMale = player.IsMale;
            control.Level = spawn.Level;
            control.WeaponClass = player.WeaponClass;
            control.PartyName = player.PartyName;
            control.IsPartyMember = player.PartyId > 0 && player.PartyId == player.PartyId;
            
            
            bodySprite.Controllable = control;
            if (spawn.State == CharacterState.Moving)
                bodySprite.ChangeMotion(SpriteMotion.Walk);
            bodySprite.ChildrenSprites.Add(headSprite);
            //bodySprite.SpriteOffset = 0.5f;
            bodySprite.HeadFacing = player.HeadFacing;

            if (spawn.State == CharacterState.Sitting)
                bodySprite.State = SpriteState.Sit;
            if (spawn.State == CharacterState.Moving)
                bodySprite.State = SpriteState.Walking;

            if (spawn.State == CharacterState.Dead)
                control.PlayerDie(Vector2Int.zero);

            headSprite.Parent = bodySprite;
            headSprite.SpriteOrder = 1;

            control.ShadowSize = 0.5f;
            control.WeaponClass = player.WeaponClass;

            var weapon = player.Weapon;
            var shield = player.Shield;
            var offHand = 0;
            if (player.Shield > 0 && dataLoader.TryGetItemById(player.Shield, out var item) && item.ItemClass == ItemClass.Weapon)
            {
                
                offHand = item.SubType;
                shield = 0;
            }

            var bodySpriteName = dataLoader.GetPlayerBodySpriteName(control.OverrideClassId, player.IsMale);
            var headSpriteName = dataLoader.GetPlayerHeadSpriteName(player.HeadType, player.HairColor, player.IsMale);
            
            
            Debug.Log($"Instantiate player sprite with job {spawn.ClassId} weapon {player.WeaponClass}");

            dataLoader.LoadAndAttachEquipmentSprite(control, player.Headgear1, EquipPosition.HeadUpper, 4);
            dataLoader.LoadAndAttachEquipmentSprite(control, player.Headgear2, EquipPosition.HeadMid, 3);
            dataLoader.LoadAndAttachEquipmentSprite(control, player.Headgear3, EquipPosition.HeadLower, 2);
            dataLoader.LoadAndAttachEquipmentSprite(control, shield, EquipPosition.Shield, 1);

            dataLoader.LoadAndAttachWeapon(control, weapon, offHand);

            control.ConfigureEntity(spawn.ServerId, spawn.Position.ToVector2Int(), spawn.Facing);
            control.Name = spawn.Name;
            control.Hp = spawn.Hp;
            control.MaxHp = spawn.MaxHp;
            // control.EnsureFloatingDisplayCreated().SetUp(param.Name, param.MaxHp, 0);

            AddressableUtility.LoadRoSpriteData(go, bodySpriteName, bodySprite.OnSpriteDataLoad);
            AddressableUtility.LoadRoSpriteData(go, headSpriteName, headSprite.OnSpriteDataLoad);
            control.AttachShadow(dataLoader.ShadowSprite);
            //AddressableUtility.LoadSprite(go, "shadow", control.AttachShadow);

            var state = PlayerState.Instance;
            
            if (control.IsMainCharacter)
            {
                CameraFollower.Instance.CharacterDetailBox.CharacterJob.text = data.Name;
                state.PlayerName = control.Name;
                state.UpdatePlayerName();
                state.WeaponClass = player.WeaponClass;
                CameraFollower.Instance.CharacterDetailBox.BaseLvlDisplay.text = $"Base Lv. {control.Level}";
            }

            control.Init();

            if (player.PartyId == state.PartyId)
            {
                state.AssignPartyMemberControllable(control.Id, control);
                if (state.PartyMemberIdLookup.TryGetValue(control.Id, out var partyMemberId) &&
                    UiManager.Instance.PartyPanel.PartyEntryLookup.TryGetValue(partyMemberId, out var panel))
                    panel.ClearAllStatusEffects(); //we will re-assign them right after
            }
            
            ChangeFollowerState(control, player.Follower);
        }

        private void InstantiateMonsterSprite(ServerControllable control, ref EntitySpawnParameters spawn, MonsterClassData data)
        {
            var go = control.gameObject;

            control.ClassId = data.Id;
            control.OverrideClassId = spawn.OverrideClassId;
            control.CharacterType = spawn.Type;
            control.SpriteMode = ClientSpriteType.Sprite;
            control.IsInteractable = false;
            var billboard = go.AddComponent<BillboardObject>();
            billboard.Style = BillboardStyle.Character;

            var child = new GameObject("Sprite");
            child.layer = LayerMask.NameToLayer("Characters");
            child.transform.SetParent(go.transform, false);
            child.transform.localPosition = Vector3.zero;

            var sprite = child.AddComponent<RoSpriteAnimator>();
            sprite.Controllable = control;

            control.SpriteAnimator = sprite;
            //control.SpriteAnimator.SpriteOffset = mData.Offset;
            control.ShadowSize = data.ShadowSize;
            control.IsAlly = spawn.Type != CharacterType.Monster;
            control.Level = spawn.Level;
            control.Name = spawn.Name;
            if (string.IsNullOrEmpty(spawn.Name))
                control.Name = data.Name;
            control.WeaponClass = 0;
            control.Hp = spawn.Hp;
            control.MaxHp = spawn.MaxHp;
            if (ColorUtility.TryParseHtmlString(data.Color, out var color))
                sprite.BaseColor = color;

            control.ConfigureEntity(spawn.ServerId, spawn.Position.ToVector2Int(), spawn.Facing);
            // control.EnsureFloatingDisplayCreated().SetUp(param.Name, param.MaxHp, 0);

            var basePath = "Assets/Sprites/Monsters/";
            if (control.ClassId < 4000)
                basePath = "Assets/Sprites/Npcs/";


            AddressableUtility.LoadRoSpriteData(go, basePath + data.SpriteName, control.SpriteAnimator.OnSpriteDataLoad);
            if (data.ShadowSize > 0)
                control.AttachShadow(dataLoader.ShadowSprite);
            //AddressableUtility.LoadSprite(go, "shadow", control.AttachShadow);

            control.Init();

            if (spawn.CharacterStatusEffects != null)
                foreach (var (s, duration) in spawn.CharacterStatusEffects)
                    StatusEffectState.AddStatusToTarget(control, s, true, duration);
        }

        private void InstantiatePrefabEntity(ServerControllable control, ref EntitySpawnParameters spawn, string prefabName)
        {
            control.ClassId = spawn.ClassId;
            control.OverrideClassId = spawn.OverrideClassId;
            control.CharacterType = spawn.Type;
            control.SpriteMode = ClientSpriteType.Prefab;
            control.EntityObject = control.gameObject;
            control.IsInteractable = false;
            control.Level = spawn.Level;
            control.Name = spawn.Name;
            control.IsAlly = spawn.Type != CharacterType.Monster;
            control.CharacterState = spawn.State;
            
            
            control.ConfigureEntity(spawn.ServerId, spawn.Position.ToVector2Int(), spawn.Facing);
            control.EnsureFloatingDisplayCreated().SetUp(control, spawn.Name, spawn.MaxHp, 0, false, false);
            
            ClientDataLoader.Instance.AttachPrefabToControllable(control, prefabName);
        }

        private void InstantiateEffect(ServerControllable control, ref EntitySpawnParameters spawn, ref NpcSpawnParameters npc, NpcEffectType type)
        {
            var obj = control.gameObject;
            obj.name = type.ToString();
            control.ClassId = spawn.ClassId;
            control.CharacterType = CharacterType.NPC;
            control.SpriteMode = ClientSpriteType.Prefab;
            control.EntityObject = obj;
            control.Level = spawn.Level;
            control.Name = spawn.Name;
            control.IsAlly = true;
            control.IsInteractable = npc.Interactable;
            control.CharacterState = spawn.State;

            control.ConfigureEntity(spawn.ServerId, spawn.Position.ToVector2Int(), spawn.Facing);
            if (type < NpcEffectType.AnkleSnare || type > NpcEffectType.ShockwaveTrap)
            {
                obj.AddComponent<BillboardObject>();
                obj.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
            }
            else
                obj.transform.localScale = new Vector3(1f, 1f, 1f);

            switch (type)
            {
                case NpcEffectType.Firewall:
                    CameraFollower.Instance.AttachEffectToEntity("FirewallEffect", obj);
                    break;
                case NpcEffectType.Pneuma:
                    CameraFollower.Instance.AttachEffectToEntity("Pneuma1", obj);
                    // var go = new GameObject("PneumaArea");
                    var highlighter = GroundHighlighter.Create(control, "pneumazone", new Color(1, 1, 1, 0.2f), 2);
                    highlighter.MaxTime = 10f;
                    break;
                case NpcEffectType.Demonstration:
                    var demonstration = RoSpriteEffect.AttachSprite(control, "Assets/Sprites/Effects/데몬스트레이션.spr", -0.15f, 1f, RoSpriteEffectFlags.None);
                    control.gameObject.transform.localScale = new Vector3(1.8f, 1.8f, 1.8f);
                    demonstration.SetDurationByFrames(9999);
                    AudioManager.Instance.OneShotSoundEffect(control.Id, $"ef_firewall.ogg", control.transform.position);
                    control.AttachEffect(demonstration);
                    break;
                case NpcEffectType.WarpPortalOpening:
                    WarpPortalOpeningEffect.StartWarpPortalOpen(obj);
                    break;
                case NpcEffectType.WarpPortal:
                    WarpPortalEffect.StartWarpPortal(obj);
                    break;
                case NpcEffectType.SafetyWall:
                    SafetyWallEffect.LaunchSafetyWall(obj);
                    break;
                case NpcEffectType.WaterBall:
                    //DummyGroundEffect.Create(obj, $"<color=#AAAAFF>Water Ball!!");
                    WaterBallRiseEffect.LaunchWaterBallRise(obj);
                    break;
                case NpcEffectType.MapWarp:
                    MapWarpEffect.StartWarp(obj);
                    break;
                case NpcEffectType.LightOrb:
                    DiscoLightsEffect.LaunchDiscoLights(control);
                    break;
                case NpcEffectType.Sanctuary:
                    SanctuaryEffect.Create(control, false);
                    //DummyGroundEffect.Create(obj, "Sanctuary");
                    break;
                case NpcEffectType.MagnusExorcismus:
                    SanctuaryEffect.Create(control, true);
                    //DummyGroundEffect.Create(obj, "Sanctuary");
                    break;
                case NpcEffectType.VenomDust:
                    //DummyGroundEffect.Create(obj, "VenomDust");
                    VenomDustEffect.Create(control);
                    break;
                case NpcEffectType.FirePillar:
                    FirePillarEffect.Create(control);
                    break;
            case NpcEffectType.Quagmire:
                    //DummyGroundEffect.Create(obj, $"<color=#33FF33>Quagmire!!");
                    QuagmireEffect.Create(control);
                    break;
                case NpcEffectType.AnkleSnare:
                    dataLoader.AttachPrefabToControllable(control, "Assets/Effects/Prefabs/ModelAnkleSnare.prefab");
                    break;
                case NpcEffectType.LandMine:
                    dataLoader.AttachPrefabToControllable(control, "Assets/Effects/Prefabs/ModelLandMine.prefab");
                    break;
                case NpcEffectType.BlastMine:
                    dataLoader.AttachPrefabToControllable(control, "Assets/Effects/Prefabs/ModelBlastMine.prefab");
                    control.IsAttackable = true;
                    break;
                case NpcEffectType.ClaymoreTrap:
                    dataLoader.AttachPrefabToControllable(control, "Assets/Effects/Prefabs/ModelClaymoreTrap.prefab");
                    control.IsAttackable = true;
                    break;
                case NpcEffectType.FlasherTrap:
                    dataLoader.AttachPrefabToControllable(control, "Assets/Effects/Prefabs/ModelFlasherTrap.prefab");
                    break;
                case NpcEffectType.FreezingTrap:
                    dataLoader.AttachPrefabToControllable(control, "Assets/Effects/Prefabs/ModelFreezingTrap.prefab");
                    break;
                case NpcEffectType.SandmanTrap:
                    dataLoader.AttachPrefabToControllable(control, "Assets/Effects/Prefabs/ModelSandmanTrap.prefab");
                    break;
                case NpcEffectType.SkidTrap:
                    dataLoader.AttachPrefabToControllable(control, "Assets/Effects/Prefabs/ModelSkidTrap.prefab");
                    break;
                case NpcEffectType.ShockwaveTrap:
                    dataLoader.AttachPrefabToControllable(control, "Assets/Effects/Prefabs/ModelShockwaveTrap.prefab");
                    break;
                case NpcEffectType.TalkieBox:
                    dataLoader.AttachPrefabToControllable(control, "Assets/Effects/Prefabs/ModelTalkieBox.prefab");
                    break;
            }
        }
        
        
        public void ChangeFollowerState(ServerControllable control, CharacterFollowerState follower)
        {
            if (control.FollowerObject != null)
            {
                Object.Destroy(control.FollowerObject);
            }

            if (follower == CharacterFollowerState.None)
            {
                control.FollowerObject = null;
                if (control.IsMainCharacter)
                {
                    PlayerState.Instance.HasCart = false;
                    UiManager.Instance.EquipmentWindow.RefreshEquipmentWindow();
                }

                return;
            }
            
            if ((follower & CharacterFollowerState.AnyCart) > 0)
            {
                var cartStyle = follower switch
                {
                    CharacterFollowerState.Cart0 => 0,
                    CharacterFollowerState.Cart1 => 1,
                    CharacterFollowerState.Cart2 => 2,
                    CharacterFollowerState.Cart3 => 3,
                    CharacterFollowerState.Cart4 => 4,
                    _ => 0
                };
                var cartObj = new GameObject("Cart");
                var cart = cartObj.AddComponent<CartFollower>();
                cart.AttachCart(control, cartStyle);
                control.FollowerObject = cartObj;
            }

            if ((follower & CharacterFollowerState.Falcon) > 0)
            {
                var birdObj = new GameObject("Falcon");
                var bird = birdObj.AddComponent<BirdFollower>();
                bird.AttachBird(control, 0);

                if (control.FollowerObject != null)
                    Destroy(control.FollowerObject);
                control.FollowerObject = birdObj;

                if (control.IsMainCharacter)
                    PlayerState.Instance.HasBird = true;
            }
            
            if(control.IsMainCharacter)
                UiManager.Instance.EquipmentWindow.RefreshEquipmentWindow();
        }
    }
}