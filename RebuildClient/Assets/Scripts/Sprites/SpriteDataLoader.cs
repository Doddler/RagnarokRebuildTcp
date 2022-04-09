using System.Collections.Generic;
using System.Linq;
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

		private Dictionary<int, MonsterClassData> monsterClassLookup = new Dictionary<int, MonsterClassData>();
		private Dictionary<int, PlayerHeadData> playerHeadLookup = new Dictionary<int, PlayerHeadData>();
		private Dictionary<int, PlayerClassData> playerClassLookup = new Dictionary<int, PlayerClassData>();

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

			var headData = JsonUtility.FromJson<DatabasePlayerHeadData>(PlayerHeadData.text);
			foreach (var h in headData.PlayerHeadData)
			{
				playerHeadLookup.Add(h.Id, h);
			}
			
			var playerData = JsonUtility.FromJson<DatabasePlayerClassData>(PlayerClassData.text);
			foreach (var p in playerData.PlayerClassData)
			{
				playerClassLookup.Add(p.Id, p);
			}

			isInitialized = true;
		}

        private GameObject CloneAnimatorForTrail(RoSpriteAnimator src, RoSpriteTrail trail, int order)
        {
            if (!src.IsInitialized)
                return null;

            var go = new GameObject(src.gameObject.name);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();

			if(order > 0)
                mr.sortingOrder = order;

            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.shadowCastingMode = ShadowCastingMode.Off;

			trail.Renderers.Add(mr);

			mf.mesh = src.GetMeshForFrame();

            var mats = new Material[src.MeshRenderer.sharedMaterials.Length];

            for (var i = 0; i < src.MeshRenderer.sharedMaterials.Length; i++)
            {
                var srcMat = src.MeshRenderer.sharedMaterials[i];

                var shader = srcMat.shader;
				//Debug.Log(shader);
                var mat = new Material(shader);
                //Debug.Log(mat);
				mat.shader = shader;
                mat.mainTexture = srcMat.mainTexture;
                mat.renderQueue = srcMat.renderQueue;
                mat.shaderKeywords = srcMat.shaderKeywords;


				mats[i] = mat;
			}

            mr.sharedMaterials = mats;

			//Debug.Log(mr.material);

            return go;
        }
		
        public void CloneObjectForTrail(RoSpriteAnimator src)
        {
            if (!isInitialized)
                Initialize();

			if(src.Parent != null)
				Debug.LogError("Cannot clone sprite animator for trail as it is not the parent animator!");

            var go = new GameObject("Trail");
            go.layer = LayerMask.NameToLayer("Characters");
            go.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
            go.transform.position = src.transform.position;
            var bb =go.AddComponent<Billboard>();


            var trail = go.AddComponent<RoSpriteTrail>();
            trail.Color = src.CurrentColor;
            trail.Duration = 0.6f;
            trail.LifeTime = 0.5f;
            trail.StartTime = 0.49f;
            trail.Renderers = new List<MeshRenderer>();

			var main = CloneAnimatorForTrail(src, trail, 0);
			main.transform.SetParent(go.transform);
            main.transform.localPosition = src.transform.localPosition + new Vector3(0, 0, 0.05f);
            main.transform.localScale = src.transform.localScale;
            trail.SortingGroup = main.AddComponent<SortingGroup>();
			
			var order = 1;

            foreach (var c in src.ChildrenSprites)
            {
                var sub = CloneAnimatorForTrail(c, trail, order);
                if (sub == null)
                    continue;
				sub.transform.SetParent(main.transform);
                sub.transform.localScale = c.transform.localScale;
				sub.transform.localPosition = c.transform.localPosition;

                order++;
            }

			//call lateupdate directly in case we are too late to update in time
			bb.LateUpdate();
			trail.Init();
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
			go.AddComponent<Billboard>();

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
			control.IsMale = param.IsMale;
            control.Level = param.Level;

			bodySprite.Controllable = control;
			if(param.State == CharacterState.Moving)
				bodySprite.ChangeMotion(SpriteMotion.Walk);
			bodySprite.ChildrenSprites.Add(headSprite);
			bodySprite.SpriteOffset = 0.5f;
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
			
			if (param.ClassId == 0)
			{
				var weaponSpriteFile = param.IsMale ? "Assets/Sprites/Weapons/Novice/Male/초보자_남_1207.spr" : "Assets/Sprites/Weapons/Novice/Female/초보자_여_1207.spr";

				var weapon = new GameObject("Weapon");
				weapon.layer = LayerMask.NameToLayer("Characters");
				weapon.transform.SetParent(body.transform, false);
				weapon.transform.localPosition = Vector3.zero;

				var weaponSprite = weapon.AddComponent<RoSpriteAnimator>();
				
				weaponSprite.Parent = bodySprite;
				weaponSprite.SpriteOrder = 2;

				bodySprite.ChildrenSprites.Add(weaponSprite);

				AddressableUtility.LoadRoSpriteData(go, weaponSpriteFile, weaponSprite.OnSpriteDataLoad);
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

		private ServerControllable PrefabMonster(MonsterClassData mData, ref MonsterSpawnParameters param)
		{
			var prefabName = mData.SpriteName; //.Replace(".prefab", "");
			//var split = prefabName.Split('/');
			//prefabName = split.Last();

            var obj = new GameObject(prefabName);

            var loader = Addressables.LoadAssetAsync<GameObject>(prefabName);
            loader.Completed += ah =>
            {
                if (obj != null && obj.activeInHierarchy)
                {
                    var obj2 = GameObject.Instantiate(ah.Result, obj.transform, false);
                    obj2.transform.localPosition = Vector3.zero;
                    //ah.Result.transform.SetParent(obj.transform, false);
                }
            };

			//var res = Resources.Load<GameObject>(prefabName);
			//if(res == null)
   //             Debug.Log("Failed to load resource with name " + prefabName);
			
			//Debug.Log(prefabName);
			//var obj = GameObject.Instantiate(res);
			var control = obj.AddComponent<ServerControllable>();
			control.CharacterType = CharacterType.NPC;
			control.SpriteMode = ClientSpriteType.Prefab;
			control.EntityObject = obj;
            control.Level = param.Level;

			control.ConfigureEntity(param.ServerId, param.Position, param.Facing);

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
			go.AddComponent<Billboard>();

			var child = new GameObject("Sprite");
			child.layer = LayerMask.NameToLayer("Characters");
			child.transform.SetParent(go.transform, false);
			child.transform.localPosition = Vector3.zero;

			var sprite = child.AddComponent<RoSpriteAnimator>();
			sprite.Controllable = control;
			
			control.SpriteAnimator = sprite;
			control.SpriteAnimator.SpriteOffset = mData.Offset;
			control.ShadowSize = mData.ShadowSize;
			control.IsAlly = false;
            control.Level = param.Level;
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