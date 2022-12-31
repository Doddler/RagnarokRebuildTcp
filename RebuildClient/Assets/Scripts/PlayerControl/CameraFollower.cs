using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Effects;
using Assets.Scripts.MapEditor;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using Assets.Scripts.Sprites;
using Assets.Scripts.UI;
using Assets.Scripts.Utility;
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
using UnityEngine.UI;
using static UnityEngine.GraphicsBuffer;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

namespace Assets.Scripts
{
    public class CameraFollower : MonoBehaviour
    {
        public GameObject ListenerProbe;

        public Texture2D NormalCursorTexture;
        public Texture2D AttackCursorTexture;
        public Texture2D TalkCursorTexture;
        public RoSpriteData TargetSprite;
        public GameObject LevelUpPrefab;
        public GameObject ResurrectPrefab;
        public GameObject DeathPrefab;
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
            var percent = exp / (float) maxExp;

            ExpDisplay.text = $"Exp: {exp}/{maxExp} ({percent * 100f:F1}%)";
            ExpSlider.value = percent;
        }
        

        private GameObject CreateSelectedCursorObject()
        {

            var go = new GameObject("Cursor");
            go.layer = LayerMask.NameToLayer("Characters");
            go.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
            go.AddComponent<Billboard>();

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

            if (cursorTexture == TalkCursorTexture)
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

            if (isHolding || isOverUi)
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
                        ChangeCursor(AttackCursorTexture);
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
                ChangeCursor(NormalCursorTexture);

            ClickDelay -= Time.deltaTime;

            if (Input.GetMouseButtonUp(0))
                ClickDelay = 0;

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
                            ChangeFacing(WalkProvider.GetWorldPositionForTile(destPos));
                        }
                    }
                    else
                    {
                        var dist = (srcPos - destPos).SquareDistance();
                        if (WalkProvider.IsCellWalkable(destPos) && dist < SharedConfig.MaxPathLength)
                        {
                            NetworkManager.Instance.MovePlayer(destPos);
                            ClickDelay = 0.5f;
                            isHolding = true;
                        }
                        else
                        {
                            if (WalkProvider.GetNextWalkableTileForClick(srcPos, destPos, out var dest2))
                            {
                                NetworkManager.Instance.MovePlayer(dest2);
                                ClickDelay = 0.5f;
                                isHolding = true;
                            }
                        }
                    }
                }
            }
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

            if (string.IsNullOrWhiteSpace(text))
                return;

            if (text.StartsWith("/"))
            {
                var s = text.Split(new [] {' '}, StringSplitOptions.RemoveEmptyEntries);

                if (s[0] == "/warp" && s.Length > 1)
                {
                    if (s.Length == 4)
                    {
                        if(int.TryParse(s[2], out var x) && int.TryParse(s[3], out var y))
                            NetworkManager.Instance.SendMoveRequest(s[1], x, y);
                        else
                            NetworkManager.Instance.SendMoveRequest(s[1]);
                    }
                    else
                        NetworkManager.Instance.SendMoveRequest(s[1]);
                }

                if (s[0] == "/where")
                {
                    var mapname = NetworkManager.Instance.CurrentMap;
                    var srcPos = WalkProvider.GetMapPositionForWorldPosition(Target.transform.position, out var srcPosition);

                    AppendChatText($"Location: {mapname} {srcPosition.x},{srcPosition.y}");
                }

                if (s[0] == "/name" || s[0] == "/changename")
                {
                    var newName = text.Substring(s[0].Length + 1);
                    NetworkManager.Instance.SendChangeName(newName);
                }

                if (s[0] == "/level")
                {
                    if(s.Length == 1 || !int.TryParse(s[1], out var level))
                        NetworkManager.Instance.SendAdminLevelUpRequest(0);
                    else
                        NetworkManager.Instance.SendAdminLevelUpRequest(level);
                }

                if (s[0] == "/bgm")
                    AudioManager.Instance.ToggleMute();

                if (s[0] == "/emote" && s.Length == 2)
                    Emote(int.Parse(s[1]));

                if (s[0] == "/change")
                {
                    if(s.Length == 1)
                        NetworkManager.Instance.SendChangeAppearance(0);

                    if (s.Length == 2)
                    {
                        if (s[1].ToLower() == "hair")
                            NetworkManager.Instance.SendChangeAppearance(1);
                        if (s[1].ToLower() == "gender")
                            NetworkManager.Instance.SendChangeAppearance(2, controllable.IsMale ? 1 : 0);
                        if (s[1].ToLower() == "job")
                            NetworkManager.Instance.SendChangeAppearance(3);
                        if (s[1].ToLower() == "weapon")
                            NetworkManager.Instance.SendChangeAppearance(4);

                    }

                    if (s.Length == 3)
                    {
                        if (int.TryParse(s[2], out var id))
                        {

                            if (s[1].ToLower() == "hair")
                                NetworkManager.Instance.SendChangeAppearance(1, id);
                            if (s[1].ToLower() == "gender")
                                NetworkManager.Instance.SendChangeAppearance(2, id);
                            if (s[1].ToLower() == "job")
                                NetworkManager.Instance.SendChangeAppearance(3, id);
                            if (s[1].ToLower() == "weapon")
                                NetworkManager.Instance.SendChangeAppearance(4, id);
                        }
                    }
                }

                if (s[0] == "/randomize" || s[0] == "/random")
                        NetworkManager.Instance.SendChangeAppearance(0);

                if (s[0] == "/effect" && s.Length > 1)
                {
                    if(int.TryParse(s[1], out var id))
                        AttachEffectToEntity(id, controllable);
                    else
                        AttachEffectToEntity(s[1], controllable);
                    
                }

                if (s[0] == "/reloadscript" || s[0] == "/scriptreload")
                {
                    NetworkManager.Instance.SendAdminAction(AdminAction.ReloadScripts);
                }


                if (s[0] == "/servergc")
                {
                    NetworkManager.Instance.SendAdminAction(AdminAction.ForceGC);
                }
            }
            else
            {
                if (text.Length > 255)
                {
                    AppendChatText("<color=yellow>Error</color>: Text too long.");
                }
                else
                    NetworkManager.Instance.SendSay(text);
                //AppendChatText(text);
                
            }


            lastMessage = text;
        }

        public void AttachEffectToEntity(string effect, ServerControllable target)
        {
            if (!EffectIdLookup.TryGetValue(effect, out var id))
            {
                AppendError($"Could not find effect with name {effect}.");
                return;
            }

            AttachEffectToEntity(id, target);
        }

        public void AttachEffectToEntity(int effect, ServerControllable target)
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
                    var obj2 = GameObject.Instantiate(prefab, target.gameObject.transform, false);
                    obj2.transform.localPosition = new Vector3(0, asset.Offset, 0);
                }
                else
                {
                    var obj2 = GameObject.Instantiate(prefab);
                    obj2.transform.localPosition = target.gameObject.transform.position + new Vector3(0, asset.Offset, 0);
                }

                return;
            }
            //Debug.Log($"Loading effect asset {asset.PrefabName}");
            var loader = Addressables.LoadAssetAsync<GameObject>(asset.PrefabName);
            loader.Completed += ah =>
            {
                if (target.gameObject != null && target.gameObject.activeInHierarchy)
                {
                    if (asset.Billboard)
                    {
                        var obj2 = GameObject.Instantiate(ah.Result, target.gameObject.transform, false);
                        obj2.transform.localPosition = new Vector3(0, asset.Offset, 0);
                    }
                    else
                    {
                        var obj2 = GameObject.Instantiate(ah.Result);
                        obj2.transform.localPosition = target.gameObject.transform.position + new Vector3(0, asset.Offset, 0);
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
                    obj2.AddComponent<Billboard>();

                if(facing != 0)
                    obj2.transform.localRotation = Quaternion.AngleAxis(45 * facing, Vector3.up);

                return;
            }
            
            var loader = Addressables.LoadAssetAsync<GameObject>(asset.PrefabName);
            loader.Completed += ah =>
            {
                var obj2 = GameObject.Instantiate(ah.Result, outputObj.transform, false);
                obj2.transform.localPosition = new Vector3(0, asset.Offset, 0);
                if (asset.Billboard)
                    obj2.AddComponent<Billboard>();


                Debug.Log("Loaded effect " + asset.PrefabName);

                EffectCache[asset.Id] = ah.Result;
            };
        }

        public void UpdateWaterTexture()
        {
            Camera.depthTextureMode = DepthTextureMode.Depth;

            if (WaterTexture == null)
            {
                WaterTexture = new RenderTexture(Screen.width, Screen.height, 8, RenderTextureFormat.Default);
                WaterDepthTexture = new RenderTexture(Screen.width, Screen.height, 8, RenderTextureFormat.Depth);

                WaterCamera.SetTargetBuffers(WaterTexture.colorBuffer, WaterDepthTexture.depthBuffer);

                Shader.SetGlobalTexture("_WaterDepth", WaterDepthTexture);
            }

            if (WaterTexture.width != Screen.width || WaterTexture.height != Screen.height)
            {
                WaterTexture.Release();
                WaterDepthTexture.Release();

                WaterTexture = new RenderTexture(Screen.width, Screen.height, 16, RenderTextureFormat.Default);
                WaterDepthTexture = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.Depth);

                WaterCamera.SetTargetBuffers(WaterTexture.colorBuffer, WaterDepthTexture.depthBuffer);

                Shader.SetGlobalTexture("_WaterDepth", WaterDepthTexture);
            }
        }

        public void OnPostRender()
        {
            //WaterCamera.RenderWithShader(WaterDepthShader, "WaterDepth");
            //WaterCamera.Render();
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
                    if(lastMessage != null)
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
                if(!WarpPanel.activeInHierarchy)
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

            //if(Input.GetKeyDown(KeyCode.Alpha6))
            //    CastingEffect.StartCasting(0.6f, "ring_yellow", controllable.gameObject);

            //if (Input.GetKeyDown(KeyCode.Q))
            //{
            //    //CastingEffect.StartCasting(3, "ring_red", controllable.gameObject);
            //    var temp = new GameObject("Warp");
            //    temp.transform.position = controllable.transform.position;
            //    MapWarpEffect.StartWarp(temp);
            //}

            //if (Input.GetKeyDown(KeyCode.Keypad1) || (Input.GetKeyDown(KeyCode.Alpha1) && Input.GetKey(KeyCode.LeftShift)))
            //    NetworkManager.Instance.SendMoveRequest("prontera");
            //if (Input.GetKeyDown(KeyCode.Keypad2) || (Input.GetKeyDown(KeyCode.Alpha2) && Input.GetKey(KeyCode.LeftShift)))
            //    NetworkManager.Instance.SendMoveRequest("geffen");
            //if (Input.GetKeyDown(KeyCode.Keypad3) || (Input.GetKeyDown(KeyCode.Alpha3) && Input.GetKey(KeyCode.LeftShift)))
            //    NetworkManager.Instance.SendMoveRequest("morocc");
            //if (Input.GetKeyDown(KeyCode.Keypad4) || (Input.GetKeyDown(KeyCode.Alpha4) && Input.GetKey(KeyCode.LeftShift)))
            //    NetworkManager.Instance.SendMoveRequest("payon");
            //if (Input.GetKeyDown(KeyCode.Keypad5) || (Input.GetKeyDown(KeyCode.Alpha5) && Input.GetKey(KeyCode.LeftShift)))
            //    NetworkManager.Instance.SendMoveRequest("alberta");
            //if (Input.GetKeyDown(KeyCode.Keypad6) || (Input.GetKeyDown(KeyCode.Alpha6) && Input.GetKey(KeyCode.LeftShift)))
            //    NetworkManager.Instance.SendMoveRequest("aldebaran");

            //        if (Input.GetKeyDown(KeyCode.F4))
            //        {
            //NetworkManager.Instance.SkillAttack();
            //        }

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


#if !DEBUG
            if (Height > 80)
	            Height = 80;
            if (Height < 35)
	            Height = 35;
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

            if(!pointerOverUi && screenRect.Contains(Input.mousePosition))
                Distance += Input.GetAxis("Mouse ScrollWheel") * 20 * ctrlKey;

#if !DEBUG
            if (Distance > 90)
	            Distance = 90;
            if (Distance < 30)
	            Distance = 30;
#endif
            
            var curTarget = Target.transform.position;
            if (OverrideTarget != null)
                curTarget = (curTarget + OverrideTarget.transform.position)/2f;

            TargetFollow = Vector3.Lerp(TargetFollow, curTarget, Time.deltaTime * 5f);
            CurLookAt = TargetFollow;

            var targetHeight = Mathf.Lerp(Target.transform.position.y, WalkProvider.GetHeightForPosition(Target.transform.position), Time.deltaTime * 20f);

            Target.transform.position = new Vector3(Target.transform.position.x, targetHeight, Target.transform.position.z);

            var pos = Quaternion.Euler(Height, Rotation, 0) * Vector3.back * Distance;

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
        }
    }
}
