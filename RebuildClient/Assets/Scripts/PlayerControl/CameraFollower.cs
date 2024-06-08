using System;
using System.Collections.Generic;
using Assets.Scripts.Effects;
using Assets.Scripts.MapEditor;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using Assets.Scripts.Sprites;
using Assets.Scripts.UI;
using Assets.Scripts.Utility;
using PlayerControl;
using RebuildSharedData.ClientTypes;
using RebuildSharedData.Config;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Utility;
using CursorMode = UnityEngine.CursorMode;
using Debug = UnityEngine.Debug;

#if UNITY_EDITOR
#endif

namespace Assets.Scripts
{
    public enum CameraMode
    {
        None,
        Normal,
        Indoor,
        Fixed
    }

    public class CameraFollower : MonoBehaviour
    {
        public GameObject ListenerProbe;
        public CursorManager CursorManager;

        // public List<Texture2D> NormalCursorAnimation;
        // //private float cursorAnimTime;
        // private int normalCursorFrame => Mathf.FloorToInt(Time.timeSinceLevelLoad * 12f) % NormalCursorAnimation.Count;
        //
        // //public Texture2D NormalCursorTexture;
        // public Texture2D AttackCursorTexture;
        // public Texture2D TalkCursorTexture;
        // public Texture2D TargetCursorTexture;
        // public Texture2D TargetCursorNoTargetTexture;
        public RoSpriteData TargetSprite;

        // public GameObject LevelUpPrefab;
        // public GameObject ResurrectPrefab;
        // public GameObject DeathPrefab;
        public TextAsset LevelChart;

        private static CameraFollower _instance;
        public RoWalkDataProvider WalkProvider;

        public Canvas UiCanvas;

        //public TextMeshProUGUI TargetUi;
        //public TextMeshProUGUI PlayerTargetUi;
        public TextMeshProUGUI HpDisplay;
        public Slider HpSlider;
        public TextMeshProUGUI ExpDisplay;
        public Slider ExpSlider;
        public TMP_InputField TextBoxInputField;
        public CanvasScaler CanvasScaler;
        public TextMeshProUGUI ErrorNoticeUi;
        public TextMeshProUGUI DebugDisplay;

        public ScrollRect TextBoxScrollRect;
        public TextMeshProUGUI TextBoxText;

        public Camera WaterCamera;
        public RenderTexture WaterTexture;
        public RenderTexture WaterDepthTexture;
        public Shader WaterDepthShader;

        private Texture2D currentCursor;

        private Vector2Int[] tempPath;

        private string lastMessage;

        private int lastWidth;
        private int lastHeight;

        private Vector2Int lastTile;
        private bool lastPathValid;
        private bool noHold = false;
        private bool hasSelection;
        public ServerControllable SelectedTarget;
        private GameObject selectedSprite;
        private string targetText;
        private GameObject clickEffectPrefab;

        public GameObject WarpPanel;
        public GameObject DialogPanel;
        public GameObject NpcOptionPanel;
        public GameObject EmotePanel;

        public bool UseTTFDamage = false;
        public bool IsInNPCInteraction = false;

        private int[] levelReqs = new int[100];

        public Dictionary<string, int> EffectIdLookup;
        public Dictionary<int, EffectTypeEntry> EffectList;
        public Dictionary<int, GameObject> EffectCache;

        private bool hasSkillOnCursor;
        private CharacterSkill cursorSkill;
        private SkillTarget cursorSkillTarget;
        private int cursorSkillLvl = 5;
        private int cursorMaxSkillLvl = 10;
        private float skillScroll = 5f;
        private bool cursorShowSkillLevel = true;

        public bool CinemachineMode;
        public VideoRecorder Recorder;

        private const bool CinemachineCenterPlayerOnMap = true;
        private const bool CinemachineHidePlayerObject = true;
        public bool IsInErrorState;
        public bool DebugVisualization;
        public bool DebugIgnoreAttackMotion;

        public float FogNearRatio = 0.3f;
        public float FogFarRatio = 4f;


#if DEBUG
        private const float MaxClickDistance = 500;

#else
        private const float MaxClickDistance = 150;
#endif

        public int ExpForLevel(int lvl) => levelReqs[Mathf.Clamp(lvl, 1, 99)];

        public static CameraFollower Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;
                var cam = GameObject.FindObjectOfType<CameraFollower>();
                if (cam != null)
                {
                    _instance = cam;
                    return _instance;
                }

                var mc = Camera.main;
                if (mc == null)
                    return null;

                cam = mc.GetComponent<CameraFollower>();
                if (cam != null)
                {
                    _instance = cam;
                    return _instance;
                }

