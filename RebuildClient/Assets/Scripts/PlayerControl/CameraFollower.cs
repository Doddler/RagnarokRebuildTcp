using System;
using System.Collections.Generic;
using System.Text;
using Assets.Scripts.Effects;
using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.MapEditor;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using Assets.Scripts.Sprites;
using Assets.Scripts.UI;
using Assets.Scripts.Utility;
using JetBrains.Annotations;
using PlayerControl;
using RebuildSharedData.ClientTypes;
using RebuildSharedData.Config;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Utility;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEditor.Recorder.Examples;
#endif

namespace Assets.Scripts
{
    public class CameraFollower : MonoBehaviour
    {
        public GameObject ListenerProbe;

        public Texture2D NormalCursorTexture;
        public Texture2D AttackCursorTexture;
        public Texture2D TalkCursorTexture;
        public Texture2D TargetCursorTexture;
        public Texture2D TargetCursorNoTargetTexture;
        public RoSpriteData TargetSprite;
        // public GameObject LevelUpPrefab;
        // public GameObject ResurrectPrefab;
        // public GameObject DeathPrefab;
        public TextAsset LevelChart;

        private static CameraFollower _instance;
        public RoWalkDataProvider WalkProvider;

        public Canvas UiCanvas;
        public TextMeshProUGUI TargetUi;
        public TextMeshProUGUI PlayerTargetUi;
        public TextMeshProUGUI HpDisplay;
        public Slider HpSlider;
        public TextMeshProUGUI ExpDisplay;
        public Slider ExpSlider;
        public TMP_InputField TextBoxInputField;
        public CanvasScaler CanvasScaler;
        public TextMeshProUGUI ErrorNoticeUi;

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
        public GameObject SelectedTarget;
        private GameObject selectedSprite;
        private string targetText;
        private GameObject ClickEffectPrefab;

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
        private int cursorSkillLvl;

        public bool CinemachineMode;
        public VideoRecorder Recorder;

        private const bool CinemachineCenterPlayerOnMap = true;
        private const bool CinemachineHidePlayerObject = true;

        public float FogNearRatio = 0.3f;
        public float FogFarRatio = 4f;
        
#if DEBUG
        private const float MaxClickDistance = 500;
        
#else
        private const float MaxClickDistance = 150;
#endif

        public int ExpForLevel(int lvl) => levelReqs[lvl];

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

            ClickEffectPrefab = Resources.Load<GameObject>($"MoveNotice");

            //targetWalkable = Target.GetComponent<EntityWalkable>();
            //if (targetWalkable == null)
            //    targetWalkable = Target.AddComponent<EntityWalkable>();

            //DoMapSpawn();
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

        public void SetSelectedTarget(GameObject target, string name, bool isAlly, bool isHard)
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

            hasSelection = true;
            SelectedTarget = target;
            PlayerTargetUi.text = color + name;
            PlayerTargetUi.gameObject.SetActive(true);
            selectedSprite.SetActive(true);
            //Debug.Log("Selecting target: '" + name + "' with game object: " + target);
            UpdateSelectedTarget();
        }

        public void ClearSelected()
        {
            //Debug.Log("Clearing target.");
            hasSelection = false;
            PlayerTargetUi.text = "";
            PlayerTargetUi.gameObject.SetActive(false);
            if (selectedSprite != null)
                selectedSprite.SetActive(false);
        }

