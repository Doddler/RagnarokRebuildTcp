using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Assets.Scripts.Effects;
using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Effects.EffectHandlers.General;
using Assets.Scripts.Effects.EffectHandlers.StatusEffects;
using Assets.Scripts.MapEditor;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.Sprites;
using Assets.Scripts.UI;
using Assets.Scripts.UI.ConfigWindow;
using Assets.Scripts.UI.Hud;
using Assets.Scripts.UI.RefineItem;
using Assets.Scripts.UI.Utility;
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
using UnityEngine.Serialization;
using UnityEngine.UI;
using Utility;
using CursorMode = UnityEngine.CursorMode;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

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

    public enum PromptType
    {
        None,
        PromptForDropCount,
        PromptForText,
        PromptForYesNo,
        RightClickMenu
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
        private RoSpriteData TargetSprite { get; set; }

        // public GameObject LevelUpPrefab;
        // public GameObject ResurrectPrefab;
        // public GameObject DeathPrefab;
        public TextAsset LevelChart;

        private static CameraFollower _instance;
        private static readonly int RoBlindDistance = Shader.PropertyToID("_RoBlindDistance");
        public RoWalkDataProvider WalkProvider;

        public Canvas UiCanvas;

        //public TextMeshProUGUI TargetUi;
        //public TextMeshProUGUI PlayerTargetUi;
        // public TextMeshProUGUI HpDisplay;
        // public Slider HpSlider;
        // public TextMeshProUGUI SpDisplay;
        // public Slider SpSlider;
        // public TextMeshProUGUI ExpDisplay;
        // public Slider ExpSlider;
        public TMP_InputField TextBoxInputField;
        public CanvasScaler CanvasScaler;
        public TextMeshProUGUI ErrorNoticeUi;
        // public TextMeshProUGUI DebugDisplay;

        public ScrollRect TextBoxScrollRect;
        public TextMeshProUGUI TextBoxText;

        private List<string> chatMessages = new();

        public CharacterDetailBox CharacterDetailBox;

        // public TextMeshProUGUI CharacterName;
        // public TextMeshProUGUI CharacterJob;
        // public TextMeshProUGUI CharacterZeny;

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

        //private bool noHold = false;
        private bool hasSelection;
        public ServerControllable SelectedTarget;
        private GameObject selectedSprite;
        private string targetText;
        private GameObject clickEffectPrefab;

        public GameObject WarpPanel;
        public GameObject DialogPanel;
        public GameObject NpcOptionPanel;
        public GameObject EmotePanel;

        private GlobalKeyword smoothPixelKeyword;
        public bool UseSmoothPixel = false;
        public bool UseTTFDamage = false;
        public bool IsInNPCInteraction = false;

        private int[] levelReqs = new int[100];

        public Dictionary<string, int> EffectIdLookup;
        public Dictionary<int, EffectTypeEntry> EffectList;
        public Dictionary<int, GameObject> EffectCache;

        private bool hasSkillOnCursor;
        private bool isCursorSkillItem;
        private CharacterSkill cursorSkill;
        private SkillTarget cursorSkillTarget;
        private int cursorItemId;
        private int cursorSkillLvl = 5;
        private int cursorMaxSkillLvl = 10;
        private float skillScroll = 5f;

        public float ShakeTime = 0f;
        private float shakeStepProgress = 0f;
        private Vector3 shakePos = Vector3.zero;
        private Vector3 shakeTarget = Vector3.zero;

        private bool canChangeCursorLevel;

        //private bool cursorShowSkillLevel = true;
        public bool HasSkillOnCursor => hasSkillOnCursor;
        public SkillTarget CursorSkillTarget => cursorSkillTarget;

        public bool CinemachineMode;
        public VideoRecorder Recorder;

        private const bool CinemachineCenterPlayerOnMap = true;
        private const bool CinemachineHidePlayerObject = true;
        public bool IsInErrorState;
        public bool DebugVisualization;
        public bool DebugIgnoreAttackMotion;

        public float FogNearRatio = 0.3f;
        public float FogFarRatio = 4f;

        public bool IsBlindActive;
        public float BlindStrength = 60;
        private const float BlindTargetDistance = 10f;

        public PromptType ActivePromptType;

        private List<RaycastResult> raycastResults;

#if DEBUG
        private const float MaxClickDistance = 500;

#else
        private const float MaxClickDistance = 150;
#endif

        public int ExpForLevel(int lvl) => lvl < 1 || lvl >= 99 ? -1 : levelReqs[lvl - 1];

        public Vector2Int PlayerPosition => TargetControllable != null ? TargetControllable.CellPosition : Vector2Int.zero;

        [NonSerialized] public PlayerState PlayerState;

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

        private List<Vector2Int> movePath;
        private float moveSpeed;
        private float moveProgress;
        private Vector3 startPos;

        public float TargetRotation;
        public float DefaultRotation;

        public float ClickDelay;

        public float Rotation;
        public float Distance;
        public float Height;

        public float TurnSpeed;

        public int MonSpawnCount = 1;

        public TextAsset EffectConfigFile;

        public float LastRightClick;
        private bool isHolding;

        public CameraMode CameraMode; //public so we can change it in editor
        public bool lockCamera;
        private Vector2 rotationRange;
        private Vector2 heightRange;
        private Vector2 zoomRange = new Vector2(30, 70);
        public bool InTextBox;
        public bool InItemInputBox;
        public bool InTextInputBox;
        public bool InYesNoPrompt;

        public void ResetCursor() => isHolding = false;

        public void Awake()
        {
            _instance = this;

            //CurLookAt = Target.transform.position;
            TargetFollow = CurLookAt;
            Camera = GetComponent<Camera>();
            //MoveTo = Target.transform.position;

            WalkProvider = GameObject.FindObjectOfType<RoWalkDataProvider>();

            GameConfig.InitializeIfNecessary();
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

            Height = 50;

            smoothPixelKeyword = GlobalKeyword.Create("SMOOTHPIXEL");
            UseSmoothPixel = false;
            Shader.SetKeyword(smoothPixelKeyword, UseSmoothPixel);

            ResetChat();

            //targetWalkable = Target.GetComponent<EntityWalkable>();
            //if (targetWalkable == null)
            //    targetWalkable = Target.AddComponent<EntityWalkable>();

            //DoMapSpawn();
        }

        private void SaveCurrentCameraSettings()
        {
            if (CameraMode == CameraMode.Normal)
            {
                PlayerPrefs.SetFloat("cameraX", TargetRotation);
                PlayerPrefs.SetFloat("cameraY", Height);
            }

            if (CameraMode == CameraMode.Indoor)
            {
                PlayerPrefs.SetFloat("cameraIndoorX", TargetRotation);
                PlayerPrefs.SetFloat("cameraIndoorY", Height);
            }
        }

        //return value signifies if we put a skill on the cursor or not
        public bool PressSkillButton(CharacterSkill skill, int id)
        {
            if (TargetControllable.SpriteAnimator.State == SpriteState.Dead)
                return false;

            var target = ClientDataLoader.Instance.GetSkillTarget(skill);
            switch (target)
            {
                case SkillTarget.Any:
                case SkillTarget.Enemy:
                case SkillTarget.Ground:
                case SkillTarget.Ally:
                    hasSkillOnCursor = true;
                    isCursorSkillItem = false;
                    canChangeCursorLevel = ClientDataLoader.Instance.GetSkillData(skill).AdjustableLevel;
                    cursorSkill = skill;
                    cursorMaxSkillLvl = PlayerState.KnownSkills.GetValueOrDefault(skill, 1);
                    if (PlayerState.GrantedSkills.TryGetValue(skill, out var granted) && granted > cursorMaxSkillLvl)
                        cursorMaxSkillLvl = granted;
                    skillScroll = Mathf.Clamp(id, 1, cursorMaxSkillLvl);
                    if (!canChangeCursorLevel)
                        skillScroll = cursorMaxSkillLvl;
                    cursorSkillLvl = Mathf.RoundToInt(skillScroll);
                    cursorSkillTarget = target;
                    if (target == SkillTarget.Any || target == SkillTarget.Ally)
                        UiManager.Instance.PartyPanel.StartSkillOnCursor();
                    return true;
                case SkillTarget.Passive:
                    Debug.LogWarning($"Can't cast passive skill!");
                    break;
                case SkillTarget.Self:
                    var adjustable = ClientDataLoader.Instance.GetSkillData(skill).AdjustableLevel;
                    var level = id;
                    var maxLvl = PlayerState.KnownSkills.GetValueOrDefault(skill, 1);
                    if (PlayerState.GrantedSkills.TryGetValue(skill, out var granted2) && granted2 > cursorMaxSkillLvl)
                        maxLvl = granted2;
                    level = Mathf.Clamp(level, 1, maxLvl);
                    if (!canChangeCursorLevel)
                        skillScroll = maxLvl;
                    NetworkManager.Instance.SendSelfTargetSkillAction(skill, level);
                    break;
            }

            return false;
        }

        public void BeginTargetingItem(int cursorItem, SkillTarget target)
        {
            hasSkillOnCursor = true;
            isCursorSkillItem = true;
            canChangeCursorLevel = false;
            cursorSkill = CharacterSkill.NoCast;
            cursorMaxSkillLvl = 1;
            skillScroll = 1;
            cursorSkillLvl = 1;
            cursorSkillTarget = target;
            cursorItemId = cursorItem;
            if (target == SkillTarget.Any || target == SkillTarget.Ally)
                UiManager.Instance.PartyPanel.StartSkillOnCursor();
        }

        public void SetCameraViewpoint(MapViewpoint viewpoint)
        {
            CameraMode = CameraMode.Fixed;
            TargetRotation = Rotation = viewpoint.SpinIn;
            DefaultRotation = viewpoint.SpinIn;
            Height = viewpoint.HeightIn;
            Distance = viewpoint.ZoomIn;
            rotationRange = new Vector2(viewpoint.SpinMin, viewpoint.SpinMax);
            heightRange = new Vector2(viewpoint.HeightMin, viewpoint.HeightMax);
            zoomRange = new Vector2(viewpoint.ZoomMin, viewpoint.ZoomMin + viewpoint.ZoomDist);
            lockCamera = true;
        }

        public void SetCameraMode(CameraMode mode)
        {
            if (mode == CameraMode)
                return;

            if (mode == CameraMode.Fixed || mode == CameraMode.None)
            {
                Debug.LogError($"You can't use this function to set camera mode to {mode}");
                return;
            }

            SaveCurrentCameraSettings();
            CameraMode = mode;

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
                DefaultRotation = 45;
                TargetRotation = Rotation;
                lockCamera = true;
                Distance = 55;
                rotationRange = new Vector2(40, 60);
                heightRange = new Vector2(35, 65);
                zoomRange = new Vector2(25, 80);
            }

#if UNITY_EDITOR
            // zoomRange = new Vector2(30, 500);
#endif
        }

        public void UpdatePlayerHP(int hp, int maxHp)
        {
            if (hp < 0)
                hp = 0;

            var percent = hp / (float)maxHp;
            CharacterDetailBox.HpDisplay.gameObject.SetActive(true);
            CharacterDetailBox.HpDisplay.text = $"HP: {hp} / {maxHp} ({percent * 100f:F1}%)";
            CharacterDetailBox.HpSlider.value = (float)hp / (float)maxHp;

            PlayerState.Hp = hp;
            PlayerState.MaxHp = maxHp;
        }

        public void UpdatePlayerSP(int sp, int maxSp)
        {
            if (sp < 0)
                sp = 0;

            var percent = sp / (float)maxSp;
            CharacterDetailBox.SpDisplay.gameObject.SetActive(true);
            CharacterDetailBox.SpDisplay.text = $"SP: {sp} / {maxSp} ({percent * 100f:F1}%)";
            CharacterDetailBox.SpSlider.value = (float)sp / (float)maxSp;

            PlayerState.Sp = sp;
            PlayerState.MaxSp = maxSp;
        }


        public void UpdatePlayerExp(int exp, int maxExp)
        {
            if (maxExp <= 0)
            {
                CharacterDetailBox.ExpDisplay.text = "";
                CharacterDetailBox.ExpSlider.gameObject.SetActive(false);
                return;
            }

            var percent = exp / (float)maxExp;

            CharacterDetailBox.ExpSlider.gameObject.SetActive(true);

            var showValue = GameConfig.Data.ShowBaseExpValue;
            var showPercent = GameConfig.Data.ShowBaseExpPercent;

            if (showValue)
            {
                if (showPercent)
                    CharacterDetailBox.ExpDisplay.text = $"XP: {exp} / {maxExp} ({percent * 100f:F1}%)";
                else
                    CharacterDetailBox.ExpDisplay.text = $"XP: {exp} / {maxExp}";
            }
            else if (showPercent)
                CharacterDetailBox.ExpDisplay.text = $"{percent * 100f:F1}%";
            else
                CharacterDetailBox.ExpDisplay.text = $"";

            CharacterDetailBox.ExpSlider.value = percent;
            //
            // CharacterDetailBox.ExpDisplay.text = $"XP: {exp} / {maxExp} ({percent * 100f:F1}%)";
            // CharacterDetailBox.ExpSlider.value = percent;
        }

        public void UpdatePlayerJobExp(int exp, int maxExp)
        {
            if (maxExp <= 0)
            {
                CharacterDetailBox.JobExpDisplay.text = "";
                CharacterDetailBox.JobExpSlider.gameObject.SetActive(false);
                return;
            }

            var percent = exp / (float)maxExp;

            CharacterDetailBox.JobExpSlider.gameObject.SetActive(true);

            var showValue = GameConfig.Data.ShowJobExpValue;
            var showPercent = GameConfig.Data.ShowJobExpPercent;

            if (showValue)
            {
                if (showPercent)
                    CharacterDetailBox.JobExpDisplay.text = $"XP: {exp} / {maxExp} ({percent * 100f:F1}%)";
                else
                    CharacterDetailBox.JobExpDisplay.text = $"XP: {exp} / {maxExp}";
            }
            else if (showPercent)
                CharacterDetailBox.JobExpDisplay.text = $"{percent * 100f:F1}%";
            else
                CharacterDetailBox.JobExpDisplay.text = $"";

            CharacterDetailBox.JobExpSlider.value = percent;
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

            var loader = Addressables.LoadAssetAsync<RoSpriteData>("Assets/Sprites/Misc/cursors.spr");
            loader.Completed += ah =>
            {
                sprite.OnSpriteDataLoadNoCollider(ah.Result);
                sprite.ChangeActionExact(3);

                var mat = EffectHelpers.NoZTestMaterial;

                var renderer = sprite.GetComponent<RoSpriteRendererStandard>();
                renderer.SetOverrideMaterial(mat);
            };

            return go;
        }

        public void SetSelectedTarget(ServerControllable target, string name, bool isAlly, bool isHard)
        {
            if (selectedSprite == null)
                selectedSprite = CreateSelectedCursorObject();

            var color = "";
            if (!isAlly)
                color = "<color=#FFAAAA>";
            if (isHard)
                color = "<color=#FF4444>";

            if (SelectedTarget != null && SelectedTarget != target)
                SelectedTarget.HideTargetNamePlate();

            hasSelection = true;
            SelectedTarget = target;
            SelectedTarget.ShowTargetNamePlate(color + name);
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
            if (SelectedTarget != null)
                SelectedTarget.HideTargetNamePlate();
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

        public void UpdateCameraSize()
        {
            if (Screen.width == 0)
                return; //wut?
            var scale = 1f / (1080f / Screen.height);
            CanvasScaler.scaleFactor = GameConfig.Data.MasterUIScale;
            lastWidth = Screen.width;
            lastHeight = Screen.height;
            UiManager.Instance.FitFloatingWindowsIntoPlayArea();
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


        private RoSpriteAnimator GetClosestOrEnemy(RaycastHit[] hits, bool preferEnemy)
        {
            var closestAnim = GetHitAnimator(hits[0]);
            var closestHit = hits[0];

            //var log = $"{hits.Length} {closestAnim.Controllable.gameObject.name}{closestHit.distance}{closestAnim.Controllable.IsAlly}";

            if (hits.Length == 1)
            {
                if (closestAnim.State == SpriteState.Dead && closestAnim.Type != SpriteType.Player)
                    return null;
                if (closestAnim.IsHidden)
                    return null;
                return closestAnim;
            }

            var isOk = closestAnim.State != SpriteState.Dead && !closestAnim.IsHidden;
            var isAlly = closestAnim.Controllable.IsAlly;


            for (var i = 1; i < hits.Length; i++)
            {
                var hit = hits[i];
                var anim = GetHitAnimator(hit);

                if (anim.IsHidden)
                    continue;

                void MakeTarget()
                {
                    closestAnim = anim;
                    closestHit = hit;
                    isAlly = anim.Controllable.IsAlly;
                    isOk = true;
                }

                if (anim.State == SpriteState.Dead && anim.Type != SpriteType.Player)
                    continue;

                if (!isOk)
                {
                    MakeTarget();
                    continue;
                }

                //log += $" {anim.Controllable.gameObject.name}{hit.distance}{anim.Controllable.IsAlly}-{anim.State}";

                if (preferEnemy)
                {
                    if (!isAlly && anim.Controllable.IsAlly)
                        continue;
                    if (isAlly && !anim.Controllable.IsAlly)
                    {
                        MakeTarget();
                        continue;
                    }
                }
                else
                {
                    if (isAlly && !anim.Controllable.IsAlly)
                        continue;
                    if (!isAlly && anim.Controllable.IsAlly)
                    {
                        MakeTarget();
                        continue;
                    }
                }

                if ((hit.distance < closestHit.distance))
                    MakeTarget();
            }

            //log += $" : {closestAnim.Controllable.gameObject.name}{closestHit.distance}{closestAnim.Controllable.IsAlly} {isOk}";
            //Debug.Log(log);


            if (isOk)
                return closestAnim;

            return null;
        }

        private bool FindItemUnderCursor(Ray ray, out GroundItem item)
        {
            item = null;

            var itemHits = Physics.Raycast(ray, out var hit, MaxClickDistance, 1 << LayerMask.NameToLayer("Item"));
            if (itemHits)
            {
                item = hit.transform.parent.GetComponent<GroundItem>();
                return item != null;
            }

            return false;
        }

        private bool FindEntityUnderCursor(Ray ray, bool preferEnemy, out ServerControllable target)
        {
            //one of these days we'll be able to target allies and that preferEnemy value will matter
            target = null;
            var characterHits = Physics.RaycastAll(ray, MaxClickDistance, (1 << LayerMask.NameToLayer("Characters")));
            if (characterHits.Length == 0)
                return false;

            var anim = GetClosestOrEnemy(characterHits, preferEnemy);
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

            var isSkillEnemyTargeted = hasSkillOnCursor && cursorSkillTarget == SkillTarget.Enemy;
            var isSkillAllyTargeted = hasSkillOnCursor && cursorSkillTarget == SkillTarget.Ally;
            var canTargetPartyList = hasSkillOnCursor && isSkillAllyTargeted && UiManager.Instance.PartyPanel.HoverEntry != null;

            //cancel skill on cursor if we can't use a skill
            if (hasSkillOnCursor && (!isAlive || isSitting))
            {
                hasSkillOnCursor = false;
                UiManager.Instance.PartyPanel.EndSkillOnCursor();
            }

            if (hasSkillOnCursor && cursorSkillTarget == SkillTarget.Any)
            {
                if ((Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
                {
                    preferEnemyTarget = true;
                    isSkillEnemyTargeted = true;
                    isSkillAllyTargeted = false;
                }
                else
                {
                    preferEnemyTarget = false;
                    isSkillEnemyTargeted = false;
                    isSkillAllyTargeted = true;
                }
            }

            var walkMask = 1 << LayerMask.NameToLayer("WalkMap");
            var groundMask = 1 << LayerMask.NameToLayer("Ground");

            var ignoreEntity = false; //(Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt));

            var hasEntity = FindEntityUnderCursor(ray, preferEnemyTarget, out var mouseTarget);
            var hasGround = FindMapPositionUnderCursor(ray, out var groundPosition, out var intersectLocation, walkMask);
            if (!hasGround) hasGround = FindMapPositionUnderCursor(ray, out groundPosition, out intersectLocation, groundMask, true);
            var hasSrcPos = WalkProvider.GetMapPositionForWorldPosition(Target.transform.position, out var srcPosition);
            var hasTargetedSkill = hasSkillOnCursor && (isSkillEnemyTargeted || isSkillAllyTargeted);
            var hasGroundSkill = hasSkillOnCursor && cursorSkillTarget == SkillTarget.Ground;
            var hasItem = FindItemUnderCursor(ray, out var item);

            var canInteract = hasEntity && isAlive && !isOverUi && !isHolding && !hasGroundSkill && !ignoreEntity;
            var canCurrentlyTarget = canInteract &&
                                     ((hasSkillOnCursor && ((mouseTarget.IsAlly && isSkillAllyTargeted) || (!mouseTarget.IsAlly && isSkillEnemyTargeted))) ||
                                      !mouseTarget.IsAlly);
            var canClickEnemy = canCurrentlyTarget && mouseTarget.CharacterType != CharacterType.NPC && mouseTarget.IsInteractable;
            var canClickNpc = canInteract && !hasSkillOnCursor && mouseTarget.CharacterType == CharacterType.NPC && mouseTarget.IsInteractable;
            var canClickItem = hasItem && !canClickEnemy && !hasSkillOnCursor && !isOverUi;
            var canClickGround = hasGround && isAlive && (!isOverUi || isHolding) && !canClickEnemy && !canClickNpc && !hasTargetedSkill && !canClickItem;
            var canMove = ClickDelay <= 0 && isAlive && !isSitting && !Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl);
            var showEntityName = hasEntity && !isOverUi;

            var cancelSkill = (hasSkillOnCursor && rightClick) || (hasSkillOnCursor && leftClick && hasTargetedSkill && !hasEntity && !canTargetPartyList);
            if (cancelSkill)
            {
                hasSkillOnCursor = hasGroundSkill = hasTargetedSkill = leftClick = rightClick = false; //lol
                UiManager.Instance.PartyPanel.EndSkillOnCursor();
            }


            var displayCursor = canClickEnemy ? GameCursorMode.Attack : GameCursorMode.Normal;
            if (canClickNpc) displayCursor = GameCursorMode.Dialog;
            if (hasSkillOnCursor) displayCursor = GameCursorMode.SkillTarget;
            if (hasItem && showEntityName && mouseTarget.IsAlly)
                showEntityName = false; //don't show friendly names if you could instead pick up an item

            if (canInteract && mouseTarget.IsAlly && mouseTarget.CharacterType == CharacterType.Player && rightClick)
            {
                UiManager.Instance.RightClickMenuWindow.RightClickPlayer(mouseTarget);
                return displayCursor;
            }

            if (showEntityName)
            {
                //if our new mouseover target is different from last time, we need to swap over
                if (mouseHoverTarget != mouseTarget)
                {
                    mouseHoverTarget?.HideHoverNamePlate();

                    mouseHoverTarget = mouseTarget;

                    if (mouseHoverTarget.IsAlly)
                        mouseHoverTarget.ShowHoverNamePlate(mouseHoverTarget.DisplayName);
                    else
                        mouseHoverTarget.ShowHoverNamePlate("<color=#FFAAAA>" + mouseHoverTarget.DisplayName); //yeah this is stupid
                }
            }
            else
            {
                mouseHoverTarget?.HideHoverNamePlate();
                mouseHoverTarget = null;
            }

            if (!isOverUi && (leftClick || rightClick) && ActivePromptType != PromptType.None)
            {
                switch (ActivePromptType)
                {
                    case PromptType.RightClickMenu:
                        UiManager.Instance.RightClickMenuWindow.HideWindow();
                        break;
                    //should probably add other prompts here
                }

                ActivePromptType = PromptType.None;
                canMove = false;
            }

            if (!isOverUi && InItemInputBox && leftClick)
            {
                UiManager.Instance.DropCountConfirmationWindow.gameObject.SetActive(false);
                InItemInputBox = false;
            }

            if (!isOverUi && InTextInputBox && leftClick)
            {
                UiManager.Instance.TextInputWindow.HideInputWindow();
                InTextInputBox = false;
            }

            if (!isOverUi && InYesNoPrompt && leftClick)
            {
                UiManager.Instance.YesNoOptionsWindow.HideInputWindow();
                InYesNoPrompt = false;
            }

            if (canClickNpc && leftClick)
            {
                NetworkManager.Instance.SendNpcClick(mouseTarget.Id);
                return displayCursor;
            }

            if ((canClickEnemy || canTargetPartyList) && leftClick)
            {
                if (!hasTargetedSkill)
                    NetworkManager.Instance.SendAttack(mouseTarget.Id);
                else
                {
                    if (canTargetPartyList)
                    {
                        var partyMember = UiManager.Instance.PartyPanel.HoverEntry.PartyMemberInfo;
                        if (partyMember != null && partyMember.Controllable != null)
                            mouseTarget = partyMember.Controllable;
                    }

                    if (!isCursorSkillItem)
                        NetworkManager.Instance.SendSingleTargetSkillAction(mouseTarget.Id, cursorSkill, cursorSkillLvl);
                    else
                        NetworkManager.Instance.SendUseItem(cursorItemId, mouseTarget.Id);
                    hasSkillOnCursor = false;
                    isCursorSkillItem = false;
                    UiManager.Instance.PartyPanel.EndSkillOnCursor();
                }

                return displayCursor;
            }

            if (canClickItem)
            {
                WalkProvider.DisableRenderer();
                UiManager.Instance.ItemOverlay.ShowItem(item);

                if (leftClick)
                {
                    NetworkManager.Instance.SendPickUpItem(item.EntityId);
                }

                return Input.GetMouseButton(0) ? GameCursorMode.PickUpMouseDown : GameCursorMode.PickUp;
            }
            else
                UiManager.Instance.ItemOverlay.HideItem();

            if (leftClick && hasSkillOnCursor && hasTargetedSkill && !canClickEnemy)
            {
                //they clicked, they have a targeted skill, but they have no target
                hasSkillOnCursor = false;
                UiManager.Instance.PartyPanel.EndSkillOnCursor();
                return displayCursor;
            }

            //if our cursor isn't over ground there's no real point in continuing.
            if (!hasGround)
                return displayCursor;

            //while sitting or holding shift and clicking turns your character to face where you clicked
            if (!isOverUi && leftClick &
                (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) || controllable.SpriteAnimator.State == SpriteState.Sit))
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
                CharacterDetailBox.DebugInfo.text = groundPosition.ToString();
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
                UiManager.Instance.PartyPanel.EndSkillOnCursor();
                return GameCursorMode.Normal; //the skill is no longer on our cursor so no point in keeping skill cursor
            }

            //everything left is movement related so if we can't move or can't click ground, we're done.
            if (!canClickGround || !canMove)
                return displayCursor;

            //if they aren't trying to move we're also done. 
            if (!leftClick && !isHolding) return displayCursor;

            var dest = groundPosition;

            //if we can't path to the tile they clicked on, instead get the closest tile that is valid and we'll walk to that instead.
            if (!hasValidPath && WalkProvider.GetNextWalkableTileForClick(controllable.CellPosition, groundPosition, out dest))
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
            chatMessages.Clear();
            chatMessages.Add("Welcome to Ragnarok Rebuild!");
            RefreshChatWindow();
        }

        private readonly StringBuilder chatBuilder = new();

        private void RefreshChatWindow()
        {
            if (chatMessages.Count > 50)
                chatMessages.RemoveAt(0);

            foreach (var c in chatMessages)
                chatBuilder.AppendLine(c);

            TextBoxText.text = chatBuilder.ToString();
            chatBuilder.Clear();

            StartCoroutine(ApplyScrollPosition(TextBoxScrollRect, 0));
        }

        IEnumerator ApplyScrollPosition(ScrollRect sr, float verticalPos)
        {
            yield return new WaitForEndOfFrame();
            sr.verticalNormalizedPosition = verticalPos;
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)sr.transform);
        }

        public void AppendChatText(string txt)
        {
            if (string.IsNullOrWhiteSpace(txt))
                return;

            chatMessages.Add(txt);
            RefreshChatWindow();

            // TextBoxText.ForceMeshUpdate();
            // LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)TextBoxScrollRect.transform);
            // TextBoxScrollRect.verticalNormalizedPosition = 0;
            // Debug.Log(TextBoxScrollRect.verticalNormalizedPosition);
        }

        public void AppendChatText(string txt, TextColor color)
        {
            if (string.IsNullOrWhiteSpace(txt))
                return;

            var c = color switch
            {
                TextColor.Party => "<color=#77FF77>",
                TextColor.Job => "color=#99CCFF>",
                TextColor.Skill => "color=#00fbfb>",
                TextColor.Equipment => "color=#00fbfb>",
                TextColor.Item => "color=#00fbfb>",
                TextColor.Error => "color=#ed0000>",
                _ => ""
            };

            chatMessages.Add(c + txt + "</color>");
            RefreshChatWindow();

            // TextBoxText.text += Environment.NewLine + txt;
            // TextBoxText.ForceMeshUpdate();
            // LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)TextBoxScrollRect.transform);
            // TextBoxScrollRect.verticalNormalizedPosition = 0;
        }

        public void AppendNotice(string text)
        {
            AppendChatText($"<color=yellow><i>{text}</i></color>");
        }

        public void AppendError(string txt)
        {
            if (string.IsNullOrWhiteSpace(txt))
                return;

            chatMessages.Add("<color=red>Error</color>: " + txt);
            RefreshChatWindow();
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

            try
            {
                ClientCommandHandler.HandleClientCommand(this, controllable, text);
            }
            catch (Exception)
            {
                AppendChatText($"<color=yellow>Error</color>: Command could not be parse.");
            }

            lastMessage = text;
        }

        public void AttachEffectToEntity(string effect, GameObject target, int ownerId = -1)
        {
            switch (effect.ToLower())
            {
                case "exit":
                    ExitEffect.LaunchExitAtLocation(target.transform.position);
                    return;
                case "teleport":
                    TeleportEffect.LaunchTeleportAtLocation(target.transform.position + new Vector3(0f, 0f, 4f));
                    return;
                case "entry":
                    EntryEffect.LaunchEntryAtLocation(target.transform.position + new Vector3(0f, 0f, 4f));
                    return;
            }

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

            switch (asset.Name)
            {
                case "HealLow":
                    HealEffect.Create(target, 0);
                    return;
                case "HealMid":
                    HealEffect.Create(target, 1);
                    return;
                case "HealHigh":
                    HealEffect.Create(target, 2);
                    return;
                case "Hiding":
                    HideEffect.AttachHideEffect(target);
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

            Debug.Log($"Loading effect asset {asset.PrefabName}");
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

        public void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
                isHolding = false;
        }

        private IEnumerator DelayedExecution(Action action, float time)
        {
            yield return new WaitForSeconds(time);
            action();
        }

        public void DelayedExecuteAction(Action action, float time)
        {
            StartCoroutine(DelayedExecution(action, time));
        }

        public void FixedUpdate()
        {
            if (ShakeTime <= 0)
            {
                shakeTarget = Vector3.zero;
                return;
            }

            if (shakeStepProgress <= 0)
                shakeStepProgress += 1f;

            shakePos = shakeTarget;
            shakeTarget = Random.insideUnitSphere * Mathf.Clamp(ShakeTime, 0, 0.3f);
        }

        public void Update()
        {
            if (IsInErrorState && Input.GetKeyDown(KeyCode.Space))
            {
                NetworkManager.IsLoaded = false;
                SceneManager.LoadScene(0);
            }

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
            var blockScrollZoom = false;

            if (!UiManager.Instance.IsCanvasVisible)
            {
                pointerOverUi = false;
                selected = null;
            }

            //special case, we want to be able to click under the text window, but it needs to be a raycast target so we can scroll wheel chat
            if (pointerOverUi && selected == null)
            {
                var pointerEvent = ExtendedStandaloneInputModule.GetPointerEventData();
                if (pointerEvent.pointerEnter.name == "ChatViewport")
                {
                    blockScrollZoom = true;
                    pointerOverUi = false;
                    selected = null;
                }

                if (pointerEvent.pointerEnter.name == "Partymember")
                {
                    pointerOverUi = false;
                    selected = null;
                }

                if (pointerEvent.pointerEnter.name == "TopLeftCharacterUI")
                {
                    pointerOverUi = false;
                    selected = null;

                    if (Input.GetMouseButtonDown(1) && UiManager.Instance.RightClickMenuWindow.RightClickSelf())
                    {
                        pointerOverUi = true;
                    }
                }
            }

            InTextBox = false;
            if (selected != null && !InItemInputBox)
                InTextBox = selected.GetComponent<TMP_InputField>() != null;

            var inInputUI = InTextBox || InItemInputBox || InTextInputBox;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                //Debug.Log("Escape pressed, inTextBox: " + inTextBox);
                //Debug.Log(EventSystem.current.currentSelectedGameObject);

                if (hasSkillOnCursor)
                {
                    hasSkillOnCursor = false;
                    UiManager.Instance.PartyPanel.EndSkillOnCursor();
                }

                if (InItemInputBox)
                {
                    UiManager.Instance.DropCountConfirmationWindow.gameObject.SetActive(false);
                    InItemInputBox = false;
                }
                else if (InTextInputBox)
                {
                    UiManager.Instance.TextInputWindow.HideInputWindow();
                    InTextInputBox = false;
                }
                else if (InTextBox)
                {
                    InTextBox = false;
                    TextBoxInputField.text = "";
                }
                else
                {
                    UiManager.Instance.CloseLastWindow();
                }

                EventSystem.current.SetSelectedGameObject(null);
            }

            if (InTextBox)
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
                if (InItemInputBox)
                {
                    UiManager.Instance.DropCountConfirmationWindow.SubmitDrop();
                    EventSystem.current.SetSelectedGameObject(null);
                }
                else if (InTextInputBox)
                {
                    UiManager.Instance.TextInputWindow.Submit();
                }
                else if (!InTextBox)
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
                        InTextBox = false;

                        //Debug.Log(text);
                        //TextBoxInputField.DeactivateInputField(true);
                        TextBoxInputField.text = "";
                        TextBoxInputField.ActivateInputField();
                        //EventSystem.current.SetSelectedGameObject(null);
                    }
                }
            }

            if (!inInputUI && Input.GetKeyDown(KeyCode.R))
            {
                if (controllable.SpriteAnimator.State == SpriteState.Dead || !controllable.IsCharacterAlive || NetworkManager.Instance.PlayerState.Hp == 0)
                    NetworkManager.Instance.SendRespawn(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
            }

            if (!inInputUI && Input.GetKeyDown(KeyCode.Insert))
            {
                if (controllable.SpriteAnimator.State == SpriteState.Idle || controllable.SpriteAnimator.State == SpriteState.Standby)
                    NetworkManager.Instance.ChangePlayerSitStand(true);
                if (controllable.SpriteAnimator.State == SpriteState.Sit)
                    NetworkManager.Instance.ChangePlayerSitStand(false);
            }

            if (!inInputUI && Input.GetKeyDown(KeyCode.M))
            {
                AudioManager.Instance.ToggleMute();
            }

            //if (Input.GetKeyDown(KeyCode.S))
            //	controllable.SpriteAnimator.Standby = true;

            if (!inInputUI && Input.GetKeyDown(KeyCode.Space))
            {
                //Debug.Log(controllable.IsWalking);
                //if(controllable.IsWalking)
                NetworkManager.Instance.StopPlayer();
            }

            if (!inInputUI && Input.GetKeyDown(KeyCode.S))
                UiManager.Instance.SkillManager.ToggleVisibility();

            if (!inInputUI && Input.GetKeyDown(KeyCode.O))
                UiManager.Instance.ConfigManager.ToggleVisibility();

            if (!inInputUI && Input.GetKeyDown(KeyCode.W))
            {
#if UNITY_EDITOR
                if (Input.GetKey(KeyCode.LeftShift))
                {
                    if (!WarpPanel.activeInHierarchy)
                        WarpPanel.GetComponent<WarpWindow>().ShowWindow();
                    else
                        WarpPanel.GetComponent<WarpWindow>().HideWindow();
                }
                else
#endif
                if (PlayerState.Instance.HasCart)
                    UiManager.Instance.CartWindow.ToggleVisibility();
            }

            if (!inInputUI && Input.GetKeyDown(KeyCode.Q))
                UiManager.Instance.EquipmentWindow.ToggleVisibility();

            if (!inInputUI && Input.GetKeyDown(KeyCode.E))
                UiManager.Instance.InventoryWindow.ToggleVisibility();

            if (!inInputUI && Input.GetKeyDown(KeyCode.A))
                UiManager.Instance.StatusWindow.ToggleVisibility();

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

            if (!inInputUI && !pointerOverUi && Input.GetMouseButtonDown(1))
            {
                if (Time.timeSinceLevelLoad - LastRightClick < 0.3f)
                {
                    if (Input.GetKey(KeyCode.LeftShift))
                        Height = 50;
                    else
                        TargetRotation = CameraMode == CameraMode.Normal ? 0 : DefaultRotation; // (rotationRange.y + rotationRange.x) / 2f;
                }

                LastRightClick = Time.timeSinceLevelLoad;
            }

            if (!inInputUI && !pointerOverUi && Input.GetMouseButton(1))
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
                    {
                        if (TargetRotation > 180 && rotationRange.x < 0)
                            TargetRotation -= 360;

                        TargetRotation = Mathf.Clamp(TargetRotation, rotationRange.x, rotationRange.y);

                        if (TargetRotation < 0)
                            TargetRotation += 360;
                    }
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


            if (IsInNPCInteraction && !pointerOverUi && Input.GetMouseButtonDown(0) && RefineItemWindow.Instance == null)
            {
                NetworkManager.Instance.SendNpcAdvance();
                isHolding = false;
                //noHold = true;
                return; //no point in doing other screencast stuff if we're still talking to the npc.
            }

            //DoScreenCast(pointerOverUi);

            var cursor = ScreenCastV2(pointerOverUi);
            if (cursor != GameCursorMode.SkillTarget)
            {
                CursorManager.UpdateCursor(cursor);
                UiManager.Instance.ActionTextDisplay.EndActionTextDisplay();
            }
            else
            {
                if (!isCursorSkillItem)
                    UiManager.Instance.ActionTextDisplay.SetSkillTargeting(cursorSkill, cursorSkillLvl);
                else
                {
                    var item = PlayerState.Inventory.GetInventoryItem(cursorItemId);
                    UiManager.Instance.ActionTextDisplay.SetItemTargeting(item.ProperName(), cursorSkillLvl);
                }

                CursorManager.UpdateCursor(cursor, cursorSkillLvl);
            }


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

            if (!pointerOverUi && screenRect.Contains(Input.mousePosition) && !blockScrollZoom)
            {
                if (!hasSkillOnCursor)
                    Distance += Input.GetAxis("Mouse ScrollWheel") * 20 * ctrlKey;
                else
                {
                    if (canChangeCursorLevel)
                    {
                        skillScroll += Input.GetAxis("Mouse ScrollWheel") * 10f;
                        skillScroll = Mathf.Clamp(skillScroll, 1, cursorMaxSkillLvl);
                        cursorSkillLvl = Mathf.RoundToInt(skillScroll);
                    }
                    // Debug.Log(skillScroll);
                }
            }

            if (pointerOverUi && UiManager.Instance.PartyPanel.HoverEntry != null && !blockScrollZoom)
            {
                if (canChangeCursorLevel) //probably should put this in a function or something instead of duplicating the code...
                {
                    skillScroll += Input.GetAxis("Mouse ScrollWheel") * 10f;
                    skillScroll = Mathf.Clamp(skillScroll, 1, cursorMaxSkillLvl);
                    cursorSkillLvl = Mathf.RoundToInt(skillScroll);
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

            ShakeTime -= Time.deltaTime;
            shakeStepProgress -= Time.deltaTime;
            var curShake = Vector3.Lerp(shakePos, shakeTarget, Mathf.Clamp01(1 - shakeStepProgress));

            transform.position = CurLookAt + pos;
            transform.LookAt(CurLookAt + new Vector3(0f, 1f, 0f), Vector3.up);

            transform.position += curShake;

            if (!inInputUI && Input.GetKeyDown(KeyCode.T))
                NetworkManager.Instance.RandomTeleport();

            if (!inInputUI && Input.GetKeyDown(KeyCode.F3))
                UseTTFDamage = !UseTTFDamage;

            if (Input.GetKeyDown(KeyCode.F4))
            {
                UseSmoothPixel = !UseSmoothPixel;
                Shader.SetKeyword(smoothPixelKeyword, UseSmoothPixel);
            }

            if (!inInputUI && Input.GetKeyDown(KeyCode.Tab))
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

            if (IsBlindActive)
            {
                BlindStrength = Mathf.Lerp(BlindStrength, BlindTargetDistance, Time.deltaTime * 3f);
                Shader.SetGlobalFloat(RoBlindDistance, BlindStrength);
            }
            else
            {
                if (TargetControllable != null)
                {
                    if (BlindStrength < 200f)
                    {
                        BlindStrength += 200f * Time.deltaTime;
                        Shader.SetGlobalFloat(RoBlindDistance, BlindStrength);
                    }
                    else
                        Shader.DisableKeyword("BLINDEFFECT_ON");
                }
            }

#if UNITY_EDITOR
            if (!inInputUI && Input.GetKeyDown(KeyCode.Z))
            {
                if (Mathf.Approximately(Time.timeScale, 1f))
                    Time.timeScale = 0.1f;
                else
                    Time.timeScale = 1f;
            }
#endif

            if (CinemachineMode)
                CinemachineFollow();

            UpdateFog();
        }

        private void OnApplicationQuit()
        {
            SaveCurrentCameraSettings();
        }
    }
}