                cam = mc.gameObject.AddComponent<CameraFollower>();
                _instance = cam;
                return _instance;
            }
        }

        public GameObject Target;
        public GameObject OverrideTarget;
        public Camera Camera;
        public Vector3 CurLookAt;

        private ServerControllable controllable;
        public ServerControllable TargetControllable => controllable;

        private ServerControllable mouseHoverTarget;

        public Vector3 MoveTo;

        public Vector3 TargetFollow;

        //private EntityWalkable targetWalkable;

        public List<Vector2Int> MovePath;
        public float MoveSpeed;
        public float MoveProgress;
        public Vector3 StartPos;

        public float TargetRotation;

        public float ClickDelay;

        public float Rotation;
        public float Distance;
        public float Height;

        public float TurnSpeed;

        public int MonSpawnCount = 1;

        public TextAsset EffectConfigFile;

        public float LastRightClick;
        private bool isHolding;

        private CameraMode cameraMode;
        private bool lockCamera;
        private Vector2 rotationRange;
        private Vector2 heightRange;
        private Vector2 zoomRange = new Vector2(30, 70);

        public void ResetCursor() => isHolding = false;

        public void Awake()
        {
            _instance = this;

            //CurLookAt = Target.transform.position;
            TargetFollow = CurLookAt;
            Camera = GetComponent<Camera>();
            //MoveTo = Target.transform.position;

            WalkProvider = GameObject.FindObjectOfType<RoWalkDataProvider>();

            UpdateCameraSize();

            Physics.queriesHitBackfaces = true;

            var effects = JsonUtility.FromJson<EffectTypeList>(EffectConfigFile.text);
            EffectList = new Dictionary<int, EffectTypeEntry>();
            EffectIdLookup = new Dictionary<string, int>();
            EffectCache = new Dictionary<int, GameObject>();
            foreach (var e in effects.Effects)
            {
                var asset = e.PrefabName;
                if (string.IsNullOrEmpty(asset) || e.ImportEffect)
                    asset = $"Assets/Effects/Prefabs/{e.Name}.prefab";

                e.PrefabName = asset; //yeah we're modifying the list after loading it but... probably fine

                EffectList.Add(e.Id, e);
                EffectIdLookup.Add(e.Name, e.Id);
            }

            var lines = LevelChart.text.Split("\n"); //we'll trim out \r after if it exists
            for (var i = 0; i < 99; i++)
                levelReqs[i] = int.Parse(lines[i].Trim());

            LevelChart = null; //don't need to hold this anymore

            DialogPanel.GetComponent<DialogWindow>().HideUI();

            if (Recorder != null)
                Recorder.gameObject.SetActive(false);

            clickEffectPrefab = Resources.Load<GameObject>($"MoveNotice");

            LayoutRebuilder.ForceRebuildLayoutImmediate(UiCanvas.transform as RectTransform);

            //targetWalkable = Target.GetComponent<EntityWalkable>();
            //if (targetWalkable == null)
            //    targetWalkable = Target.AddComponent<EntityWalkable>();

            //DoMapSpawn();
        }

        private void SaveCurrentCameraSettings()
        {
            if (cameraMode == CameraMode.Normal)
            {
                PlayerPrefs.SetFloat("cameraX", TargetRotation);
                PlayerPrefs.SetFloat("cameraY", Height);
            }

            if (cameraMode == CameraMode.Indoor)
            {
                PlayerPrefs.SetFloat("cameraIndoorX", TargetRotation);
                PlayerPrefs.SetFloat("cameraIndoorY", Height);
            }
        }

        public void SetCameraViewpoint(MapViewpoint viewpoint)
        {
            cameraMode = CameraMode.Fixed;
            TargetRotation = Rotation = viewpoint.SpinIn;
            Height = viewpoint.HeightIn;
            Distance = viewpoint.ZoomIn;
            rotationRange = new Vector2(viewpoint.SpinMin, viewpoint.SpinMax);
            heightRange = new Vector2(viewpoint.HeightMin, viewpoint.HeightMax);
            zoomRange = new Vector2(viewpoint.ZoomMin, viewpoint.ZoomMin + viewpoint.ZoomDist);
            lockCamera = true;
        }

        public void SetCameraMode(CameraMode mode)
        {
            if (mode == cameraMode)
                return;

            if (mode == CameraMode.Fixed || mode == CameraMode.None)
            {
                Debug.LogError($"You can't use this function to set camera mode to {mode}");
                return;
            }

            SaveCurrentCameraSettings();
            cameraMode = mode;

            if (mode == CameraMode.Normal)
            {
                Rotation = PlayerPrefs.GetFloat("cameraX", 0);
                Height = PlayerPrefs.GetFloat("cameraY", 50);
                TargetRotation = Rotation;
                Distance = 60;
                zoomRange = new Vector2(30, 90);
                lockCamera = false;
            }

            if (mode == CameraMode.Indoor)
            {
                Rotation = PlayerPrefs.GetFloat("cameraIndoorX", 45);
                Height = PlayerPrefs.GetFloat("cameraIndoorY", 55);
                TargetRotation = Rotation;
                lockCamera = true;
                Distance = 55;
                rotationRange = new Vector2(40, 60);
                heightRange = new Vector2(35, 65);
                zoomRange = new Vector2(25, 80);
            }

#if UNITY_EDITOR
            zoomRange = new Vector2(30, 150);
#endif
        }

        public void UpdatePlayerHP(int hp, int maxHp)
        {
            if (hp < 0)
                hp = 0;

            HpDisplay.gameObject.SetActive(true);
            HpDisplay.text = $"HP: {hp}/{maxHp}";
            HpSlider.value = (float)hp / (float)maxHp;
        }

        public void UpdatePlayerExp(int exp, int maxExp)
        {
            if (maxExp <= 0)
            {
                ExpDisplay.text = "";
                ExpSlider.gameObject.SetActive(false);
                return;
            }

            var percent = exp / (float)maxExp;

            ExpSlider.gameObject.SetActive(true);
            ExpDisplay.text = $"Exp: {exp}/{maxExp} ({percent * 100f:F1}%)";
            ExpSlider.value = percent;
        }


        private GameObject CreateSelectedCursorObject()
        {
            var go = new GameObject("Cursor");
            go.layer = LayerMask.NameToLayer("Characters");
            go.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
            go.AddComponent<BillboardObject>();

            var cursor = new GameObject("Cursor");
            cursor.layer = LayerMask.NameToLayer("Characters");
            cursor.transform.SetParent(go.transform, false);
            cursor.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            cursor.AddComponent<SortingGroup>();

            var sprite = cursor.AddComponent<RoSpriteAnimator>();

            //sprite.SpriteOffset = 0.5f;
            sprite.ChangeMotion(SpriteMotion.Idle);
            sprite.Type = SpriteType.Npc;
            sprite.LockAngle = true;
            sprite.SpriteOrder = 19999; //be in front of everything damn you

            sprite.SpriteData = TargetSprite;
            sprite.Initialize(false);
            sprite.ChangeActionExact(3);

            var mat = new Material(ShaderCache.Instance.SpriteShaderNoZTest);
            mat.renderQueue = 3005; //above water and all other sprites

            var renderer = sprite.GetComponent<RoSpriteRendererStandard>();
            renderer.SetOverrideMaterial(mat);


            return go;
        }

        public void SetSelectedTarget(ServerControllable target, string name, bool isAlly, bool isHard)
        {
            if (selectedSprite == null)
            {
                selectedSprite = CreateSelectedCursorObject();
            }

            var color = "";
            if (!isAlly)
                color = "<color=#FFAAAA>";
            if (isHard)
                color = "<color=#FF4444>";

            if (SelectedTarget != null && SelectedTarget != target)
                SelectedTarget.HideName();

            hasSelection = true;
            SelectedTarget = target;
            SelectedTarget.ShowName(color + name);
            //PlayerTargetUi.text = color + name;
            //PlayerTargetUi.gameObject.SetActive(true);
            selectedSprite.SetActive(true);
            //Debug.Log("Selecting target: '" + name + "' with game object: " + target);
            UpdateSelectedTarget();
        }

        public void ClearSelected()
        {
            //Debug.Log("Clearing target.");
            hasSelection = false;
            if (SelectedTarget != null && SelectedTarget != mouseHoverTarget)
                SelectedTarget.HideName();
            SelectedTarget = null;
            // PlayerTargetUi.text = "";
            // PlayerTargetUi.gameObject.SetActive(false);
            if (selectedSprite != null)
                selectedSprite.SetActive(false);
        }

        private void UpdateSelectedTarget()
        {
            if (!hasSelection || SelectedTarget == null || IsInNPCInteraction)
            {
                ClearSelected();
                return;
            }
            //
            // if (!PlayerTargetUi.gameObject.activeInHierarchy)
            //     PlayerTargetUi.gameObject.SetActive(true);

            // var screenPos = Camera.WorldToScreenPoint(SelectedTarget.transform.position);
            // var reverseScale = 1f / CanvasScaler.scaleFactor;

            // PlayerTargetUi.rectTransform.anchoredPosition = new Vector2(screenPos.x * reverseScale,
            //     ((screenPos.y - UiCanvas.pixelRect.height) - 30) * reverseScale);

            selectedSprite.transform.position = SelectedTarget.transform.position;
            //TargetUi.text = color + anim.Controllable.gameObject.name;
        }

        private void UpdateCameraSize()
        {
            if (Screen.width == 0)
                return; //wut?
            var scale = 1f / (1080f / Screen.height);
            CanvasScaler.scaleFactor = scale;
            lastWidth = Screen.width;
            lastHeight = Screen.height;
        }


        private FacingDirection GetFacingForAngle(float angle)
        {
            if (angle > 157.5f) return FacingDirection.South;
            if (angle > 112.5f) return FacingDirection.SouthWest;
            if (angle > 67.5f) return FacingDirection.West;
            if (angle > 22.5f) return FacingDirection.NorthWest;
            if (angle > -22.5f) return FacingDirection.North;
            if (angle > -67.5f) return FacingDirection.NorthEast;
            if (angle > -112.5f) return FacingDirection.East;
            if (angle > -157.5f) return FacingDirection.SouthEast;
            return FacingDirection.South;
        }


        private Direction GetFacingForPoint(Vector2Int point)
        {
            if (point.y == 0)
            {
                if (point.x < 0)
                    return Direction.West;
                else
                    return Direction.East;
            }

            if (point.x == 0)
            {
                if (point.y < 0)
                    return Direction.South;
                else
                    return Direction.North;
            }

            if (point.x < 0)
            {
                if (point.y < 0)
                    return Direction.SouthWest;
                else
                    return Direction.NorthWest;
            }

            if (point.y < 0)
                return Direction.SouthEast;

            return Direction.NorthEast;

            //return FacingDirection.South;
        }

        public void ChangeFacing(Vector3 dest)
        {
            var srcPoint = WalkProvider.GetTilePositionForPoint(Target.transform.position);
            var destPoint = WalkProvider.GetTilePositionForPoint(dest);

            var curFacing = controllable.SpriteAnimator.Direction;
            var newFacing = GetFacingForPoint(destPoint - srcPoint);
            var newHead = HeadFacing.Center;

            if (curFacing == newFacing)
            {
                if (controllable.SpriteAnimator.HeadFacing != HeadFacing.Center)
                    NetworkManager.Instance.ChangePlayerFacing(newFacing, HeadFacing.Center);

                return;
            }

            if (controllable.SpriteAnimator.State != SpriteState.Idle &&
                controllable.SpriteAnimator.State != SpriteState.Sit)
            {
                NetworkManager.Instance.ChangePlayerFacing(newFacing, HeadFacing.Center);
                return;
            }

            var diff = (int)curFacing - (int)newFacing;
            if (diff < 0)
                diff += 8;

            //var dontChange = false;

            if (diff != 4) //they're trying to turn around, let them without changing face
            {
                if ((diff == 1 && controllable.SpriteAnimator.HeadFacing == HeadFacing.Left)
                    || (diff == 7 && controllable.SpriteAnimator.HeadFacing == HeadFacing.Right))
                {
                    //we go from head turn to fully turning in that direction.
                    //not inverting the if statement just for clarity
                }
                else
                {
                    var facing = (int)newFacing;
                    if (diff < 4)
                    {
                        facing = facing + 1;
                        if (facing > 7)
                            facing = 0;
                        newHead = HeadFacing.Left;
                    }
                    else
                    {
                        facing = facing - 1;
                        if (facing < 0)
                            facing = 7;
                        newHead = HeadFacing.Right;
                    }

                    newFacing = (Direction)facing;
                }
            }

            //Debug.Log($"{curFacing} {newFacing} {newHead} {diff}");

            NetworkManager.Instance.ChangePlayerFacing(newFacing, newHead);
        }

        public void SnapLookAt()
        {
            CurLookAt = Target.transform.position;
            TargetFollow = Target.transform.position;
        }

        // public void ChangeCursor(Texture2D cursorTexture)
        // {
        //     if (currentCursor == cursorTexture)
        //         return;
        //
        //     if (cursorTexture == TalkCursorTexture || cursorTexture == TargetCursorTexture || cursorTexture == TargetCursorNoTargetTexture)
        //         Cursor.SetCursor(cursorTexture, new Vector2(16, 14), CursorMode.Auto);
        //     else
        //         Cursor.SetCursor(cursorTexture, Vector2.zero, CursorMode.Auto);
        //
        //     currentCursor = cursorTexture;
        // }

        private RoSpriteAnimator GetHitAnimator(RaycastHit hit)
        {
            var target = hit.transform.gameObject.GetComponent<RoSpriteAnimator>();
            return target.Parent ? target.Parent : target;
        }

        private RoSpriteAnimator GetClosestOrEnemy(RaycastHit[] hits)
        {
            var closestAnim = GetHitAnimator(hits[0]);
            var closestHit = hits[0];

            //var log = $"{hits.Length} {closestAnim.Controllable.gameObject.name}{closestHit.distance}{closestAnim.Controllable.IsAlly}";

            if (hits.Length == 1)
            {
                if (closestAnim.State == SpriteState.Dead && closestAnim.Type != SpriteType.Player)
                    return null;
                return closestAnim;
            }

            var isOk = closestAnim.State != SpriteState.Dead;
            var isAlly = closestAnim.Controllable.IsAlly;

            for (var i = 1; i < hits.Length; i++)
            {
                var hit = hits[i];
                var anim = GetHitAnimator(hit);

                if (anim.State == SpriteState.Dead && anim.Type != SpriteType.Player)
                    continue;

                if (!isOk)
                {
                    closestAnim = anim;
                    closestHit = hit;
                    isAlly = anim.Controllable.IsAlly;
                    isOk = true;
                }

                if (!isAlly && anim.Controllable.IsAlly)
                    continue;

                //log += $" {anim.Controllable.gameObject.name}{hit.distance}{anim.Controllable.IsAlly}-{anim.State}";

                if ((hit.distance < closestHit.distance) || (isAlly && !anim.Controllable.IsAlly))
                {
                    closestHit = hit;
                    closestAnim = anim;
                    isAlly = anim.Controllable.IsAlly;
                }
            }

            //log += $" : {closestAnim.Controllable.gameObject.name}{closestHit.distance}{closestAnim.Controllable.IsAlly} {isOk}";
            //Debug.Log(log);


            if (isOk)
                return closestAnim;

            return null;
        }

        private bool FindEntityUnderCursor(Ray ray, bool preferEnemy, out ServerControllable target)
        {
            //one of these days we'll be able to target allies and that preferEnemy value will matter
            target = null;
            var characterHits = Physics.RaycastAll(ray, MaxClickDistance, (1 << LayerMask.NameToLayer("Characters")));
            if (characterHits.Length == 0)
                return false;

            var anim = GetClosestOrEnemy(characterHits);
            if (anim == null)
                return false;

            target = anim.Controllable;
            return true;
        }

        private bool FindMapPositionUnderCursor(Ray ray, out Vector2Int position, out Vector3 groundHit, int mask, bool pushPointTowardsNormal = false)
        {
            position = Vector2Int.zero;
            groundHit = Vector3.zero;

            var hasHitMap = Physics.Raycast(ray, out var hit, MaxClickDistance, mask);
            if (!hasHitMap)
                return false;

            groundHit = hit.point;
            if (pushPointTowardsNormal)
                groundHit += hit.normal * 0.05f;

            if (WalkProvider.GetMapPositionForWorldPosition(groundHit, out position))
                return true;

            return false;
        }

        private bool FindWalkablePositionPastInitialRayHit(Ray ray, Vector3 groundHit, Vector2Int startPosition, Vector2Int walkSource,
            out Vector3 newGroundHit, out Vector2Int foundPosition)
        {
            //the target isn't valid, so we're going to try to see if we can find something that is in the same raycast target
            //if multiple walkable tiles are in the path of the ray, the first one that the player can reach is chosen, otherwise the furthest will be used.

            var loopCount = 0; //infinite loop safety
            var hasMatch = false;
            newGroundHit = groundHit;
            foundPosition = startPosition;

            ray.origin = groundHit + ray.direction * 0.01f;
            while (Physics.Raycast(ray, out var rehit, MaxClickDistance, (1 << LayerMask.NameToLayer("WalkMap"))) && loopCount < 5)
            {
                var newGroundPos = WalkProvider.GetMapPositionForWorldPosition(rehit.point, out var newMapPosition);

                if (newGroundPos && WalkProvider.IsCellWalkable(newMapPosition) &&
                    (newMapPosition - startPosition).SquareDistance() <= SharedConfig.MaxPathLength)
                {
                    newGroundHit = rehit.point;
                    foundPosition = newMapPosition;
                    hasMatch = true;

                    var steps = Pathfinder.GetPath(WalkProvider.WalkData, walkSource, newMapPosition, tempPath);
                    if (steps > 0)
                        return true;
                }

                ray.origin = rehit.point + ray.direction * 0.01f;
                loopCount++;
            }

            return hasMatch;
        }

        private GameCursorMode ScreenCastV2(bool isOverUi)
        {
            if (tempPath == null)
                tempPath = new Vector2Int[SharedConfig.MaxPathLength + 2];

            var ray = Camera.ScreenPointToRay(Input.mousePosition);

            ClickDelay -= Time.deltaTime;
            if (Input.GetMouseButtonUp(0))
            {
                isHolding = false;
                ClickDelay = 0;
            }

            var leftClick = Input.GetMouseButtonDown(0);
            var rightClick = Input.GetMouseButtonDown(1);

            var preferEnemyTarget = !(hasSkillOnCursor && cursorSkillTarget == SkillTarget.Ally);
            var isAlive = controllable.SpriteAnimator.State != SpriteState.Dead;
            var isSitting = controllable.SpriteAnimator.State == SpriteState.Sit;

            //cancel skill on cursor if we can't use a skill
            if (hasSkillOnCursor && (!isAlive || isSitting))
                hasSkillOnCursor = false;

            var walkMask = 1 << LayerMask.NameToLayer("WalkMap");
            var groundMask = 1 << LayerMask.NameToLayer("Ground");

            var hasEntity = FindEntityUnderCursor(ray, preferEnemyTarget, out var mouseTarget);
            var hasGround = FindMapPositionUnderCursor(ray, out var groundPosition, out var intersectLocation, walkMask);
            if (!hasGround) hasGround = FindMapPositionUnderCursor(ray, out groundPosition, out intersectLocation, groundMask, true);
            var hasSrcPos = WalkProvider.GetMapPositionForWorldPosition(Target.transform.position, out var srcPosition);
            var hasTargetedSkill = hasSkillOnCursor && (cursorSkillTarget == SkillTarget.Enemy || cursorSkillTarget == SkillTarget.Ally);
            var hasGroundSkill = hasSkillOnCursor && cursorSkillTarget == SkillTarget.Ground;

            var canInteract = hasEntity && isAlive && !isOverUi && !isHolding && !hasGroundSkill;
            var canClickEnemy = canInteract && !mouseTarget.IsAlly && mouseTarget.CharacterType != CharacterType.NPC;
            var canClickNpc = canInteract && mouseTarget.CharacterType == CharacterType.NPC && mouseTarget.IsInteractable;
            var canClickGround = hasGround && isAlive && (!isOverUi || isHolding) && !canClickEnemy && !canClickNpc;
            var canMove = ClickDelay <= 0 && isAlive && !isSitting;
            var showEntityName = hasEntity && !isOverUi;

            var cancelSkill = (hasSkillOnCursor && rightClick) || (hasSkillOnCursor && leftClick && hasTargetedSkill && !hasEntity);
            if (cancelSkill) hasSkillOnCursor = hasGroundSkill = hasTargetedSkill = leftClick = rightClick = false; //lol

            var displayCursor = canClickEnemy ? GameCursorMode.Attack : GameCursorMode.Normal;
            if (canClickNpc) displayCursor = GameCursorMode.Dialog;
            if (hasSkillOnCursor) displayCursor = GameCursorMode.SkillTarget;
            
            //Debug.Log($"{hasEntity} {}");

            if (showEntityName)
            {
                //if our new mouseover target is different from last time, we need to swap over
                if (mouseHoverTarget != mouseTarget)
                {
                    if (SelectedTarget != mouseTarget) //we don't want to hide it if it's our currently targeted enemy though, that stays
                        mouseHoverTarget?.HideName();

                    mouseHoverTarget = mouseTarget;

                    if (mouseHoverTarget.IsAlly)
                        mouseHoverTarget.ShowName(mouseHoverTarget.DisplayName);
                    else
                        mouseHoverTarget.ShowName("<color=#FFAAAA>" + mouseHoverTarget.DisplayName); //yeah this is stupid
                }
            }
            else
            {
                mouseHoverTarget?.HideName();
                mouseHoverTarget = null;
            }

            if (canClickNpc && leftClick)
            {
                NetworkManager.Instance.SendNpcClick(mouseTarget.Id);
                return displayCursor;
            }

            if (canClickEnemy && leftClick)
            {
                if (!hasTargetedSkill)
                    NetworkManager.Instance.SendAttack(mouseTarget.Id);
                else
                {
                    NetworkManager.Instance.SendSingleTargetSkillAction(mouseTarget.Id, cursorSkill, cursorSkillLvl);
                    hasSkillOnCursor = false;
                }

                return displayCursor;
            }

            //if our cursor isn't over ground there's no real point in continuing.
            if (!hasGround)
                return displayCursor;

            //while sitting or holding shift and clicking turns your character to face where you clicked
            if (leftClick & (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) || controllable.SpriteAnimator.State == SpriteState.Sit))
            {
                ClickDelay = 0.1f;
                ChangeFacing(WalkProvider.GetWorldPositionForTile(groundPosition));
                return displayCursor;
            }

            var hasValidPath = lastPathValid; //if we haven't changed targets since last time we can assume it's just as valid as before 

            if (hasSrcPos && groundPosition != lastTile) //but we have changed targets, so lets see if it's valid
            {
                hasValidPath = WalkProvider.IsCellWalkable(groundPosition);

                //check to see if the destination can be reached
                var steps = Pathfinder.GetPath(WalkProvider.WalkData, groundPosition, srcPosition, tempPath);
                if (steps == 0)
                    hasValidPath = false;

                //if our cursor hits the top of a wall we want to check if there's valid ground behind it we can use.
                if (!hasValidPath && FindWalkablePositionPastInitialRayHit(ray, intersectLocation,
                        groundPosition, srcPosition, out intersectLocation, out groundPosition))
                {
                    //try to make a path again to our new, valid cell we found
                    steps = Pathfinder.GetPath(WalkProvider.WalkData, groundPosition, srcPosition, tempPath);
                    hasValidPath = steps > 0;
                }
            }

            //this draws (or disables) the square indicator that shows where you're targeting on the ground
            if (!isOverUi && canClickGround)
            {
#if DEBUG
                DebugDisplay.text = groundPosition.ToString();
#endif
                WalkProvider.UpdateCursorPosition(Target.transform.position, intersectLocation, hasValidPath);
            }
            else
                WalkProvider.DisableRenderer();

            lastTile = groundPosition;
            lastPathValid = hasValidPath;

            if (leftClick && canClickGround && hasGroundSkill)
            {
                if (WalkProvider.IsCellWalkable(groundPosition))
                {
                    NetworkManager.Instance.SendGroundTargetSkillAction(groundPosition, cursorSkill, cursorSkillLvl);
                    hasSkillOnCursor = false;
                    isHolding = false;
                }

                //TODO: we should inform the user it isn't valid here

                hasSkillOnCursor = false;
                return GameCursorMode.Normal; //the skill is no longer on our cursor so no point in keeping skill cursor
            }

            //everything left is movement related so if we can't move or can't click ground, we're done.
            if (!canClickGround || !canMove)
                return displayCursor;

            //if they aren't trying to move we're also done. 
            if (!leftClick && !isHolding) return displayCursor;

            var dest = groundPosition;

            //if we can't path to the tile they clicked on, instead get the closest tile that is valid and we'll walk to that instead.
            if (!hasValidPath && WalkProvider.GetNextWalkableTileForClick(controllable.Position, groundPosition, out dest))
                hasValidPath = true;

            if (hasValidPath)
            {
                if (!isHolding)
                {
                    var click = Instantiate(clickEffectPrefab);
                    click.transform.position = WalkProvider.GetWorldPositionForTile(dest) + new Vector3(0f, 0.02f, 0f);
                }

                NetworkManager.Instance.MovePlayer(dest);
                ClickDelay = 0.5f;
                isHolding = true;
            }

            return displayCursor;
        }

        public void ResetChat()
        {
            TextBoxText.text = "Welcome to Ragnarok Rebuild!";
        }

        public void AppendChatText(string txt)
        {
            if (string.IsNullOrWhiteSpace(txt))
                return;

            TextBoxText.text += Environment.NewLine + txt;
            TextBoxText.ForceMeshUpdate();
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)TextBoxScrollRect.transform);
            TextBoxScrollRect.verticalNormalizedPosition = 0;
        }

        public void AppendError(string txt)
        {
            if (string.IsNullOrWhiteSpace(txt))
                return;

            TextBoxText.text += Environment.NewLine + "<color=red>Error</color>: " + txt;
            TextBoxText.ForceMeshUpdate();
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)TextBoxScrollRect.transform);
            TextBoxScrollRect.verticalNormalizedPosition = 0;
        }

        public void Emote(int id)
        {
            NetworkManager.Instance.SendEmote(id);
        }

        public void OnSubmitTextBox(string text)
        {
            //Debug.Log("Submitted: " + text);
            //         EventSystem.current.SetSelectedGameObject(null);
            //         TextBoxInputField.text = "";

            ClientCommandHandler.HandleClientCommand(this, controllable, text);

            lastMessage = text;
        }

        public void AttachEffectToEntity(string effect, GameObject target, int ownerId = -1)
        {
            if (!EffectIdLookup.TryGetValue(effect, out var id))
            {
                AppendError($"Could not find effect with name {effect}.");
                return;
            }

            AttachEffectToEntity(id, target);
        }

        public void AttachEffectToEntity(int effect, GameObject target, int ownerId = -1)
        {
            if (!EffectList.TryGetValue(effect, out var asset))
            {
                AppendError($"Could not find effect with id {effect}.");
                return;
            }

            if (EffectCache.TryGetValue(effect, out var prefab) && prefab != null)
            {
                if (asset.Billboard)
                {
                    var obj2 = GameObject.Instantiate(prefab, target.transform, false);
                    obj2.transform.localPosition = new Vector3(0, asset.Offset, 0);

                    var audio = obj2.GetComponent<EffectAudioSource>();
                    if (audio)
                        audio.OwnerId = ownerId;
                }
                else
                {
                    var obj2 = GameObject.Instantiate(prefab);
                    obj2.transform.localPosition = target.transform.position + new Vector3(0, asset.Offset, 0);

                    var audio = obj2.GetComponent<EffectAudioSource>();
                    if (audio)
                        audio.OwnerId = ownerId;
                }

                return;
            }

            //Debug.Log($"Loading effect asset {asset.PrefabName}");
            var loader = Addressables.LoadAssetAsync<GameObject>(asset.PrefabName);
            loader.Completed += ah =>
            {
                if (target != null && target.gameObject.activeInHierarchy)
                {
                    if (asset.Billboard)
                    {
                        var obj2 = GameObject.Instantiate(ah.Result, target.transform, false);
                        obj2.transform.localPosition = new Vector3(0, asset.Offset, 0);
                        var audio = obj2.GetComponent<EffectAudioSource>();
                        if (audio)
                            audio.OwnerId = ownerId;
                    }
                    else
                    {
                        var obj2 = GameObject.Instantiate(ah.Result);
                        obj2.transform.localPosition = target.transform.position + new Vector3(0, asset.Offset, 0);
                        var audio = obj2.GetComponent<EffectAudioSource>();
                        if (audio)
                            audio.OwnerId = ownerId;
                    }

                    EffectCache[asset.Id] = ah.Result;
                    //obj2.transform.localScale = new Vector3(0.75f, 0.75f, 0.75f);
                    //ah.Result.transform.SetParent(obj.transform, false);
                }
            };
        }

        public void CreateEffect(int effect, Vector3 pos, int facing)
        {
            if (!EffectList.TryGetValue(effect, out var asset))
            {
                AppendError($"Could not find effect with id {effect}.");
                return;
            }

            var outputObj = new GameObject(asset.Name);
            outputObj.transform.localPosition = pos;

            if (EffectCache.TryGetValue(effect, out var prefab) && prefab != null)
            {
                var obj2 = GameObject.Instantiate(prefab, outputObj.transform, false);
                obj2.transform.localPosition = new Vector3(0, asset.Offset, 0);
                if (asset.Billboard)
                    obj2.AddComponent<BillboardObject>();

                outputObj.AddComponent<RemoveWhenChildless>();

                if (facing != 0)
                    obj2.transform.localRotation = Quaternion.AngleAxis(45 * facing, Vector3.up);

                return;
            }

            var loader = Addressables.LoadAssetAsync<GameObject>(asset.PrefabName);
            loader.Completed += ah =>
            {
                var obj2 = GameObject.Instantiate(ah.Result, outputObj.transform, false);
                obj2.transform.localPosition = new Vector3(0, asset.Offset, 0);
                if (asset.Billboard)
                    obj2.AddComponent<BillboardObject>();

                outputObj.AddComponent<RemoveWhenChildless>();


                // Debug.Log("Loaded effect " + asset.PrefabName);

                EffectCache[asset.Id] = ah.Result;
            };
        }

        public void UpdateWaterTexture()
        {
            Camera.depthTextureMode = DepthTextureMode.Depth;

            if (WaterTexture == null)
            {
                WaterTexture = new RenderTexture(Screen.width / 4, Screen.height / 4, 32, RenderTextureFormat.ARGBHalf);
                WaterDepthTexture = new RenderTexture(Screen.width / 4, Screen.height / 4, 16, RenderTextureFormat.Depth);

                WaterCamera.SetTargetBuffers(WaterTexture.colorBuffer, WaterDepthTexture.depthBuffer);
                WaterCamera.SetReplacementShader(ShaderCache.Instance.WaterShader, null);
                WaterCamera.backgroundColor = new Color(0, 0, -1000);

                Shader.SetGlobalTexture("_WaterDepth", WaterTexture);
            }

            if (WaterTexture.width != Screen.width / 4 || WaterTexture.height != Screen.height / 4)
            {
                var oldWaterTexture = WaterTexture;
                var oldWaterDepth = WaterDepthTexture;

                WaterTexture = new RenderTexture(Screen.width / 4, Screen.height / 4, 32, RenderTextureFormat.ARGBHalf);
                WaterDepthTexture = new RenderTexture(Screen.width / 4, Screen.height / 4, 16, RenderTextureFormat.Depth);

                WaterCamera.SetTargetBuffers(WaterTexture.colorBuffer, WaterDepthTexture.depthBuffer);
                WaterCamera.SetReplacementShader(ShaderCache.Instance.WaterShader, null);
                WaterCamera.backgroundColor = new Color(0, 0, -1000);

                Shader.SetGlobalTexture("_WaterDepth", WaterTexture);

                oldWaterTexture.Release();
                oldWaterDepth.Release();
            }
        }

        public void OnPostRender()
        {
            //WaterCamera.RenderWithShader(WaterDepthShader, "WaterDepth");
            //WaterCamera.Render();
        }

        public void CinemachineFollow()
        {
            if (CinemachineHidePlayerObject)
                controllable.SpriteAnimator.SetRenderActive(false);

            if (Recorder != null)
            {
                Recorder.gameObject.SetActive(true);
                transform.position = Recorder.transform.position;
                transform.localRotation = Recorder.transform.rotation;

                WaterCamera.fieldOfView = Recorder.GetComponent<Camera>().fieldOfView;
                Camera.main.fieldOfView = Recorder.GetComponent<Camera>().fieldOfView;

                // Debug.Log(transform.position);
            }

            Rotation = transform.localRotation.eulerAngles.y;
            WalkProvider.DisableRenderer();

            if (ListenerProbe != null)
            {
                if (Physics.Raycast(transform.localPosition, transform.forward, out var hit, LayerMask.NameToLayer("Ground")))
                {
                    var dist = Vector3.Distance(Camera.main.transform.position, hit.point);
                    if (dist > 30)
                        ListenerProbe.transform.localPosition = new Vector3(0f, 0f, dist - 30f);
                    else
                        ListenerProbe.transform.localPosition = Camera.main.transform.position;
                }
                else
                {
                    ListenerProbe.transform.localPosition = new Vector3(0f, 0f, 30f);
                }


                // var forward = Camera.main.transform.forward;
                //var dist = Vector3.Distance(Camera.main.transform.position, Target.transform.position);
            }
        }

        public void UpdateFog()
        {
            if (!CinemachineMode && RenderSettings.fog)
            {
                var near = Distance * FogNearRatio;
                var far = Distance * FogFarRatio;

                RenderSettings.fogStartDistance = near;
                RenderSettings.fogEndDistance = far;

                // var val = (RenderSettings.fogEndDistance - Distance) / (RenderSettings.fogEndDistance - RenderSettings.fogStartDistance);
                // Camera.backgroundColor = RenderSettings.fogColor * (1 - val);
            }
        }

        public static void SetErrorUiText(string text)
        {
            Dispatcher.RunOnMainThread(() =>
            {
                Instance.ErrorNoticeUi.gameObject.SetActive(true);
                Instance.ErrorNoticeUi.text = "<color=red>Error: </color>" + text;
                Instance.IsInErrorState = true;
            });
        }

        public void Update()
        {
            if (IsInErrorState && Input.GetKeyDown(KeyCode.Space))
                SceneManager.LoadScene(0);

            if (Target == null)
                return;

            if (controllable == null)
                controllable = Target.GetComponent<ServerControllable>();

            if (ListenerProbe != null)
            {
                var forward = Camera.main.transform.forward;
                var dist = Vector3.Distance(Camera.main.transform.position, Target.transform.position);
                ListenerProbe.transform.localPosition = new Vector3(0f, 0f, dist - 10f);
            }

            if (WalkProvider == null)
                WalkProvider = GameObject.FindObjectOfType<RoWalkDataProvider>();

            if (Screen.height != lastHeight)
                UpdateCameraSize();

            UpdateWaterTexture();

            var pointerOverUi = EventSystem.current.IsPointerOverGameObject();
            var selected = EventSystem.current.currentSelectedGameObject;

            var inTextBox = false;
            if (selected != null)
                inTextBox = selected.GetComponent<TMP_InputField>() != null;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                //Debug.Log("Escape pressed, inTextBox: " + inTextBox);
                //Debug.Log(EventSystem.current.currentSelectedGameObject);

                if (hasSkillOnCursor)
                    hasSkillOnCursor = false;

                if (inTextBox)
                {
                    inTextBox = false;
                    TextBoxInputField.text = "";
                }
                else
                {
                    UiManager.Instance.CloseLastWindow();
                }

                EventSystem.current.SetSelectedGameObject(null);
            }

            if (inTextBox)
            {
                if (Input.GetKeyDown(KeyCode.UpArrow))
                {
                    TextBoxInputField.text = lastMessage;
                    if (lastMessage != null)
                        TextBoxInputField.caretPosition = lastMessage.Length;
                }
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (!inTextBox)
                {
                    //EventSystem.current.SetSelectedGameObject(TextBoxInputField.gameObject);
                    TextBoxInputField.ActivateInputField();
                }
                else
                {
                    var text = TextBoxInputField.text;
                    OnSubmitTextBox(text);
                    if (string.IsNullOrWhiteSpace(text) || text.StartsWith("/"))
                    {
                        TextBoxInputField.text = "";
                        TextBoxInputField.DeactivateInputField(true);
                        EventSystem.current.SetSelectedGameObject(null);
                    }
                    else
                    {
                        inTextBox = false;

                        //Debug.Log(text);
                        //TextBoxInputField.DeactivateInputField(true);
                        TextBoxInputField.text = "";
                        TextBoxInputField.ActivateInputField();
                        //EventSystem.current.SetSelectedGameObject(null);
                    }
                }
            }


            if (!inTextBox && Input.GetKeyDown(KeyCode.R))
            {
                if (controllable.SpriteAnimator.State == SpriteState.Dead)
                    NetworkManager.Instance.SendRespawn(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
            }

            if (!inTextBox && Input.GetKeyDown(KeyCode.Insert))
            {
                if (controllable.SpriteAnimator.State == SpriteState.Idle || controllable.SpriteAnimator.State == SpriteState.Standby)
                    NetworkManager.Instance.ChangePlayerSitStand(true);
                if (controllable.SpriteAnimator.State == SpriteState.Sit)
                    NetworkManager.Instance.ChangePlayerSitStand(false);
            }

            if (!inTextBox && Input.GetKeyDown(KeyCode.M))
            {
                AudioManager.Instance.ToggleMute();
            }

            //if (Input.GetKeyDown(KeyCode.S))
            //	controllable.SpriteAnimator.Standby = true;

            if (!inTextBox && Input.GetKeyDown(KeyCode.Space))
            {
                //Debug.Log(controllable.IsWalking);
                //if(controllable.IsWalking)
                NetworkManager.Instance.StopPlayer();
            }

            if (!inTextBox && Input.GetKeyDown(KeyCode.W))
            {
                if (!WarpPanel.activeInHierarchy)
                    WarpPanel.GetComponent<WarpWindow>().ShowWindow();
                else
                    WarpPanel.GetComponent<WarpWindow>().HideWindow();
            }


            if (!inTextBox && Input.GetKeyDown(KeyCode.E))
            {
                if (!EmotePanel.activeInHierarchy)
                    EmotePanel.GetComponent<EmoteWindow>().ShowWindow();
                else
                    EmotePanel.GetComponent<EmoteWindow>().HideWindow();
            }

            if (!inTextBox && Input.GetKeyDown(KeyCode.Alpha1))
                NetworkManager.Instance.SendUseItem(501);

            if (!inTextBox && Input.GetKeyDown(KeyCode.Alpha2))
            {
                hasSkillOnCursor = true;
                cursorSkill = CharacterSkill.ThunderStorm;
                cursorSkillTarget = ClientDataLoader.Instance.GetSkillTarget(cursorSkill);
                // Debug.Log(cursorSkillTarget);
                //cursorSkillLvl = 10;
                //skillScroll = 10f;
            }

            if (!inTextBox && Input.GetKeyDown(KeyCode.Alpha3))
            {
                hasSkillOnCursor = true;
                cursorSkill = CharacterSkill.Bash;
                cursorSkillTarget = ClientDataLoader.Instance.GetSkillTarget(cursorSkill);
                // Debug.Log(cursorSkillTarget);
                //cursorSkillLvl = 5;
                //skillScroll = 5f;
            }

            if (!inTextBox && Input.GetKeyDown(KeyCode.D))
            {
                DebugVisualization = !DebugVisualization;
                //GroundHighlighter.Create(controllable, "blue");
            }
            
            
            if (!inTextBox && Input.GetKeyDown(KeyCode.A))
            {
                DebugIgnoreAttackMotion = !DebugIgnoreAttackMotion;
                //GroundHighlighter.Create(controllable, "blue");
            }


            //remove the flag to enable cinemachine recording on this
#if UNITY_EDITOR
            if ((Input.GetKeyDown(KeyCode.F5) || Input.GetKeyDown(KeyCode.F6)) && Application.isEditor && Recorder != null)
            {
                if (CinemachineMode)
                {
                    CinemachineMode = false;

                    Recorder.StopRecording();
                    Camera.main.fieldOfView = 15f;
                }
                else
                {
                    CinemachineMode = true;
                    if (Recorder.CenterPlayerOnMap)
                        NetworkManager.Instance.SendAdminHideCharacter(true);
                    Recorder.StartRecording();
                }
            }
#endif

            if (!inTextBox && !pointerOverUi && Input.GetMouseButtonDown(1))
            {
                if (Time.timeSinceLevelLoad - LastRightClick < 0.3f)
                {
                    if (Input.GetKey(KeyCode.LeftShift))
                        Height = 50;
                    else
                        TargetRotation = cameraMode == CameraMode.Normal ? 0 : (rotationRange.y + rotationRange.x) / 2f;
                }

                LastRightClick = Time.timeSinceLevelLoad;
            }

            if (!inTextBox && !pointerOverUi && Input.GetMouseButton(1))
            {
                if (Input.GetMouseButton(1) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
                {
                    Height -= Input.GetAxis("Mouse Y") / 4;

                    Height = Mathf.Clamp(Height, 0f, 90f);
                    if (lockCamera)
                        Height = Mathf.Clamp(Height, heightRange.x, heightRange.y);
                }
                else
                {
                    var turnSpeed = 200;
                    TargetRotation += Input.GetAxis("Mouse X") * turnSpeed * Time.deltaTime;
                    if (lockCamera)
                        TargetRotation = Mathf.Clamp(TargetRotation, rotationRange.x, rotationRange.y);
                }
            }


//#if !DEBUG
            if (Height > 75)
                Height = 75;
            if (Height < 30)
                Height = 30;

            if (Distance < 0)
                Distance *= -1;
            if (Height < 0)
                Height *= -1;
#if !DEBUG
            if (Distance > 100)
                Distance = 100;
#endif


            if (IsInNPCInteraction && !pointerOverUi && Input.GetMouseButtonDown(0))
            {
                NetworkManager.Instance.SendNpcAdvance();
                isHolding = false;
                noHold = true;
                return; //no point in doing other screencast stuff if we're still talking to the npc.
            }

            //DoScreenCast(pointerOverUi);

            var cursor = ScreenCastV2(pointerOverUi);
            if (cursor != GameCursorMode.SkillTarget)
                CursorManager.UpdateCursor(cursor);
            else
                CursorManager.UpdateCursor(cursor, cursorSkillLvl);

            UpdateSelectedTarget();

            //Rotation += Time.deltaTime * 360;

            if (TargetRotation > 360)
                TargetRotation -= 360;
            if (TargetRotation < 0)
                TargetRotation += 360;

            if (Rotation > 360)
                Rotation -= 360;
            if (Rotation < 0)
                Rotation += 360;

            Rotation = Mathf.LerpAngle(Rotation, TargetRotation, 7.5f * Time.deltaTime);

            var ctrlKey = Input.GetKey(KeyCode.LeftControl) ? 10 : 1;

            var screenRect = new Rect(0, 0, Screen.width, Screen.height);

            if (!pointerOverUi && screenRect.Contains(Input.mousePosition))
            {
                if (!hasSkillOnCursor)
                    Distance += Input.GetAxis("Mouse ScrollWheel") * 20 * ctrlKey;
                else
                {
                    skillScroll += Input.GetAxis("Mouse ScrollWheel") * 10f;
                    skillScroll = Mathf.Clamp(skillScroll, 1, 10);
                    cursorSkillLvl = Mathf.RoundToInt(skillScroll);
                    // Debug.Log(skillScroll);
                }
            }

            Distance = Mathf.Clamp(Distance, zoomRange.x, zoomRange.y);

#if !DEBUG
            if (Distance > 90)
                Distance = 90;
            if (Distance < 30)
                Distance = 30;
#endif

            var curTarget = Target.transform.position;
            if (OverrideTarget != null)
                curTarget = (curTarget + OverrideTarget.transform.position) / 2f;

            TargetFollow = Vector3.Lerp(TargetFollow, curTarget, Time.deltaTime * 5f);
            CurLookAt = TargetFollow;

            var targetHeight = Mathf.Lerp(Target.transform.position.y, WalkProvider.GetHeightForPosition(Target.transform.position), Time.deltaTime * 20f);

            Target.transform.position = new Vector3(Target.transform.position.x, targetHeight, Target.transform.position.z);

            var pos = Quaternion.Euler(Height, Rotation, 0) * Vector3.back * Distance;

            //sort sprites and all that jazz as if the camera were completely flat with the ground.
            Camera.transparencySortMode = TransparencySortMode.CustomAxis;
            Camera.transparencySortAxis = Quaternion.Euler(0, Rotation, 0) * Vector3.forward;

            transform.position = CurLookAt + pos;
            transform.LookAt(CurLookAt, Vector3.up);


            if (!inTextBox && Input.GetKeyDown(KeyCode.T))
                NetworkManager.Instance.RandomTeleport();

            if (!inTextBox && Input.GetKeyDown(KeyCode.F3))
                UseTTFDamage = !UseTTFDamage;

            if (!inTextBox && Input.GetKeyDown(KeyCode.Tab))
            {
                var chunks = GameObject.FindObjectsOfType<RoMapChunk>();
                foreach (var c in chunks)
                {
                    if (c.gameObject.layer == LayerMask.NameToLayer("WalkMap"))
                    {
                        var r = c.gameObject.GetComponent<MeshRenderer>();
                        r.enabled = !r.enabled;
                    }
                }
            }

            if (CinemachineMode)
                CinemachineFollow();

            UpdateFog();
        }
    }
}