using System;
using System.Collections.Generic;
using Assets.Scripts.Network;
using Assets.Scripts.Utility;
using JetBrains.Annotations;
using RebuildSharedData.ClientTypes;
using RebuildSharedData.Enum;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering;
using UnityEngine.U2D;
using Random = UnityEngine.Random;

namespace Assets.Scripts.Sprites
{
    public class MapViewpoint
    {
        public string MapName;
        public int ZoomMin;
        public int ZoomDist;
        public int ZoomIn;
        public int SpinMin;
        public int SpinMax;
        public int SpinIn;
        public int HeightMin;
        public int HeightMax;
        public int HeightIn;
    }

    public class ClientDataLoader : MonoBehaviour
    {
        public static ClientDataLoader Instance;

        public TextAsset MonsterClassData;
        public TextAsset PlayerClassData;
        public TextAsset PlayerHeadData;
        public TextAsset PlayerWeaponData;
        public TextAsset WeaponClassData;
        public TextAsset SkillData;
        public TextAsset SkillTreeData;
        public TextAsset MapViewpointData;
        public TextAsset UniqueAttackActionData;
        public SpriteAtlas ItemIconAtlas;

        private readonly Dictionary<int, MonsterClassData> monsterClassLookup = new();
        private readonly Dictionary<int, PlayerHeadData> playerHeadLookup = new();
        private readonly Dictionary<int, PlayerClassData> playerClassLookup = new();
        private readonly Dictionary<int, Dictionary<int, List<PlayerWeaponData>>> playerWeaponLookup = new();
        private readonly Dictionary<int, WeaponClassData> weaponClassData = new();
        private readonly Dictionary<CharacterSkill, SkillData> skillData = new();
        private readonly Dictionary<string, MapViewpoint> mapViewpoints = new();
        private readonly Dictionary<string, Dictionary<CharacterSkill, UniqueAttackAction>> uniqueSpriteActions = new();
        private readonly Dictionary<int, ClientSkillTree> jobSkillTrees = new();
        private readonly Dictionary<string, int> jobNameToIdTable = new();

        private readonly List<string> validMonsterClasses = new();
        private readonly List<string> validMonsterCodes = new();

        private bool isInitialized;

        public bool IsValidMonsterName(string name) => validMonsterClasses.Contains(name);
        public bool IsValidMonsterCode(string name) => validMonsterCodes.Contains(name);

        public int GetJobIdForName(string name) => jobNameToIdTable.GetValueOrDefault(name, -1);
        public string GetSkillName(CharacterSkill skill) => skillData.TryGetValue(skill, out var skOut) ? skOut.Name : "";
        public SkillData GetSkillData(CharacterSkill skill) => skillData[skill];
        public SkillTarget GetSkillTarget(CharacterSkill skill) => skillData.TryGetValue(skill, out var target) ? target.Target : SkillTarget.Any;
        public Dictionary<CharacterSkill, SkillData> GetAllSkills() => skillData;
        public ClientSkillTree GetSkillTree(int jobId) => jobSkillTrees.GetValueOrDefault(jobId);

        public MapViewpoint GetMapViewpoint(string mapName) => mapViewpoints.GetValueOrDefault(mapName);
        public MonsterClassData GetMonsterData(int classId) => monsterClassLookup.GetValueOrDefault(classId);

        public string GetHitSoundForWeapon(int weaponId)
        {
            if (!weaponClassData.TryGetValue(weaponId, out var weapon))
                weapon = weaponClassData[0];

            var hitSoundsCount = weapon.HitSounds.Count;
            if (hitSoundsCount <= 1)
                return weapon.HitSounds[0];

            return weapon.HitSounds[Random.Range(0, hitSoundsCount)];
        }

        public bool GetUniqueAction(string spriteName, CharacterSkill skill, out UniqueAttackAction actOut)
        {
            actOut = null;
            if (uniqueSpriteActions.TryGetValue(spriteName, out var list))
                if (list.TryGetValue(skill, out var action))
                    actOut = action;
            return actOut != null;
        }

