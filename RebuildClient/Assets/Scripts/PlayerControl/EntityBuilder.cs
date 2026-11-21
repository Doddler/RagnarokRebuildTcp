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
using RebuildSharedData.Data;

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
            control.IsMainCharacter = spawn.IsMainCharacter;
            control.SetAppearance(PlayerAppearance.FromSpawn(spawn, player));
            NameAfterJob(control, ref spawn);

            if (spawn.OverrideClassId > 0 && dataLoader.MonsterClassLookup.TryGetValue(spawn.OverrideClassId, out var monData))
            {
                
                if (monData.SpriteName.Contains(".prefab"))
                    InstantiatePrefabEntity(control, ref spawn, monData.SpriteName);
                else
                    InstantiateMonsterSprite(control, ref spawn, monData);
                
                ChangeFollowerState(control, player.Follower);

                control.ConfigureEntity(spawn.ServerId, spawn.Position.ToVector2Int(), spawn.Facing);
                control.SetName(spawn.Name);
                control.InitializeHp(spawn.Hp, spawn.MaxHp);
                return control;
            }

            InstantiatePlayerSprite(control, ref spawn, ref player);

            return control;
        }

        //named "[Job]Name"; an unknown job shows as novice
        private void NameAfterJob(ServerControllable control, ref EntitySpawnParameters spawn)
        {
            if (!dataLoader.PlayerClassLookup.TryGetValue(spawn.ClassId, out var data))
            {
                Debug.LogWarning($"Failed to find player class for player {spawn.Name} with classId of {spawn.ClassId}");
                data = dataLoader.PlayerClassLookup[0];
            }

            control.gameObject.name = $"[{data.Name}]{spawn.Name}";
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
        
        private void InstantiatePlayerSprite(ServerControllable control, ref EntitySpawnParameters spawn, ref PlayerSpawnParameters player)
        {
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

            ApplyPlayerFields(control, ref spawn, ref player);
            control.SpriteAnimator = bodySprite;
            control.CharacterType = CharacterType.Player;
            control.SpriteMode = ClientSpriteType.Sprite;
            control.IsAlly = true;

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
            control.HeadAnimator = headSprite;

            control.ShadowSize = 0.5f;

            Debug.Log($"Instantiate player sprite with job {spawn.ClassId} weapon {player.WeaponClass}");

            AttachGear(control, player.Headgear1, player.Headgear2, player.Headgear3, player.Weapon, player.Shield);

            control.ConfigureEntity(spawn.ServerId, spawn.Position.ToVector2Int(), spawn.Facing);
            control.InitializeHp(spawn.Hp, spawn.MaxHp);
            // control.EnsureFloatingDisplayCreated().SetUp(param.Name, param.MaxHp, 0);

            LoadBodyAndHead(control, ref player);
            control.AttachShadow(dataLoader.ShadowSprite);
            //AddressableUtility.LoadSprite(go, "shadow", control.AttachShadow);

            control.Init();

            ChangeFollowerState(control, player.Follower);
        }

        //Applies a refreshed player to the shown object, keeping its position, effects and selection.
        //False when it needs a new object, as for a monster disguise.
        public bool TryRefreshPlayer(ServerControllable control, ref EntitySpawnParameters spawn, ref PlayerSpawnParameters player)
        {
            if (control.HeadAnimator == null)
                return false;
            if (spawn.OverrideClassId > 0 && dataLoader.MonsterClassLookup.ContainsKey(spawn.OverrideClassId))
                return false;

            NameAfterJob(control, ref spawn);
            ApplyPlayerFields(control, ref spawn, ref player);
            control.SetAppearance(PlayerAppearance.FromSpawn(spawn, player));

            LoadBodyAndHead(control, ref player);
            AttachGear(control, player.Headgear1, player.Headgear2, player.Headgear3, player.Weapon, player.Shield);

            if (player.Follower != control.Follower)
                ChangeFollowerState(control, player.Follower);

            return true;
        }

        private void ApplyPlayerFields(ServerControllable control, ref EntitySpawnParameters spawn, ref PlayerSpawnParameters player)
        {
            //a mounted knight or crusader uses its peco body, job id + 500
            var isMounted = (player.Follower & CharacterFollowerState.Mounted) > 0 && (spawn.ClassId == 7 || spawn.ClassId == 13);
            control.ClassId = spawn.ClassId;
            control.OverrideClassId = isMounted ? spawn.ClassId + 500 : spawn.ClassId;
            control.InitializeParty(player.PartyName, PlayerState.Instance.Party.IsMemberEntity(spawn.ServerId));
            control.SetName(spawn.Name); //after the party name, which the name plate shows
            control.SetLevel(spawn.Level);
            control.SetWeaponClass(player.WeaponClass);
        }

        //each layer shows its old sprite until the new one loads
        private void LoadBodyAndHead(ServerControllable control, ref PlayerSpawnParameters player)
        {
            var go = control.gameObject;
            var bodySpriteName = dataLoader.GetPlayerBodySpriteName(control.OverrideClassId, player.IsMale);
            var headSpriteName = dataLoader.GetPlayerHeadSpriteName(player.HeadType, player.HairColor, player.IsMale);
            AddressableUtility.LoadRoSpriteData(go, bodySpriteName, control.SpriteAnimator.ReplaceSpriteData);
            AddressableUtility.LoadRoSpriteData(go, headSpriteName, control.HeadAnimator.ReplaceSpriteData);
        }

        //Gender and job pick the sprite variant, so set those first.
        public void AttachGear(ServerControllable control, int headgear1, int headgear2, int headgear3, int weapon, int shield)
        {
            //a dual-wielded weapon arrives in the shield slot
            var offHand = 0;
            if (shield > 0 && dataLoader.TryGetItemById(shield, out var item) && item.ItemClass == ItemClass.Weapon)
            {
                offHand = item.SubType;
                shield = 0;
            }

            dataLoader.LoadAndAttachEquipmentSprite(control, headgear1, EquipPosition.HeadUpper, 4);
            dataLoader.LoadAndAttachEquipmentSprite(control, headgear2, EquipPosition.HeadMid, 3);
            dataLoader.LoadAndAttachEquipmentSprite(control, headgear3, EquipPosition.HeadLower, 2);
            dataLoader.LoadAndAttachEquipmentSprite(control, shield, EquipPosition.Shield, 1);

            dataLoader.LoadAndAttachWeapon(control, weapon, offHand);
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
            control.SetLevel(spawn.Level);
            control.SetName(spawn.Name);
            if (string.IsNullOrEmpty(spawn.Name))
                control.SetName(data.Name);
            control.SetWeaponClass(0);
            control.InitializeHp(spawn.Hp, spawn.MaxHp);
            if (ColorUtility.TryParseHtmlString(data.Color, out var color))
                sprite.BaseColor = color;

            control.ConfigureEntity(spawn.ServerId, spawn.Position.ToVector2Int(), spawn.Facing);
            // control.EnsureFloatingDisplayCreated().SetUp(param.Name, param.MaxHp, 0);

            AddressableUtility.LoadRoSpriteData(go, ClientDataLoader.GetMonsterSpritePath(control.ClassId, data.SpriteName), control.SpriteAnimator.OnSpriteDataLoad);
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
            control.SetLevel(spawn.Level);
            control.SetName(spawn.Name);
            control.IsAlly = spawn.Type != CharacterType.Monster;
            control.CharacterState = spawn.State;
            
            
            control.ConfigureEntity(spawn.ServerId, spawn.Position.ToVector2Int(), spawn.Facing);

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
            control.SetLevel(spawn.Level);
            control.SetName(spawn.Name);
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
        
        
        //The peco is part of the body sprite; an unchanged follower keeps its place.
        public void ChangeFollowerState(ServerControllable control, CharacterFollowerState newState)
        {
            var currentState = control.Follower;

            var currentHasFalcon = (currentState & CharacterFollowerState.Falcon) != 0;
            var newHasFalcon = (newState & CharacterFollowerState.Falcon) != 0;
            if (newHasFalcon && !currentHasFalcon)
            {
                control.FalconObject = new GameObject("Falcon");
                control.FalconObject.AddComponent<BirdFollower>().AttachBird(control, 0);
            }
            else if (currentHasFalcon && !newHasFalcon)
            {
                Object.Destroy(control.FalconObject);
                control.FalconObject = null;
            }

            var newCartStyle = newState.CartStyle();
            if (newCartStyle != currentState.CartStyle())
            {
                if (control.CartObject != null)
                    Object.Destroy(control.CartObject);
                control.CartObject = null;

                if (newCartStyle >= 0)
                {
                    control.CartObject = new GameObject("Cart");
                    control.CartObject.AddComponent<CartFollower>().AttachCart(control, newCartStyle);
                }
            }

            control.SetFollowersVisible(!control.IsHiddenFromPlayer);
            control.SetFollower(newState);
        }
    }
}
