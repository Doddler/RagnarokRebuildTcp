using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Effects;
using Assets.Scripts.Network;
using Assets.Scripts.Utility;
using RebuildSharedData.ClientTypes;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering;

namespace Assets.Scripts.Sprites
{
	public struct PlayerSpawnParameters
    {
		public int ServerId;
		public int ClassId;
		public int HeadId;
		public HeadFacing HeadFacing;
		public Direction Facing;
		public CharacterState State;
		public Vector2Int Position;
		public bool IsMale;
		public bool IsMainCharacter;
		public int Level;
        public string Name;
        public int Hp;
        public int MaxHp;
        public int WeaponClass;
    }

	public struct MonsterSpawnParameters
	{
		public int ServerId;
		public int ClassId;
        public string Name;
		public Direction Facing;
		public CharacterState State;
		public Vector2Int Position;
        public int Level;
        public int Hp;
        public int MaxHp;
        public bool Interactable;
    }

	public class SpriteDataLoader : MonoBehaviour
	{
		public static SpriteDataLoader Instance;

		public TextAsset MonsterClassData;
		public TextAsset PlayerClassData;
		public TextAsset PlayerHeadData;
		public TextAsset PlayerWeaponData;

        private readonly Dictionary<int, MonsterClassData> monsterClassLookup = new();
		private readonly Dictionary<int, PlayerHeadData> playerHeadLookup = new();
		private readonly Dictionary<int, PlayerClassData> playerClassLookup = new();
		private readonly Dictionary<int, Dictionary<int, List<PlayerWeaponData>>> playerWeaponLookup = new();

        private bool isInitialized;
		
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
			}

            //split weapon entries into a tree of base job -> weapon class -> sprite list
            var weaponData = JsonUtility.FromJson<Wrapper<PlayerWeaponData>>(PlayerWeaponData.text);
            foreach (var weapon in weaponData.Items)
            {
				if(!playerWeaponLookup.ContainsKey(weapon.Job))
					playerWeaponLookup.Add(weapon.Job, new Dictionary<int, List<PlayerWeaponData>>());

				var jList = playerWeaponLookup[weapon.Job];
				if(!jList.ContainsKey(weapon.Class))
					jList.Add(weapon.Class, new List<PlayerWeaponData>());

				var cList = jList[weapon.Class];
				cList.Add(weapon);
            }