        private void Awake()
        {
            Initialize();
        }

        private void Initialize()
        {
            Instance = this;
            var entityData = JsonUtility.FromJson<DatabaseMonsterClassData>(MonsterClassData.text);
            foreach (var m in entityData.MonsterClassData)
            {
                monsterClassLookup.Add(m.Id, m);
                validMonsterClasses.Add(m.Name);
                validMonsterCodes.Add(m.Code);
            }

            var headData = JsonUtility.FromJson<Wrapper<PlayerHeadData>>(PlayerHeadData.text);
            foreach (var h in headData.Items)
            {
                playerHeadLookup.Add(h.Id, h);
            }

            var playerData = JsonUtility.FromJson<Wrapper<PlayerClassData>>(PlayerClassData.text);
            foreach (var p in playerData.Items)
            {
                playerClassLookup.Add(p.Id, p);
                jobNameToIdTable.Add(p.Name, p.Id);
            }

            //split weapon entries into a tree of base job -> weapon class -> sprite list
            var weaponData = JsonUtility.FromJson<Wrapper<PlayerWeaponData>>(PlayerWeaponData.text);
            foreach (var weapon in weaponData.Items)
            {
                if (!playerWeaponLookup.ContainsKey(weapon.Job))
                    playerWeaponLookup.Add(weapon.Job, new Dictionary<int, List<PlayerWeaponData>>());

                var jList = playerWeaponLookup[weapon.Job];
                if (!jList.ContainsKey(weapon.Class))
                    jList.Add(weapon.Class, new List<PlayerWeaponData>());

                var cList = jList[weapon.Class];
                cList.Add(weapon);
            }

            var weaponClass = JsonUtility.FromJson<Wrapper<WeaponClassData>>(WeaponClassData.text);
            foreach (var weapon in weaponClass.Items)
                weaponClassData.TryAdd(weapon.Id, weapon);

            var uniqueAttacks = JsonUtility.FromJson<Wrapper<UniqueAttackAction>>(UniqueAttackActionData.text);
            foreach (var action in uniqueAttacks.Items)
            {
                if (!uniqueSpriteActions.TryGetValue(action.Sprite, out var list))
                {
                    list = new Dictionary<CharacterSkill, UniqueAttackAction>();
                    uniqueSpriteActions.Add(action.Sprite, list);
                }

                if (Enum.TryParse(action.Action, out CharacterSkill skill))
                    list.Add(skill, action);
                else
                    Debug.LogWarning($"Could not convert {action.Action} to a skill type when parsing unique skill actions");
            }

            var skills = JsonUtility.FromJson<Wrapper<SkillData>>(SkillData.text);
            foreach (var skill in skills.Items)
                skillData.Add(skill.SkillId, skill);

            var trees = JsonUtility.FromJson<Wrapper<ClientSkillTree>>(SkillTreeData.text);
            foreach (var tree in trees.Items)
                jobSkillTrees.Add(tree.ClassId, tree);


            foreach (var mapDef in MapViewpointData.text.Split("\r\n"))
            {
                var s = mapDef.Split(',');
                if (s.Length < 9 || s[0] == "map")
                    continue;
                mapViewpoints.Add(s[0], new MapViewpoint()
                {
                    MapName = s[0],
                    ZoomMin = int.Parse(s[1]),
                    ZoomDist = int.Parse(s[2]),
                    ZoomIn = int.Parse(s[3]),
                    SpinMin = int.Parse(s[4]),
                    SpinMax = int.Parse(s[5]),
                    SpinIn = int.Parse(s[6]),
                    HeightMin = int.Parse(s[7]),
                    HeightMax = int.Parse(s[8]),
                    HeightIn = int.Parse(s[9]),
                });
            }

            isInitialized = true;
        }

        private List<RoSpriteAnimator> tempList;