        private void UpdateSelectedTarget()
        {
            if (!hasSelection || SelectedTarget == null || IsInNPCInteraction)
            {
                if (PlayerTargetUi.gameObject.activeInHierarchy)
                    ClearSelected();
                return;
            }

            if (!PlayerTargetUi.gameObject.activeInHierarchy)
                PlayerTargetUi.gameObject.SetActive(true);

            var screenPos = Camera.WorldToScreenPoint(SelectedTarget.transform.position);
            var reverseScale = 1f / CanvasScaler.scaleFactor;

            PlayerTargetUi.rectTransform.anchoredPosition = new Vector2(screenPos.x * reverseScale,
                ((screenPos.y - UiCanvas.pixelRect.height) - 30) * reverseScale);

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

        public void ChangeCursor(Texture2D cursorTexture)
        {
            if (currentCursor == cursorTexture)
                return;

            if (cursorTexture == TalkCursorTexture || cursorTexture == TargetCursorTexture || cursorTexture == TargetCursorNoTargetTexture)
                Cursor.SetCursor(cursorTexture, new Vector2(16, 14), CursorMode.Auto);
            else
                Cursor.SetCursor(cursorTexture, Vector2.zero, CursorMode.Auto);

            currentCursor = cursorTexture;
        }

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

        private void DoScreenCast(bool isOverUi)
        {
            if (IsInNPCInteraction && !isOverUi && Input.GetMouseButtonDown(0))
            {
                NetworkManager.Instance.SendNpcAdvance();
                isHolding = false;
                noHold = true;
                return; //no point in doing other screencast stuff if we're still talking to the npc.
            }
            
            var ray = Camera.ScreenPointToRay(Input.mousePosition);

            var hasHitCharacter = false;

            var characterHits = Physics.RaycastAll(ray, MaxClickDistance, (1 << LayerMask.NameToLayer("Characters")));
            var hasHitMap = Physics.Raycast(ray, out var groundHit, MaxClickDistance, (1 << LayerMask.NameToLayer("WalkMap")));

            if (characterHits.Length > 0)
                hasHitCharacter = true;

            if (isHolding || isOverUi || CinemachineMode)
                hasHitCharacter = false;
            
            //Debug.Log(string.Join(", ", characterHits.Select(c => c.transform.name)));

            if (hasHitCharacter)
            {
                //var anim = charHit.transform.gameObject.GetComponent<RoSpriteAnimator>();
                var anim = GetClosestOrEnemy(characterHits);
                if (anim == null)
                {
                    hasHitCharacter = false; //back out if our hit is a false positive (the object is dead or dying for example)
                    ChangeCursor(NormalCursorTexture);
                }

                if (hasHitCharacter)
                {
                    var screenPos = Camera.main.WorldToScreenPoint(anim.Controllable.gameObject.transform.position);
                    var color = "";
                    if (!anim.Controllable.IsAlly && anim.Controllable.CharacterType != CharacterType.NPC)
                    {
                        if (hasSkillOnCursor)
                        {
                            ChangeCursor(TargetCursorTexture);
                            if (Input.GetMouseButtonDown(0))
                            {
                                NetworkManager.Instance.SendSingleTargetSkillAction(anim.Controllable.Id, cursorSkill, cursorSkillLvl);
                                hasSkillOnCursor = false;
                                return;
                            }
                        }
                        else
                            ChangeCursor(AttackCursorTexture);
                        
                            
                            //CastLockOnEffect.Create(3f, anim.Controllable.gameObject);
                        
                        color = "<color=#FFAAAA>";
                        hasHitMap = false;
                    }
                    else
                    {
                        if (anim.Controllable.IsInteractable)
                        {
                            ChangeCursor(TalkCursorTexture);
                            hasHitMap = false;
                        }
                        else
                            ChangeCursor(NormalCursorTexture);
                    }

                    var reverseScale = 1f / CanvasScaler.scaleFactor;

                    TargetUi.rectTransform.anchoredPosition = new Vector2(screenPos.x * reverseScale,
                        ((screenPos.y - UiCanvas.pixelRect.height) - 30) * reverseScale);
                    TargetUi.text = color + anim.Controllable.DisplayName;

                    if (anim.Controllable.CharacterType == CharacterType.Monster)
                    {
                        if (Input.GetMouseButtonDown(0))
                        {
                            NetworkManager.Instance.SendAttack(anim.Controllable.Id);
                            isHolding = false;
                            noHold = true;
                        }
                    }


                    if (anim.Controllable.CharacterType == CharacterType.NPC && anim.Controllable.IsInteractable)
                    {
                        if (Input.GetMouseButtonDown(0))
                        {
                            NetworkManager.Instance.SendNpcClick(anim.Controllable.Id);
                            isHolding = false;
                            noHold = true;
                        }
                    }
                }
            }
            else
            {
                if(hasSkillOnCursor)
                    ChangeCursor(TargetCursorNoTargetTexture);
                else
                    ChangeCursor(NormalCursorTexture);
            }

            ClickDelay -= Time.deltaTime;

            if (Input.GetMouseButtonUp(0))
                ClickDelay = 0;
            
            
            if ((Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)) && hasSkillOnCursor)
            {
                hasSkillOnCursor = false;
                isHolding = false;
                noHold = true;
                //return; //first click just ends cast mode
            }

            if (!Input.GetMouseButton(0))
            {
                isHolding = false;
                noHold = false;
            }


            if (isOverUi && Input.GetMouseButtonDown(0))
                noHold = true;

            if (!hasHitMap)
            {
                WalkProvider.DisableRenderer();
                return;
            }

            //we hit the map! Do map things

            var hasGroundPos = WalkProvider.GetMapPositionForWorldPosition(groundHit.point, out var mapPosition);
            var hasSrcPos = WalkProvider.GetMapPositionForWorldPosition(Target.transform.position, out var srcPosition);
            var okPath = true;

            if (hasGroundPos && !WalkProvider.IsCellWalkable(mapPosition))
            {
            }

            if (hasGroundPos && hasSrcPos && !IsInNPCInteraction)
            {
                if (mapPosition != lastTile)
                {
                    if (tempPath == null)
                        tempPath = new Vector2Int[SharedConfig.MaxPathLength + 2];

                    if (!WalkProvider.IsCellWalkable(mapPosition))
                        okPath = false;

                    if ((mapPosition - srcPosition).SquareDistance() > SharedConfig.MaxPathLength)
                        okPath = false;

                    if (okPath)
                    {
                        //Debug.Log("Performing path check");

                        var steps = Pathfinder.GetPath(WalkProvider.WalkData, mapPosition, srcPosition, tempPath);
                        if (steps == 0)
                            okPath = false;
                    }
                    else
                    {
                        //the target isn't valid, so we're going to try to see if we can find something that is in the same raycast target

                        var loopCount = 0; //infinite loop safety
                        ray.origin = groundHit.point + ray.direction * 0.01f;
                        while (Physics.Raycast(ray, out var rehit, MaxClickDistance, (1 << LayerMask.NameToLayer("WalkMap"))) && loopCount < 5)
                        {
                            var newGroundPos = WalkProvider.GetMapPositionForWorldPosition(rehit.point, out var newMapPosition);

                            if (newGroundPos && WalkProvider.IsCellWalkable(newMapPosition) &&
                                (newMapPosition - srcPosition).SquareDistance() <= SharedConfig.MaxPathLength)
                            {
                                var steps = Pathfinder.GetPath(WalkProvider.WalkData, newMapPosition, srcPosition, tempPath);
                                if (steps > 0)
                                {
                                    groundHit = rehit;
                                    mapPosition = newMapPosition;
                                    okPath = true;
                                    break;
                                }
                            }

                            ray.origin = rehit.point + ray.direction * 0.01f;
                            loopCount++;
                        }
                    }
                }
                else
                    okPath = lastPathValid;

                if (!isOverUi)
                    WalkProvider.UpdateCursorPosition(Target.transform.position, groundHit.point, okPath);
                else
                    WalkProvider.DisableRenderer();

                lastPathValid = okPath;
                lastTile = mapPosition;
            }

            if (!hasHitCharacter)
                TargetUi.text = "";


            if (noHold)
                return;

            if (IsInNPCInteraction)
                return;

            if (Input.GetMouseButton(0) && ClickDelay <= 0)
            {
                var srcPos = controllable.Position;
                var hasDest = WalkProvider.GetClosestTileTopToPoint(groundHit.point, out var destPos);

                if (hasDest)
                {
                    if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ||
                        controllable.SpriteAnimator.State == SpriteState.Sit)
                    {
                        if (Input.GetMouseButtonDown(0)) //only do this when mouse is down the first time. Yeah the second check is dumb...
                        {
                            ClickDelay = 0.1f;
                            //if(controllable.SpriteAnimator.State != SpriteState.Standby)
                                ChangeFacing(WalkProvider.GetWorldPositionForTile(destPos));
                        }
                    }
                    else
                    {
                        var dist = (srcPos - destPos).SquareDistance();
                        if (WalkProvider.IsCellWalkable(destPos) && dist < SharedConfig.MaxPathLength)
                        {
                            NetworkManager.Instance.MovePlayer(destPos);
                            var click = GameObject.Instantiate(ClickEffectPrefab);
                            click.transform.position = WalkProvider.GetWorldPositionForTile(destPos) + new Vector3(0f, 0.02f, 0f);
                            ClickDelay = 0.5f;
                            isHolding = true;
                        }
                        else
                        {
                            if (WalkProvider.GetNextWalkableTileForClick(srcPos, destPos, out var dest2))
                            {
                                NetworkManager.Instance.MovePlayer(dest2);
                                var click = GameObject.Instantiate(ClickEffectPrefab);
                                click.transform.position = WalkProvider.GetWorldPositionForTile(destPos) + new Vector3(0f, 0.02f, 0f);
                                ClickDelay = 0.5f;
                                isHolding = true;
                            }
                        }

                    }
                }
            }
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