			isInitialized = true;
		}

   //     private GameObject CloneAnimatorForTrail(RoSpriteAnimator src, RoSpriteTrail trail, int order)
   //     {
   //         if (!src.IsInitialized)
   //             return null;

   //         var go = new GameObject(src.gameObject.name);
   //         var mf = go.AddComponent<MeshFilter>();
   //         var mr = go.AddComponent<MeshRenderer>();

   //         var sr = src.SpriteRenderer as RoSpriteRendererStandard;
   //         if (sr == null)
   //             throw new Exception("Cannot CloneAnimatorForTrail as it is not using a RoSpriteRendererStandard.");

			//if(order > 0)
   //             mr.sortingOrder = order;

   //         mr.receiveShadows = false;
   //         mr.lightProbeUsage = LightProbeUsage.Off;
   //         mr.shadowCastingMode = ShadowCastingMode.Off;

			//trail.Renderers.Add(mr);

			//mf.mesh = sr.GetMeshForFrame();

   //         var mats = new Material[src.MeshRenderer.sharedMaterials.Length];

   //         for (var i = 0; i < src.MeshRenderer.sharedMaterials.Length; i++)
   //         {
   //             var srcMat = src.MeshRenderer.sharedMaterials[i];

   //             var shader = srcMat.shader;
			//	//Debug.Log(shader);
   //             var mat = new Material(shader);
   //             //Debug.Log(mat);
			//	mat.shader = shader;
   //             mat.mainTexture = srcMat.mainTexture;
   //             mat.renderQueue = srcMat.renderQueue;
   //             mat.shaderKeywords = srcMat.shaderKeywords;


			//	mats[i] = mat;
			//}

   //         mr.sharedMaterials = mats;

			////Debug.Log(mr.material);

   //         return go;
   //     }
		
   //     public void CloneObjectForTrail(RoSpriteAnimator src)
   //     {
   //         if (!isInitialized)
   //             Initialize();

			//if(src.Parent != null)
			//	Debug.LogError("Cannot clone sprite animator for trail as it is not the parent animator!");

   //         var go = new GameObject("Trail");
   //         go.layer = LayerMask.NameToLayer("Characters");
   //         go.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
   //         go.transform.position = src.transform.position;
   //         var bb =go.AddComponent<Billboard>();

   //         var sr = src.SpriteRenderer as RoSpriteRendererStandard;
   //         if (sr == null)
   //             throw new Exception("Cannot CloneAnimatorForTrail as it is not using a RoSpriteRendererStandard.");
			
   //         var trail = go.AddComponent<RoSpriteTrail>();
   //         trail.Color = sr.Color;
   //         trail.Duration = 0.6f;
   //         trail.LifeTime = 0.5f;
   //         trail.StartTime = 0.49f;
   //         trail.Renderers = new List<MeshRenderer>();

			//var main = CloneAnimatorForTrail(src, trail, 0);
			//main.transform.SetParent(go.transform);
   //         main.transform.localPosition = src.transform.localPosition + new Vector3(0, 0, 0.05f);
   //         main.transform.localScale = src.transform.localScale;
   //         trail.SortingGroup = main.AddComponent<SortingGroup>();
			
			//var order = 1;

   //         foreach (var c in src.ChildrenSprites)
   //         {
   //             var sub = CloneAnimatorForTrail(c, trail, order);
   //             if (sub == null)
   //                 continue;
			//	sub.transform.SetParent(main.transform);
   //             sub.transform.localScale = c.transform.localScale;
			//	sub.transform.localPosition = c.transform.localPosition;

   //             order++;
   //         }

			////call lateupdate directly in case we are too late to update in time
			//bb.LateUpdate();
			//trail.Init();
   //     }
		
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
		    var billboard = go.AddComponent<Billboard>();
            billboard.Style = Billboard.BillboardStyle.Character;

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

			control.SpriteAnimator = bodySprite;
			control.CharacterType = CharacterType.Player;
			control.SpriteMode = ClientSpriteType.Sprite;
			control.IsAlly = true;
            control.IsMainCharacter = param.IsMainCharacter;
			control.IsMale = param.IsMale;
            control.Level = param.Level;

			bodySprite.Controllable = control;
			if(param.State == CharacterState.Moving)
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

            AddressableUtility.LoadRoSpriteData(go, bodySpriteName, bodySprite.OnSpriteDataLoad);
			AddressableUtility.LoadRoSpriteData(go, headSpriteName, headSprite.OnSpriteDataLoad);
			AddressableUtility.LoadSprite(go, "shadow", control.AttachShadow);

			return control;
		}

        private void LoadAndAttachWeapon(GameObject parent, Transform bodyTransform, RoSpriteAnimator bodySprite, PlayerWeaponData weapon, bool isEffect, bool isMale)
        {
            var weaponSpriteFile = isMale ? weapon.SpriteMale : weapon.SpriteFemale;
            if (isEffect)
				weaponSpriteFile = isMale ? weapon.EffectMale : weapon.EffectFemale;

            if (string.IsNullOrEmpty(weaponSpriteFile))
            {
                Debug.Log($"Not loading sprite for weapon as the requested weapon class does not have one. (GO: {parent} Sprite: {bodySprite?.SpriteData?.Name})");
                return;
            }

            var weaponObj = new GameObject("Weapon");
            weaponObj.layer = LayerMask.NameToLayer("Characters");
            weaponObj.transform.SetParent(bodyTransform, false);
            weaponObj.transform.localPosition = Vector3.zero;

            var weaponSprite = weaponObj.AddComponent<RoSpriteAnimator>();

            weaponSprite.Parent = bodySprite;
            weaponSprite.SpriteOrder = 2;
			if(isEffect)
                weaponSprite.SpriteOrder = 20;

            bodySprite.PreferredAttackMotion = isMale ? weapon.AttackMale : weapon.AttackFemale;
            bodySprite.ChildrenSprites.Add(weaponSprite);

            AddressableUtility.LoadRoSpriteData(parent, weaponSpriteFile, weaponSprite.OnSpriteDataLoad);
        }

        public void AttachEmote(GameObject target, int emoteId)
        {
            var go = new GameObject("Emote");
            //go.layer = LayerMask.NameToLayer("Characters");
            var billboard = go.AddComponent<Billboard>();
            billboard.Style = Billboard.BillboardStyle.Character;

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
			control.CharacterType = CharacterType.NPC;
			control.SpriteMode = ClientSpriteType.Prefab;
			control.EntityObject = obj;
            control.Level = param.Level;
            control.Name = param.Name;
            control.IsAlly = true;
            control.IsInteractable = false;
			
			control.ConfigureEntity(param.ServerId, param.Position, param.Facing);
			
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


            return control;
		}

		public ServerControllable InstantiateMonster(ref MonsterSpawnParameters param)
		{
			if(!isInitialized)
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
			if(param.ClassId < 4000)
			    control.CharacterType = CharacterType.NPC;
			else
			    control.CharacterType = CharacterType.Monster;
			control.SpriteMode = ClientSpriteType.Sprite;
            control.IsInteractable = param.Interactable;
            var billboard = go.AddComponent<Billboard>();
            billboard.Style = Billboard.BillboardStyle.Character;

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

			control.ConfigureEntity(param.ServerId, param.Position, param.Facing);

            var basePath = "Assets/Sprites/Monsters/";
			if(param.ClassId < 4000)
                basePath = "Assets/Sprites/Npcs/";


			AddressableUtility.LoadRoSpriteData(go, basePath + mData.SpriteName, control.SpriteAnimator.OnSpriteDataLoad);
			if (mData.ShadowSize > 0)
				AddressableUtility.LoadSprite(go, "shadow", control.AttachShadow);

			return control;
		}
	}
}