        public void ChangePlayerClass(ServerControllable player, ref PlayerSpawnParameters param)
        {
            if (!isInitialized)
                throw new Exception($"We shouldn't be changing the player class while not initialized!");

            var pData = playerClassLookup[0]; //novice
            if (playerClassLookup.TryGetValue(param.ClassId, out var lookupData))
                pData = lookupData;
            else
                Debug.LogWarning("Failed to find player with id of " + param.ClassId);

            var hData = playerHeadLookup[0]; //default;
            if (playerHeadLookup.TryGetValue(param.HeadId, out var lookupData2))
                hData = lookupData2;
            else
                Debug.LogWarning("Failed to find player head with id of " + param.ClassId);

            var bodySprite = player.SpriteAnimator;

            //find the head child, discard everything else
            if (tempList == null)
                tempList = new List<RoSpriteAnimator>();
            else
                tempList.Clear();

            RoSpriteAnimator headSprite = null;

            for (var i = 0; i < bodySprite.ChildrenSprites.Count; i++)
            {
                var type = bodySprite.ChildrenSprites[i].Type;
                switch (type)
                {
                    case SpriteType.Head:
                        headSprite = bodySprite.ChildrenSprites[i];
                        tempList.Add(headSprite);
                        break;
                    case SpriteType.Headgear:
                        tempList.Add(bodySprite.ChildrenSprites[i]);
                        break;
                    default:
                        Destroy(bodySprite.ChildrenSprites[i].gameObject);
                        break;
                }
            }

            if (headSprite == null)
                throw new Exception($"Existing player has no head!");

            bodySprite.ChildrenSprites.Clear();
            for (var i = 0; i < tempList.Count; i++)
                bodySprite.ChildrenSprites.Add(tempList[i]);
        }

        public ServerControllable InstantiatePlayer(ref PlayerSpawnParameters param)
        {
            if (!isInitialized)
                Initialize();

            var pData = playerClassLookup[0]; //novice
            if (playerClassLookup.TryGetValue(param.ClassId, out var lookupData))
                pData = lookupData;
            else
                Debug.LogWarning("Failed to find player with id of " + param.ClassId);

            var hData = playerHeadLookup[0]; //default;
            if (playerHeadLookup.TryGetValue(param.HeadId, out var lookupData2))
                hData = lookupData2;
            else
                Debug.LogWarning("Failed to find player head with id of " + param.ClassId);


            var go = new GameObject(pData.Name);
            go.layer = LayerMask.NameToLayer("Characters");
            go.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
            var control = go.AddComponent<ServerControllable>();
            var billboard = go.AddComponent<BillboardObject>();
            billboard.Style = BillboardStyle.Character;
            
            var body = new GameObject("Sprite");
            body.layer = LayerMask.NameToLayer("Characters");
            body.transform.SetParent(go.transform, false);
            body.transform.localPosition = Vector3.zero;
            body.AddComponent<SortingGroup>();

            var head = new GameObject("Head");
            head.layer = LayerMask.NameToLayer("Characters");
            head.transform.SetParent(body.transform, false);
            head.transform.localPosition = Vector3.zero;

            var bodySprite = body.AddComponent<RoSpriteAnimator>();
            var headSprite = head.AddComponent<RoSpriteAnimator>();

            control.ClassId = param.ClassId;
            control.SpriteAnimator = bodySprite;
            control.CharacterType = CharacterType.Player;
            control.SpriteMode = ClientSpriteType.Sprite;
            control.IsAlly = true;
            control.IsMainCharacter = param.IsMainCharacter;
            control.IsMale = param.IsMale;
            control.Level = param.Level;
            control.WeaponClass = param.WeaponClass;

            bodySprite.Controllable = control;
            if (param.State == CharacterState.Moving)
                bodySprite.ChangeMotion(SpriteMotion.Walk);
            bodySprite.ChildrenSprites.Add(headSprite);
            //bodySprite.SpriteOffset = 0.5f;
            bodySprite.HeadFacing = param.HeadFacing;

            if (param.State == CharacterState.Sitting)
                bodySprite.State = SpriteState.Sit;
            if (param.State == CharacterState.Moving)
                bodySprite.State = SpriteState.Walking;

            headSprite.Parent = bodySprite;
            headSprite.SpriteOrder = 1;

            control.ShadowSize = 0.5f;
            control.WeaponClass = param.WeaponClass;

            var bodySpriteName = param.IsMale ? pData.SpriteMale : pData.SpriteFemale;
            var headSpriteName = param.IsMale ? hData.SpriteMale : hData.SpriteFemale;

            Debug.Log($"Instantiate player sprite with job {param.ClassId} weapon {param.WeaponClass}");

            PlayerWeaponData weapon = null;

            if (playerWeaponLookup.TryGetValue(param.ClassId, out var weaponsByJob))
            {
                if (weaponsByJob.TryGetValue(param.WeaponClass, out var weapons) && weapons.Count > 0)
                {
                    weapon = weapons[0]; //cheeeat, will need to change when we can have multiple sprites per weapon type
                }
            }

            if (weapon != null)
            {
                LoadAndAttachWeapon(go, body.transform, bodySprite, weapon, false, param.IsMale);
                LoadAndAttachWeapon(go, body.transform, bodySprite, weapon, true, param.IsMale);
            }

            control.ConfigureEntity(param.ServerId, param.Position, param.Facing);
            control.Name = param.Name;
            control.Hp = param.Hp;
            control.MaxHp = param.MaxHp;
            // control.EnsureFloatingDisplayCreated().SetUp(param.Name, param.MaxHp, 0);

            AddressableUtility.LoadRoSpriteData(go, bodySpriteName, bodySprite.OnSpriteDataLoad);
            AddressableUtility.LoadRoSpriteData(go, headSpriteName, headSprite.OnSpriteDataLoad);
            AddressableUtility.LoadSprite(go, "shadow", control.AttachShadow);

            if (control.IsMainCharacter)
            {
                CameraFollower.Instance.CharacterJob.text = pData.Name;
                CameraFollower.Instance.CharacterName.text = $"Lv. {control.Level} {control.Name}";
            }

            control.Init();

            return control;
        }