                Debug.Log("Loaded effect " + asset.PrefabName);

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
                
                var val = (RenderSettings.fogEndDistance - Distance) / (RenderSettings.fogEndDistance - RenderSettings.fogStartDistance);
                Camera.backgroundColor = RenderSettings.fogColor * (1 - val);
            }
        }

        public static void SetErrorUiText(string text)
        {
            Dispatcher.RunOnMainThread(() =>
            {
                Instance.ErrorNoticeUi.gameObject.SetActive(true);
                Instance.ErrorNoticeUi.text = "<color=red>Error: </color>" + text;                
            });
        }

        public void Update()
        {
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
                cursorSkill = CharacterSkill.Bash;
                cursorSkillLvl = 5;
            }
            //
            // if (!inTextBox && Input.GetKeyDown(KeyCode.F3))
            //     FireArrow.Create(controllable.gameObject, 5);
            // if (!inTextBox && Input.GetKeyDown(KeyCode.F4))
            //     CastEffect.Create(3f, "ring_red", controllable.gameObject);
            //
            // if (!inTextBox && Input.GetKeyDown(KeyCode.L))
            //     ForestLightEffect.Create((ForestLightType)Random.Range(0, 4), controllable.transform.position);
            //
            // if (!inTextBox && Input.GetKeyDown(KeyCode.F2))
            //     CastLockOnEffect.Create(3f, controllable.gameObject);

            //if (Input.GetKeyDown(KeyCode.Alpha1))
            //    AttachEffectToEntity("RedPotion", controllable);

            //if (Input.GetKeyDown(KeyCode.Alpha2))
            //    AttachEffectToEntity("Death", controllable);

            //if (Input.GetKeyDown(KeyCode.Alpha3))
            //    AttachEffectToEntity("LevelUp", controllable);

            //if (Input.GetKeyDown(KeyCode.Alpha4))
            //    AttachEffectToEntity("Resurrect", controllable);

            //if (Input.GetKeyDown(KeyCode.Alpha5))
            //    AttachEffectToEntity("MVP", controllable);
            //
            // if(Input.GetKeyDown(KeyCode.Alpha6))
            //     CastingEffect.StartCasting(2f, "ring_blue", controllable.gameObject);

            // if (Input.GetKeyDown(KeyCode.Q))
            // {
            // //    //CastingEffect.StartCasting(3, "ring_red", controllable.gameObject);
            //     var temp = new GameObject("Warp");
            //     temp.transform.position = controllable.transform.position + new Vector3(0, 0.1f, 0f);
            //     MapWarpEffect.StartWarp(temp);
            // }

            //        if (Input.GetKeyDown(KeyCode.F4))
            //        {
            //NetworkManager.Instance.SkillAttack();
            //        }

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
                    if(Recorder.CenterPlayerOnMap)
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
                        TargetRotation = 0f;
                }

                LastRightClick = Time.timeSinceLevelLoad;
            }

            if (!inTextBox && !pointerOverUi && Input.GetMouseButton(1))
            {
                if (Input.GetMouseButton(1) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
                {
                    Height -= Input.GetAxis("Mouse Y") / 4;

                    Height = Mathf.Clamp(Height, 0f, 90f);
                }
                else
                {
                    var turnSpeed = 200;
                    TargetRotation += Input.GetAxis("Mouse X") * turnSpeed * Time.deltaTime;
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

            DoScreenCast(pointerOverUi);
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
                Distance += Input.GetAxis("Mouse ScrollWheel") * 20 * ctrlKey;

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