        private void LoadAndAttachWeapon(GameObject parent, Transform bodyTransform, RoSpriteAnimator bodySprite, PlayerWeaponData weapon, bool isEffect,
            bool isMale)
        {
            var weaponSpriteFile = isMale ? weapon.SpriteMale : weapon.SpriteFemale;
            if (isEffect)
                weaponSpriteFile = isMale ? weapon.EffectMale : weapon.EffectFemale;

            if (string.IsNullOrEmpty(weaponSpriteFile))
            {
                Debug.Log(
                    $"Not loading sprite for weapon as the requested weapon class does not have one. (GO: {parent} Sprite: {bodySprite?.SpriteData?.Name})");
                return;
            }

            var weaponObj = new GameObject("Weapon");
            weaponObj.layer = LayerMask.NameToLayer("Characters");
            weaponObj.transform.SetParent(bodyTransform, false);
            weaponObj.transform.localPosition = Vector3.zero;

            var weaponSprite = weaponObj.AddComponent<RoSpriteAnimator>();

            weaponSprite.Parent = bodySprite;
            weaponSprite.SpriteOrder = 2;
            if (isEffect)
                weaponSprite.SpriteOrder = 20;

            bodySprite.PreferredAttackMotion = isMale ? weapon.AttackMale : weapon.AttackFemale;
            bodySprite.ChildrenSprites.Add(weaponSprite);

            AddressableUtility.LoadRoSpriteData(parent, weaponSpriteFile, weaponSprite.OnSpriteDataLoad);
        }

        public void AttachEmote(GameObject target, int emoteId)
        {
            var go = new GameObject("Emote");
            //go.layer = LayerMask.NameToLayer("Characters");
            var billboard = go.AddComponent<BillboardObject>();
            billboard.Style = BillboardStyle.Character;

            var emote = go.AddComponent<EmoteController>();
            emote.AnimationId = emoteId;
            emote.Target = target;

            var child = new GameObject("Sprite");
            //child.layer = LayerMask.NameToLayer("Characters");
            child.transform.SetParent(go.transform, false);
            child.transform.localPosition = Vector3.zero;
            child.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);

            var sr = child.AddComponent<RoSpriteRendererStandard>();
            sr.SecondPassForWater = false;
            sr.UpdateAngleWithCamera = false;

            var sprite = child.AddComponent<RoSpriteAnimator>();
            emote.RoSpriteAnimator = sprite;
            sprite.Type = SpriteType.Npc;
            sprite.State = SpriteState.Dead;
            //sprite.LockAngle = true;
            sprite.SpriteRenderer = sr;

            AddressableUtility.LoadRoSpriteData(go, "Assets/Sprites/emotion.spr", emote.OnFinishLoad);
        }

        private ServerControllable PrefabMonster(MonsterClassData mData, ref MonsterSpawnParameters param)
        {
            var prefabName = mData.SpriteName;

            var obj = new GameObject(prefabName);

            var control = obj.AddComponent<ServerControllable>();
            control.ClassId = param.ClassId;
            control.CharacterType = CharacterType.NPC;
            control.SpriteMode = ClientSpriteType.Prefab;
            control.EntityObject = obj;
            control.Level = param.Level;
            control.Name = param.Name;
            control.IsAlly = true;
            control.IsInteractable = false;

            control.ConfigureEntity(param.ServerId, param.Position, param.Facing);
            control.EnsureFloatingDisplayCreated().SetUp(param.Name, param.MaxHp, 0, false, false);

            var loader = Addressables.LoadAssetAsync<GameObject>(prefabName);
            loader.Completed += ah =>
            {
                if (obj != null && obj.activeInHierarchy)
                {
                    var obj2 = GameObject.Instantiate(ah.Result, obj.transform, false);
                    obj2.transform.localPosition = Vector3.zero;

                    var sprite = obj2.GetComponent<RoSpriteAnimator>();
                    if (sprite != null)
                        sprite.Controllable = control;
                }
            };

            control.Init();

            return control;
        }

        public ServerControllable InstantiateMonster(ref MonsterSpawnParameters param, CharacterType entityType)
        {
            if (!isInitialized)
                Initialize();

            var mData = monsterClassLookup[4000]; //poring
            if (monsterClassLookup.TryGetValue(param.ClassId, out var lookupData))
                mData = lookupData;
            else
                Debug.LogWarning("Failed to find monster with id of " + param.ClassId);

            if (mData.SpriteName.Contains(".prefab"))
                return PrefabMonster(mData, ref param);

            var go = new GameObject(mData.Name);
            go.layer = LayerMask.NameToLayer("Characters");
            go.transform.localScale = new Vector3(1.5f * mData.Size, 1.5f * mData.Size, 1.5f * mData.Size);
            var control = go.AddComponent<ServerControllable>();
            // if (param.ClassId < 4000)
            //     control.CharacterType = CharacterType.NPC;
            // else
            //     control.CharacterType = CharacterType.Monster;
            control.ClassId = param.ClassId;
            control.CharacterType = entityType;
            control.SpriteMode = ClientSpriteType.Sprite;
            control.IsInteractable = param.Interactable;
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
            control.ShadowSize = mData.ShadowSize;
            control.IsAlly = false;
            control.Level = param.Level;
            control.Name = param.Name;
            if (string.IsNullOrEmpty(param.Name))
                control.Name = mData.Name;
            control.WeaponClass = 0;
            control.Hp = param.Hp;
            control.MaxHp = param.MaxHp;
            if (ColorUtility.TryParseHtmlString(mData.Color, out var color))
                sprite.BaseColor = color;

            control.ConfigureEntity(param.ServerId, param.Position, param.Facing);
            // control.EnsureFloatingDisplayCreated().SetUp(param.Name, param.MaxHp, 0);

            var basePath = "Assets/Sprites/Monsters/";
            if (param.ClassId < 4000)
                basePath = "Assets/Sprites/Npcs/";


            AddressableUtility.LoadRoSpriteData(go, basePath + mData.SpriteName, control.SpriteAnimator.OnSpriteDataLoad);
            if (mData.ShadowSize > 0)
                AddressableUtility.LoadSprite(go, "shadow", control.AttachShadow);

            control.Init();

            return control;
        }
    }
}