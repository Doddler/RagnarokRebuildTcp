using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Assets.Scripts.Effects;
using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Network.PacketBase;
using Assets.Scripts.Objects;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.SkillHandlers;
using Assets.Scripts.Sprites;
using Assets.Scripts.UI;
using Assets.Scripts.UI.ConfigWindow;
using Assets.Scripts.UI.Hud;
using Assets.Scripts.UI.TitleScreen;
using Assets.Scripts.Utility;
using HybridWebSocket;
using JetBrains.Annotations;
using Lidgren.Network;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace Assets.Scripts.Network
{
    public class NetworkManager : MonoBehaviour
    {
        public static NetworkManager Instance;

        private WebSocket socket;

        public CameraFollower CameraFollower;
        public CharacterOverlayManager OverlayManager;
        public TitleScreen TitleScreen;
        public LoginBox LoginBox;
        public GameObject TargetNoticePrefab;
        public TextMeshProUGUI LoadingText;
        public Dictionary<int, ServerControllable> EntityList = new Dictionary<int, ServerControllable>();
        public Dictionary<int, GroundItem> GroundItemList = new Dictionary<int, GroundItem>();
        public int PlayerId;
        public NetQueue<ClientInboundMessage> InboundMessages = new NetQueue<ClientInboundMessage>(30);
        public NetQueue<ClientOutgoingMessage> OutboundMessages = new NetQueue<ClientOutgoingMessage>(30);

        public PlayerState PlayerState = new PlayerState();

        //private static NetClient client;

        private float lastPing;
        private bool isReady;
        private bool isConnected;

        // public Color FakeAmbient = Color.white;

        public string CurrentMap = "";

        private List<Vector2Int> pathData = new List<Vector2Int>(20);

        public Scene CurrentScene;

        private AsyncOperationHandle<RoSpriteData> spritePreload;
        private AsyncOperationHandle uiPreload;

        public Guid CharacterGuid;

        private WebSocketOpenEventHandler openEventHandler;
        private WebSocketMessageEventHandler messageEventHandler;

        //login stuff
        private bool isOnLoginScreen;
        private bool hasAttemptedConnection;
        private string webUrl;
        private string username;

        private string password;

        // private bool isNewUser;
        private bool isTokenLogin;
        private bool isInErrorState;
        private bool requestLoginToken;

        public static IResourceLocator ResourceLocator;
        public static bool IsLoaded;


#if DEBUG
        public static string SpawnMap = "";
#endif

        private void Start()
        {
            Debug.Log("Starting Network Manager");

            Instance = this;

            //NetPeerConfiguration config = new NetPeerConfiguration("RebuildZoneServer");

#if DEBUG
            UnloadOldScenes();

            //config.SimulatedMinimumLatency = 0.1f;
            //config.SimulatedLoss = 0.05f;
#endif

            LeanTween.init(4000);


            StartCoroutine(StartUp());
        }

        private IEnumerator StartUp()
        {
#if WINDOWS_RUNTIME
            var op = Addressables.LoadContentCatalogAsync(Path.Combine(Application.streamingAssetsPath, "aa/catalog.json"));
            yield return op;

#else
            var updateCheck = Addressables.CheckForCatalogUpdates(true);
            yield return updateCheck;

            if (updateCheck.IsValid() && updateCheck.Result != null && updateCheck.Result.Count > 0)
            {
                AsyncOperationHandle<List<IResourceLocator>> updateHandle = Addressables.UpdateCatalogs();
                yield return updateHandle;
            }
#endif
            //update addressables
            //
            // AsyncOperationHandle<IList<IResourceLocation>> handle
            //     = Addressables.LoadResourceLocationsAsync(new string[] {"Assets/Sprites/Characters/BodyMale/초보자_남.spr", "Assets/Sprites/Monsters/poring.spr"}, Addressables.MergeMode.Union);
            // yield return handle;
#if !UNITY_EDITOR && UNITY_WEBGL
            var info = Addressables.GetLocatorInfo("AddressablesMainContentCatalog");
            ResourceLocator = info.Locator;
#endif

            ClientDataLoader.Instance.Initialize();
            UiManager.Instance.Initialize();


            spritePreload = Addressables.LoadAssetAsync<RoSpriteData>("Assets/Sprites/Monsters/poring.spr");


            while (!spritePreload.IsDone)
            {
                LoadingText.text = $"Sprites {spritePreload.PercentComplete * 100:N0}%";
                yield return 0;
            }

            if (spritePreload.Status != AsyncOperationStatus.Succeeded)
                Debug.LogError("Could not load poring sprite");

            LoadingText.text = "";
            RoSpriteAnimator.FallbackSpriteData = spritePreload.Result;

            uiPreload = Addressables.LoadAssetAsync<GameObject>("Assets/Effects/Prefabs/RedPotion.prefab");

            while (!uiPreload.IsDone)
            {
                LoadingText.text = $"Effects {uiPreload.PercentComplete * 100:N0}%";
                yield return 0;
            }

            CameraFollower.PlayerState = PlayerState;
            ClientPacketHandler.Init(this, PlayerState);
            IsLoaded = true;

            isOnLoginScreen = true;
            TitleScreen.gameObject.SetActive(true);
            LoginBox.gameObject.SetActive(true);
            LoginBox.Init();
            CameraFollower.UpdateCameraSize();
            SceneTransitioner.Instance.FadeIn(); //show login window
            AudioManager.Instance.PlayBgm("01.mp3");
        }

        private IEnumerator BeginConnection(string username, string password)
        {
#if UNITY_EDITOR
            var target = "ws://127.0.0.1:5000/ws";
            //var target = "wss://roserver.dodsrv.com/ws";
            StartConnectServer(target, username, password);
            yield break; //end coroutine
#else
            if (Application.platform == RuntimePlatform.WindowsPlayer)
            {
                var path = File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "serverconfig.txt"));
                StartConnectServer(path, username, password);
                yield break;
            }

            //we will load our config 
            var www = new WWW("https://www.dodsrv.com/ragnarok/serverconfig.txt");
            yield return www;

            if (www.error != null)
            {
                Debug.LogError("Failed get server location! Error: " + www.error);
            }
            else
            {
                Debug.Log("Identified remote server for connection: " + www.text);

                while (!spritePreload.IsDone || !uiPreload.IsDone)
                    yield return new WaitForSeconds(0.1f);

                StartConnectServer(www.text, username, password);
            }
#endif
        }

        public void StartConnectWithNewAccount(string serverPath, string connectUserName, string connectPassword)
        {
            if (socket != null && socket.GetState() != WebSocketState.Closed)
                return;

            socket = WebSocketFactory.CreateInstance(serverPath);

            webUrl = serverPath;
            this.username = connectUserName;
            this.password = connectPassword;
            isConnected = false;
            isReady = false;
            isTokenLogin = false;
            isInErrorState = false;
            isOnLoginScreen = true;
            requestLoginToken = false;

            socket.OnOpen += OnOpenConnectionNewUser;
            socket.OnClose += OnCloseLoginScreenHandler;
            socket.OnError += OnErrorLoginScreenHandler;
            socket.OnMessage += LoginScreenMessageHandler;

            Debug.Log($"Connecting to server at target {serverPath}...");

            lastPing = Time.time;

            socket.Connect();
        }

        public void StartConnectWithRegularLogin(string serverPath, string connectUserName, string connectPassword, bool usePasswordToken,
            bool askForLoginToken)
        {
            if (socket != null && socket.GetState() != WebSocketState.Closed)
                return;

            socket = WebSocketFactory.CreateInstance(serverPath);

            webUrl = serverPath;
            this.username = connectUserName;
            this.password = connectPassword;
            requestLoginToken = askForLoginToken;
            isConnected = false;
            isReady = false;
            isTokenLogin = usePasswordToken;
            // isNewUser = false;
            isInErrorState = false;
            isOnLoginScreen = true;

            socket.OnOpen += OnOpenConnectionRegularLogin;
            socket.OnClose += OnCloseLoginScreenHandler;
            socket.OnError += OnErrorLoginScreenHandler;
            socket.OnMessage += LoginScreenMessageHandler;

            Debug.Log($"Connecting to server at target {serverPath}...");

            lastPing = Time.time;

            socket.Connect();
        }

        private void OnCloseLoginScreenHandler(WebSocketCloseCode code)
        {
            Debug.Log($"OnCloseLoginScreenHandler called: " + code);

            if (isInErrorState)
                return; //we've already handled the error
            Dispatcher.RunOnMainThread(() =>
            {
                if (isConnected || TitleScreen.TitleState != TitleScreen.TitleScreenState.LogIn)
                    TitleScreen.LogInError($"You have been disconnected from the server.");
                else
                    TitleScreen.LogInError($"Unable to connect to server at url: {webUrl}");
            });

            socket = null;
        }

        private void OnErrorLoginScreenHandler(string message)
        {
            Debug.Log($"OnErrorLoginScreenHandler called: " + message);

            if (isInErrorState)
                return;

            Dispatcher.RunOnMainThread(() =>
            {
                TitleScreen.LogInError($"An error has occured: {message}");
                socket.Close();
                socket = null;
            });
        }

        private void LoginScreenMessageHandler(byte[] bytes)
        {
            var msg = new ClientInboundMessage(bytes, bytes.Length);
            var type = (PacketType)msg.ReadByte();
            Debug.Log($"LoginScreenMessageHandler called: " + type);
            switch (type)
            {
                case PacketType.ConnectionApproved:
                    InboundMessages.Enqueue(new ClientInboundMessage(bytes, bytes.Length));
                    break;
                case PacketType.EnterServer:
                    socket.OnMessage -= LoginScreenMessageHandler;
                    socket.OnMessage += NormalOperationMessageHandler;
                    socket.OnError -= OnErrorLoginScreenHandler;
                    socket.OnError += NormalOperationErrorHandler;
                    socket.OnClose -= OnCloseLoginScreenHandler;
                    socket.OnClose += NormalOperationCloseHandler;
                    isConnected = true;
                    InboundMessages.Enqueue(new ClientInboundMessage(bytes, bytes.Length));
                    break;
                case PacketType.ConnectionDenied:
                    isInErrorState = true;
                    InboundMessages.Enqueue(new ClientInboundMessage(bytes, bytes.Length));
                    break;
                case PacketType.ErrorMessage:
                    Dispatcher.RunOnMainThread(() => TitleScreen.ErrorMessage(msg.ReadString()));
                    break;
                default:
                    Debug.LogWarning($"Unhandled packet type {type} in LoginScreenMessageHandler");
                    break;
            }
        }

        private void NormalOperationCloseHandler(WebSocketCloseCode code)
        {
            Debug.LogWarning("Socket connection closed: " + code);
            if (!isConnected)
                CameraFollower.SetErrorUiText($"Could not connect to server at {webUrl}.\n<size=-4>(Press space to try to reconnect)");
            else
                CameraFollower.SetErrorUiText("Connection has been closed.\n<size=-4>(Press space to try to reconnect)");
        }

        private void NormalOperationErrorHandler(string message)
        {
            Debug.LogError("Socket connection had an error: " + message);
            CameraFollower.SetErrorUiText("Socket connection generated an error.\n<size=-4>(Press space to try to reconnect)");
        }

        private void NormalOperationMessageHandler(byte[] bytes)
        {
            InboundMessages.Enqueue(new ClientInboundMessage(bytes, bytes.Length));
        }

        public void Disconnect()
        {
            if (socket == null || socket.GetState() != WebSocketState.Open)
                return;
            var outmsg = StartMessage();
            outmsg.Write((byte)PacketType.Disconnect);
            SendMessage(outmsg);
            socket.Close();
            socket.OnError -= NormalOperationErrorHandler;
            socket.OnClose -= NormalOperationCloseHandler;
            socket.OnClose -= OnCloseLoginScreenHandler;
            socket.OnError -= OnErrorLoginScreenHandler;
            isConnected = false;
            isReady = false;
            socket = null;
        }

        private void StartConnectServer(string serverPath, string username, string password)
        {
            socket = WebSocketFactory.CreateInstance(serverPath);

            var id = PlayerPrefs.GetString("characterid", "");
            var str = "Connect" + id.ToString();
            Debug.Log(str);

            socket.OnOpen += () =>
            {
                Debug.Log("Socket connection opened!");


                socket.Send(Encoding.UTF8.GetBytes(str));
            };

            socket.OnClose += e =>
            {
                Debug.LogWarning("Socket connection closed: " + e);
                if (!isConnected)
                    CameraFollower.SetErrorUiText($"Could not connect to server at {serverPath}.\n<size=-4>(Press space to try to reconnect)");
                else
                    CameraFollower.SetErrorUiText("Connection has been closed.\n<size=-4>(Press space to try to reconnect)");
            };


            socket.OnError += e =>
            {
                Debug.LogError("Socket connection had an error: " + e);
                CameraFollower.SetErrorUiText("Socket connection generated an error.\n<size=-4>(Press space to try to reconnect)");
            };

            socket.OnMessage += bytes =>
            {
                //Debug.Log("Received message!");
                InboundMessages.Enqueue(new ClientInboundMessage(bytes, bytes.Length));
            };

            //client.Connect(s[0], int.Parse(s[1]), outMsg);

            Debug.Log($"Connecting to server at target {serverPath}...");

            lastPing = Time.time;

            socket.Connect();
        }

        private void OnOpenConnectionRegularLogin()
        {
            Debug.Log("Socket connection opened!");

            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            bw.Write(false);
            bw.Write(isTokenLogin);
            bw.Write(requestLoginToken);
            bw.Write(username);
            if (!isTokenLogin)
                bw.Write(password);
            else
            {
                var tokenData = System.Convert.FromBase64String(password);
                bw.Write(""); //password
                bw.Write(tokenData.Length);
                bw.Write(tokenData);
            }

            socket.Send(ms.ToArray());
        }


        private void OnOpenConnectionNewUser()
        {
            Debug.Log("Socket connection opened!");

            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            bw.Write(true); //isNewAccount
            bw.Write(false); //isTokenLogin
            bw.Write(false); //requestLoginToken
            bw.Write(username);
            bw.Write(password);

            socket.Send(ms.ToArray());
        }


        public ClientOutgoingMessage StartMessage()
        {
            //we should use a pool for these
            return new ClientOutgoingMessage();
        }

        public void SendMessage(ClientOutgoingMessage message)
        {
            if (socket.GetState() != WebSocketState.Open)
            {
                Debug.Log("Could not send message, socket not open!");
                return;
            }

            //this sucks. Should modify it to work with spans...
            var buffer = new byte[message.Length];
            Buffer.BlockCopy(message.Message, 0, buffer, 0, message.Length);


            socket.Send(buffer);
            //Debug.Log("Sending message type: " + (PacketType)buffer[0]);
        }

        private void UnloadOldScenes()
        {
            var sceneCount = SceneManager.sceneCount;
            for (var i = 0; i < sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.name != "MainScene")
                    SceneManager.UnloadSceneAsync(scene);
            }

            CameraFollower.Target = null;

            EntityList.Clear();
        }

        public void ClearGroundItemList()
        {
            if (GroundItemList == null)
                return;

            foreach (var item in GroundItemList)
                GameObject.Destroy(item.Value.gameObject);

            GroundItemList.Clear();
        }

        private Vector2Int ReadPosition(ClientInboundMessage msg)
        {
            var x = msg.ReadInt16();
            var y = msg.ReadInt16();
            return new Vector2Int(x, y);
        }

        public void LoadMoveData2(ClientInboundMessage msg, ServerControllable ctrl)
        {
            var startPos = msg.ReadPosition();
            var realPos = new Vector2(msg.ReadFloat(), msg.ReadFloat());
            var moveSpeed = msg.ReadFloat();
            var moveDistance = msg.ReadFloat();
            var totalSteps = (int)msg.ReadByte();
            var curStep = 0;

            pathData.Clear();
            if (totalSteps > 0) //should always be true but whatever
            {
                pathData.Add(new Vector2Int(Mathf.FloorToInt(startPos.x), Mathf.FloorToInt(startPos.y)));
                var i = 1;
                while (i < totalSteps)
                {
                    var b = msg.ReadByte();
                    pathData.Add(pathData[i - 1].AddDirection((Direction)(b >> 4)));
                    i++;
                    if (i < totalSteps)
                    {
                        pathData.Add(pathData[i - 1].AddDirection((Direction)(b & 0xF)));
                        i++;
                    }
                }
            }

            var isInMoveDelay = msg.ReadBoolean();
            if (!isInMoveDelay) //we need to remove this and handle this properly
            {
                var height = 0f; 
                if(CameraFollower.WalkProvider != null)
                    CameraFollower.WalkProvider.GetHeightForPosition(new Vector3(realPos.x, 0, realPos.y)); //happens on initial load...
                var newRealPos = new Vector3(realPos.x, height, realPos.y);
                ctrl.RealPosition = newRealPos;
                ctrl.StartMove2(moveSpeed, moveDistance, totalSteps, curStep, startPos, pathData);
            }

            //ctrl.SetHitDelay(lockTime);
        }

        private ServerControllable SpawnEntity(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();
            var type = (CharacterType)msg.ReadByte();
            var classId = msg.ReadInt16();
            var pos = ReadPosition(msg);
            var facing = (Direction)msg.ReadByte();
            var state = (CharacterState)msg.ReadByte();

            var lvl = -1;
            var maxHp = 0;
            var hp = 0;

            if (type == CharacterType.Player || type == CharacterType.Monster)
            {
                lvl = (int)msg.ReadByte();
                maxHp = (int)msg.ReadInt32();
                hp = (int)msg.ReadInt32();
            }

            if (EntityList.TryGetValue(id, out var oldEntity))
            {
                //if for some reason we try to spawn an entity that already exists, we kill the old one.
                oldEntity.FadeOutAndVanish(0.1f);
                EntityList.Remove(id);
            }

            ServerControllable controllable;
            if (type == CharacterType.Player)
            {
                var headFacing = (HeadFacing)msg.ReadByte();
                var headId = msg.ReadByte();
                var weapon = msg.ReadByte();
                var isMale = msg.ReadBoolean();
                var name = msg.ReadString();
                var isMain = PlayerId == id;
                if (isMain)
                    PlayerState.EntityId = id;

                Debug.Log("Name: " + name);

                var playerData = new PlayerSpawnParameters()
                {
                    ServerId = id,
                    ClassId = classId,
                    Facing = facing,
                    Position = pos,
                    State = state,
                    HeadFacing = headFacing,
                    HeadId = headId,
                    IsMale = isMale,
                    Name = name,
                    Level = lvl,
                    MaxHp = maxHp,
                    Hp = hp,
                    WeaponClass = weapon,
                    IsMainCharacter = isMain,
                };

                controllable = ClientDataLoader.Instance.InstantiatePlayer(ref playerData);


                if (id == PlayerId)
                {
                    PlayerState.Level = lvl;

                    var max = CameraFollower.Instance.ExpForLevel(controllable.Level);
                    CameraFollower.UpdatePlayerExp(PlayerState.Exp, max);
                    controllable.IsHidden = PlayerState.IsAdminHidden;
                    PlayerState.JobId = classId;
                    UiManager.Instance.SkillManager.UpdateAvailableSkills();
                }
            }
            else
            {
                var interactable = false;
                var name = string.Empty;

                // if (type == CharacterType.Monster)
                // {
                //     lvl = (int)msg.ReadByte();
                //     maxHp = (int)msg.ReadUInt16();
                //     hp = (int)msg.ReadUInt16();
                // }

                if (type == CharacterType.NPC)
                {
                    name = msg.ReadString();
                    interactable = msg.ReadBoolean();
                    //Debug.Log(name);
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
                    Interactable = interactable
                };
                controllable = ClientDataLoader.Instance.InstantiateMonster(ref monData, type);
            }

            controllable.EnsureFloatingDisplayCreated().SetUp(controllable, name, maxHp, hp, type == CharacterType.Player, controllable.IsMainCharacter);
            if (controllable.IsMainCharacter)
            {
                CameraFollower.UpdatePlayerHP(hp, maxHp);
                //CameraFollower.UpdatePlayerSP(100, 100);
            }
            else
                controllable.Hp = hp;

            Debug.Log($"Create Entity {name} hp:{hp} maxhp:{maxHp}");

            controllable.SetHp(hp);
            if (type != CharacterType.NPC)
                controllable.IsInteractable = true;

            EntityList.Add(id, controllable);

            if (controllable.SpriteMode == ClientSpriteType.Prefab)
                return controllable;

            if (state == CharacterState.Moving)
                LoadMoveData2(msg, controllable);
            if (state == CharacterState.Sitting)
            {
                controllable.SpriteAnimator.ChangeMotion(SpriteMotion.Sit);
                controllable.SpriteAnimator.State = SpriteState.Sit;
            }

            if (PlayerId == controllable.Id)
            {
                CameraFollower.Target = controllable.gameObject;
                //Debug.Log($"Player entity sent, we're at position {pos}");

                if (!CameraFollower.Instance.CinemachineMode)
                    SceneTransitioner.Instance.FadeIn();
                CameraFollower.Instance.SnapLookAt();
            }

// #if UNITY_EDITOR
//             switch (type)
//             {
//                 case CharacterType.Player:
//                     GroundHighlighter.Create(controllable, "blue");
//                     break;
//                 case CharacterType.Monster:
//                     GroundHighlighter.Create(controllable, "red");
//                     break;
//                 case CharacterType.NPC:
//                     GroundHighlighter.Create(controllable, "orange");
//                     break;
//             }
// #endif

            return controllable;
        }


        private void OnMessageChangeSitStand(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();
            var isSitting = msg.ReadBoolean();

            if (!EntityList.TryGetValue(id, out var controllable))
            {
                Debug.LogWarning("Trying to move entity " + id + ", but it does not exist in scene!");
                return;
            }

            if (isSitting)
            {
                controllable.SpriteAnimator.ChangeMotion(SpriteMotion.Sit);
                controllable.SpriteAnimator.State = SpriteState.Sit;
                return;
            }

            if (controllable.SpriteAnimator.State == SpriteState.Sit)
            {
                controllable.SpriteAnimator.ChangeMotion(SpriteMotion.Idle);
                controllable.SpriteAnimator.State = SpriteState.Idle;
            }
        }

        private void OnMessageChangeFacing(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();
            var lookAt = msg.ReadPosition();
            var facing = (Direction)msg.ReadByte();

            if (!EntityList.TryGetValue(id, out var controllable))
            {
                Debug.LogWarning("Trying to move entity " + id + ", but it does not exist in scene!");
                return;
            }

            controllable.LookAt(lookAt.ToWorldPosition());
            if (controllable.SpriteAnimator.Type == SpriteType.Player)
                controllable.SpriteAnimator.SetHeadFacing((HeadFacing)msg.ReadByte());
        }

        private void OnMessageCreateEntity(ClientInboundMessage msg)
        {
            SpawnEntity(msg);
        }

        private void OnMessageMove(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();

            if (!EntityList.TryGetValue(id, out var controllable))
            {
                Debug.LogWarning("Trying to move entity " + id + ", but it does not exist in scene!");
                return;
            }

            controllable.MovePosition(ReadPosition(msg));
        }

        private void OnMessageStartMove(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();

            if (!EntityList.TryGetValue(id, out var controllable))
            {
                Debug.LogWarning("Trying to move entity " + id + ", but it does not exist in scene!");
                return;
            }

            LoadMoveData2(msg, controllable);
        }

        private void OnMessageStopImmediate(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();

            if (!EntityList.TryGetValue(id, out var controllable))
            {
                Debug.LogWarning("Trying to stop entity " + id + ", but it does not exist in scene!");
                return;
            }

            var pos = ReadPosition(msg);
            // Debug.Log($"Stoppping {controllable}");

            controllable.StopImmediate(pos, false);
        }

        private void OnMessageStopPlayer(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();

            if (!EntityList.TryGetValue(id, out var controllable))
            {
                Debug.LogWarning("Trying to stop entity " + id + ", but it does not exist in scene!");
                return;
            }

            if (id == PlayerId)
                CameraFollower.ClearSelected();

            controllable.StopWalking();
        }

        public void AttackMotion(ServerControllable src, Vector2Int pos, Direction dir, float motionSpeed, [CanBeNull] ServerControllable target)
        {
            var hasTarget = target != null;

            if (hasTarget)
            {
                var cd = src.transform.localPosition - target.transform.localPosition;
                cd.y = 0;
                target.CounterHitDir = cd.normalized;
                //Debug.Log("Counter hit: " + cd);
            }
            else
            {
                var v = dir.GetVectorValue();
                src.CounterHitDir = new Vector3(v.x, 0, v.y);
            }

            //src.SpriteAnimator.AnimSpeed = motionSpeed; //this will get reset when we resume walking anyways
            //src.AttackAnimationSpeed = motionSpeed;
            src.SetAttackAnimationSpeed(motionSpeed);
            //
            // if (src.SpriteAnimator.State == SpriteState.Walking)
            // {
            //     src.PauseMove(motionTime);
            //     src.SpriteAnimator.State = SpriteState.Standby;
            //     src.SpriteAnimator.Direction = dir;
            // }
            // else
            {
                src.StopImmediate(pos, false);
                //src.SpriteAnimator.Direction = dir;
                src.SpriteAnimator.State = SpriteState.Standby;
            }
        }

        private IEnumerator DamageEvent(int damage, float delay, int hitCount, int weaponClass, ServerControllable target)
        {
            yield return new WaitForSeconds(delay);
            if (target != null && target.SpriteAnimator.IsInitialized)
            {
                // if (hitCount > 1)
                //     target.SlowMove(0.5f, hitCount * 0.2f);

                if (damage < 0)
                {
                    AttachHealIndicator(-damage, target);
                    yield break;
                }

                for (var i = 0; i < hitCount; i++)
                {
                    //var go = GameObject.Instantiate(DamagePrefab, target.transform.localPosition, Quaternion.identity);
                    //var di = go.GetComponent<DamageIndicator>();
                    if (!target)
                        break;

                    if (target.SpriteAnimator.CurrentMotion != SpriteMotion.Dead)
                    {
                        if (target.SpriteAnimator.Type == SpriteType.Player)
                            target.SpriteAnimator.State = SpriteState.Standby;

                        //controllable.SnapToTile(controllable.Position, 0.2f);

                        if (!target.SpriteAnimator.IsAttackMotion)
                        {
                            target.SpriteAnimator.AnimSpeed = 1f;
                            target.SpriteAnimator.ChangeMotion(SpriteMotion.Hit, true);
                        }
                    }

                    if (weaponClass >= 0)
                    {
                        var hitSound = ClientDataLoader.Instance.GetHitSoundForWeapon(weaponClass);
                        AudioManager.Instance.OneShotSoundEffect(target.Id, hitSound, target.transform.position, 1f);
                    }

                    AttachDamageIndicator(damage, damage * (i + 1), target);
                    // target.SlowMove(0.7f, 0.3f);
                    //target.PosLockTime = hitLockTime;
                    // if (target.IsMoving)
                    //     target.UpdateMove(true); //this will snap them to the position in their path
                    yield return new WaitForSeconds(0.2f);
                }
            }
        }

        private void AttachHealIndicator(int damage, ServerControllable target)
        {
            var di = RagnarokEffectPool.GetDamageIndicator();
            var height = 1f;
            di.DoDamage(TextIndicatorType.Heal, damage.ToString(), new Vector3(0f, 0.6f, 0f), height,
                target.SpriteAnimator.Direction, "green", false);
            di.AttachDamageIndicator(target);
        }


        private void AttachDamageIndicator(int damage, int totalDamage, ServerControllable target)
        {
            var di = RagnarokEffectPool.GetDamageIndicator();
            var red = target.SpriteAnimator.Type == SpriteType.Player;
            var height = 1f;
            di.DoDamage(TextIndicatorType.Damage, damage.ToString(), target.gameObject.transform.localPosition, height,
                target.SpriteAnimator.Direction, red ? "red" : null, false);

            if (damage != totalDamage && target.CharacterType != CharacterType.Player)
            {
                var di2 = RagnarokEffectPool.GetDamageIndicator();
                di2.DoDamage(TextIndicatorType.ComboDamage, $"{totalDamage}", Vector3.zero, height,
                    target.SpriteAnimator.Direction, "#FFFF00", false);
                di2.AttachComboIndicatorToControllable(target);
            }
        }

        private void OnMessageHit(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();
            //var delay = msg.ReadFloat();
            var damage = msg.ReadInt32();
            var pos = ReadPosition(msg);
            var shouldStop = msg.ReadBoolean();

            if (!EntityList.TryGetValue(id, out var controllable))
            {
                //Debug.LogWarning("Trying to do hit entity " + id1 + ", but it does not exist in scene!");
                return;
            }

            var newHp = controllable.Hp - damage;

            if (controllable.CharacterType != CharacterType.NPC && controllable.MaxHp > 0)
            {
                if (controllable.IsMainCharacter)
                    CameraFollower.UpdatePlayerHP(newHp, controllable.MaxHp);
                controllable.SetHp(newHp);
            }

            controllable.Hp = newHp;
            if (controllable.Hp < 0)
                controllable.Hp = 0;


            if (shouldStop)
            {
                controllable.StopImmediate(pos, false);
                if (controllable.CharacterType != CharacterType.Player || !controllable.SpriteAnimator.IsAttackMotion)
                    controllable.SpriteAnimator.ChangeMotion(SpriteMotion.Hit);
            }
        }

        public void OnMessageGainExp(ClientInboundMessage msg)
        {
            var total = msg.ReadInt32();
            var exp = msg.ReadInt32();

            //Debug.Log("Gain Exp:" + exp + " " + total);

            PlayerState.Exp = total;
            CameraFollower.UpdatePlayerExp(PlayerState.Exp, CameraFollower.Instance.ExpForLevel(PlayerState.Level));

            if (exp == 0)
                return;

            if (!EntityList.TryGetValue(PlayerId, out var controllable))
                return;


            //var go = GameObject.Instantiate(HealPrefab, controllable.transform.localPosition, Quaternion.identity);
            var di = RagnarokEffectPool.GetDamageIndicator();
            var height = 72f / 50f;

            if (GameConfig.Data.ShowExpGainOnKill)
            {
                if (controllable.SpriteAnimator != null)
                    height = controllable.SpriteAnimator.SpriteData.Size / 50f;

                di.DoDamage(TextIndicatorType.Experience, $"+{exp} Exp", controllable.gameObject.transform.localPosition,
                    height, Direction.None, "yellow", false);
            }

            PlayerState.Exp += exp;
            var max = CameraFollower.Instance.ExpForLevel(controllable.Level);
            CameraFollower.Instance.UpdatePlayerExp(PlayerState.Exp, max);
        }

        public void OnMessageLevelUp(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();
            var lvl = msg.ReadByte();
            var curExp = msg.ReadInt32();

            if (!EntityList.TryGetValue(id, out var controllable))
            {
                //Debug.LogWarning("Trying to do hit entity " + id1 + ", but it does not exist in scene!");
                return;
            }

            CameraFollower.AttachEffectToEntity("LevelUp", controllable.gameObject, id);
            //
            // var go = GameObject.Instantiate(ClientConstants.Instance.LevelUpPrefab);
            // go.transform.SetParent(controllable.transform, true);
            // go.transform.localPosition = Vector3.zero;
            // go.transform.localRotation = Quaternion.identity;

            var oldLvl = controllable.Level;
            controllable.Level = lvl;
            if (controllable.IsMainCharacter)
            {
                if (PlayerState.JobId == 0 && oldLvl < 10 && lvl >= 10)
                    CameraFollower.AppendChatText($"<color=#99CCFF><i>Congratulations, you've reached level 10! You are now eligible to change jobs. "
                                                  + "Speak to the bard south of Prontera to get started.</i></color>");

                var req = CameraFollower.Instance.ExpForLevel(lvl);
                PlayerState.Exp = curExp;
                PlayerState.Level = lvl;
                CameraFollower.Instance.UpdatePlayerExp(PlayerState.Exp, req);
                CameraFollower.Instance.CharacterName.text = $"Lv. {controllable.Level} {controllable.Name}";
            }
        }

        public void OnMessageDeath(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();
            var pos = ReadPosition(msg);

            if (!EntityList.TryGetValue(id, out var controllable))
            {
                Debug.LogWarning("Trying to do death on entity " + id + ", but it does not exist in scene!");
                return;
            }

            Debug.Log($"{controllable.Name} is dead!");

            if (id == PlayerId)
                CameraFollower.AppendChatText("You have died! Press R key to respawn, or press shift + R to resurrect in place.");

            if (CameraFollower.SelectedTarget == controllable)
                CameraFollower.ClearSelected();

            controllable.StopImmediate(pos, false);
            controllable.SpriteAnimator.State = SpriteState.Dead;
            controllable.SpriteAnimator.ChangeMotion(SpriteMotion.Dead, true);

            if (id == PlayerId)
            {
                CameraFollower.AttachEffectToEntity("Death", controllable.gameObject, id);
            }
        }

        public void OnMessageHpRecovery(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();
            var amnt = msg.ReadInt32();
            var hp = msg.ReadInt32();
            var maxHp = msg.ReadInt32();
            var type = (HealType)msg.ReadByte();

            if (!EntityList.TryGetValue(id, out var controllable))
            {
                //Debug.LogWarning("Trying to do hit entity " + id1 + ", but it does not exist in scene!");
                return;
            }

            controllable.Hp = hp;
            controllable.MaxHp = maxHp;

            if (controllable.IsMainCharacter)
                CameraFollower.UpdatePlayerHP(hp, maxHp);
            controllable.SetHp(controllable.Hp, controllable.MaxHp);
        }

        public void OnMessageChangeName(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();
            var text = msg.ReadString();

            if (!EntityList.TryGetValue(id, out var controllable))
                return;

            CameraFollower.AppendChatText($"{controllable.Name} has changed their name to {text}.");
            controllable.Name = text;

            if (controllable.IsMainCharacter)
                CameraFollower.Instance.CharacterName.text = $"Lv. {controllable.Level} {controllable.Name}";
        }


        public void OnMessageNpcInteraction(ClientInboundMessage msg)
        {
            var type = (NpcInteractionType)msg.ReadByte();

            //Debug.Log($"Received NPC interaction of type: {type}");

            switch (type)
            {
                case NpcInteractionType.NpcDialog:
                {
                    var name = msg.ReadString();
                    var text = msg.ReadString();

                    //CameraFollower.AppendChatText(name + ": " + text);
                    CameraFollower.IsInNPCInteraction = true;
                    CameraFollower.DialogPanel.GetComponent<DialogWindow>().SetDialog(name, text);
                    break;
                }
                case NpcInteractionType.NpcFocusNpc:
                {
                    var id = msg.ReadInt32();
                    if (!EntityList.TryGetValue(id, out var controllable))
                        return;
                    var isFocus = msg.ReadBoolean();

                    CameraFollower.OverrideTarget = isFocus ? controllable.gameObject : null;
                    break;
                }
                case NpcInteractionType.NpcShowSprite:
                {
                    var sprite = msg.ReadString();
                    Debug.Log($"Show npc sprite {sprite}");
                    CameraFollower.DialogPanel.GetComponent<DialogWindow>().ShowImage(sprite);
                    break;
                }
                case NpcInteractionType.NpcOption:
                {
                    var options = new List<string>();
                    var len = msg.ReadInt32();
                    for (var i = 0; i < len; i++)
                        options.Add(msg.ReadString());

                    CameraFollower.NpcOptionPanel.GetComponent<NpcOptionWindow>().ShowOptionWindow(options);
                    break;
                }
                case NpcInteractionType.NpcEndInteraction:
                    CameraFollower.OverrideTarget = null;
                    CameraFollower.IsInNPCInteraction = false;
                    CameraFollower.DialogPanel.GetComponent<DialogWindow>().HideUI();
                    break;
                default:
                    Debug.LogError($"Unknown Npc Interaction type: {type}");
                    break;
            }
        }

        public void OnMessageStartCasting(ClientInboundMessage msg)
        {
            var srcId = msg.ReadInt32();
            var targetId = msg.ReadInt32();
            var skill = (CharacterSkill)msg.ReadByte();
            var lvl = (int)msg.ReadByte();
            var dir = (Direction)msg.ReadByte();
            var casterPos = new Vector2Int(msg.ReadInt16(), msg.ReadInt16());
            var castTime = msg.ReadFloat();

            EntityList.TryGetValue(targetId, out var target);

            if (EntityList.TryGetValue(srcId, out var controllable))
            {
                if (controllable.SpriteAnimator.State == SpriteState.Walking)
                    controllable.StopImmediate(casterPos, false);

                if (target != null)
                    controllable.LookAt(target.transform.position);
                else
                    controllable.SpriteAnimator.ChangeAngle(RoAnimationHelper.FacingDirectionToRotation(dir));

                if (controllable.SpriteAnimator.State != SpriteState.Dead && controllable.SpriteAnimator.State != SpriteState.Walking)
                {
                    controllable.SpriteAnimator.State = SpriteState.Standby;
                    controllable.SpriteAnimator.ChangeMotion(SpriteMotion.Casting);
                    controllable.SpriteAnimator.PauseAnimation();
                }

                ClientSkillHandler.StartCastingSkill(controllable, target, skill, lvl, castTime);
                controllable.StartCastBar(skill, castTime);
                //
                // if (skill == CharacterSkill.FireBolt)
                //     CastEffect.Create(castTime, "ring_red", controllable.gameObject);
                // if (skill == CharacterSkill.ColdBolt)
                //     CastEffect.Create(castTime, "ring_blue", controllable.gameObject);
            }
        }

        public void OnMessageStartAreaCasting(ClientInboundMessage msg)
        {
            var srcId = msg.ReadInt32();
            var target = new Vector2Int(msg.ReadInt16(), msg.ReadInt16());
            var skill = (CharacterSkill)msg.ReadByte();
            var lvl = (int)msg.ReadByte();
            var size = (int)msg.ReadByte();
            var dir = (Direction)msg.ReadByte();
            var casterPos = new Vector2Int(msg.ReadInt16(), msg.ReadInt16());
            var castTime = msg.ReadFloat();
            var hideName = msg.ReadBoolean();

            if (EntityList.TryGetValue(srcId, out var controllable))
            {
                if (controllable.SpriteAnimator.State == SpriteState.Walking)
                    controllable.StopImmediate(casterPos, false);
                controllable.LookAt(target.ToWorldPosition());
                if (controllable.SpriteAnimator.State != SpriteState.Dead && controllable.SpriteAnimator.State != SpriteState.Walking)
                {
                    controllable.SpriteAnimator.State = SpriteState.Standby;
                    controllable.SpriteAnimator.ChangeMotion(SpriteMotion.Casting);
                    controllable.SpriteAnimator.PauseAnimation();
                }

                controllable.HideCastName = hideName;
                ClientSkillHandler.StartCastingSkill(controllable, target, skill, lvl, castTime);
                controllable.StartCastBar(skill, castTime);
            }
        }

        public void OnMessageCreateCastCircle(ClientInboundMessage msg)
        {
            var target = new Vector2Int(msg.ReadInt16(), msg.ReadInt16());
            var size = (int)msg.ReadByte();
            var castTime = msg.ReadFloat();
            var isAlly = msg.ReadBoolean();
            var hasSound = msg.ReadBoolean();

            var targetCell = CameraFollower.Instance.WalkProvider.GetWorldPositionForTile(target);
            var color = new Color(1f, 1f, 1f, 0.5f);
            //CastTargetCircle.Create(isAlly? "magic_target" : "magic_target_bad", targetCell, color, size, castTime);
            CastTargetCircle.Create(isAlly, targetCell, size, castTime);
            if (hasSound)
                AudioManager.Instance.OneShotSoundEffect(-1, $"ef_beginspell.ogg", targetCell);
        }

        public void OnMessagePlaySound(ClientInboundMessage msg)
        {
        }

        void HandleDataPacket(ClientInboundMessage msg)
        {
            var type = (PacketType)msg.ReadByte();

            if (ClientPacketHandler.HasValidHandler(type))
            {
                // Debug.Log($"Wow! Using new packet handler for packet type {type}");
                ClientPacketHandler.Execute(type, msg);
                return;
            }

            //Debug.Log("Received packet type: " + type);

            switch (type)
            {
                case PacketType.ConnectionApproved:
                    isReady = true;
                    AudioManager.Instance.FadeOutCurrentBgm(); //end title screen bgm
                    break;
                case PacketType.ConnectionDenied:
                    TitleScreen.LogInError(msg.ReadString());
                    break;
                case PacketType.StartWalk:
                    OnMessageStartMove(msg);
                    break;
                case PacketType.SitStand:
                    OnMessageChangeSitStand(msg);
                    break;
                case PacketType.StopAction:
                    OnMessageStopPlayer(msg);
                    break;
                case PacketType.StopImmediate:
                    OnMessageStopImmediate(msg);
                    break;
                case PacketType.Move:
                    OnMessageMove(msg);
                    break;
                case PacketType.HitTarget:
                    OnMessageHit(msg);
                    break;
                case PacketType.GainExp:
                    OnMessageGainExp(msg);
                    break;
                case PacketType.LevelUp:
                    OnMessageLevelUp(msg);
                    break;
                case PacketType.Death:
                    OnMessageDeath(msg);
                    break;
                case PacketType.HpRecovery:
                    OnMessageHpRecovery(msg);
                    break;
                case PacketType.ChangeName:
                    OnMessageChangeName(msg);
                    break;
                case PacketType.NpcInteraction:
                    OnMessageNpcInteraction(msg);
                    break;
                case PacketType.StartCast:
                    OnMessageStartCasting(msg);
                    break;
                case PacketType.StartAreaCast:
                    OnMessageStartAreaCasting(msg);
                    break;
                case PacketType.CreateCastCircle:
                    OnMessageCreateCastCircle(msg);
                    break;
                default:
                    InvalidPacket(msg, type); //Debug.LogWarning($"Failed to handle packet type: {type}"));
                    break;
            }
        }

        private void InvalidPacket(ClientInboundMessage msg, PacketType type)
        {
            var sb = new StringBuilder();
            var len = msg.RemainingLength;
            sb.Append($"Failed to handle packet type: {type} (data length {len})");
            for (var i = 0; i < len; i++)
            {
                if (i > 0)
                    sb.Append(" ");
                if (i % 32 == 0)
                    sb.Append("\n");
                sb.Append(msg.ReadByte().ToString("X2"));
            }

            Debug.LogWarning(sb.ToString());
        }

        private void DoPacketHandling()
        {
            while (InboundMessages.TryDequeue(out var msg))
            {
                HandleDataPacket(msg);
            }
        }

        public void OnMapLoad()
        {
            var msg = StartMessage();

            msg.Write((byte)PacketType.PlayerReady);

            CurrentScene = SceneManager.GetSceneByName(CurrentMap);
            SceneManager.SetActiveScene(CurrentScene);

            SendMessage(msg);
        }

        public void ChangePlayerSitStand(bool isChangingToSitting)
        {
            var msg = StartMessage();

            msg.Write((byte)PacketType.SitStand);
            msg.Write(isChangingToSitting);

            SendMessage(msg);
        }

        public void ChangePlayerFacing(Direction direction, HeadFacing headFacing)
        {
            var msg = StartMessage();

            msg.Write((byte)PacketType.LookTowards);
            msg.Write((byte)direction);
            msg.Write((byte)headFacing);

            SendMessage(msg);
        }

        public void RandomTeleport()
        {
            var msg = StartMessage();

            msg.Write((byte)PacketType.RandomTeleport);

            SendMessage(msg);
        }

        public void MovePlayer(Vector2Int position)
        {
            var msg = StartMessage();
            //Debug.Log(position);
            msg.Write((byte)PacketType.StartWalk);
            msg.Write((short)position.x);
            msg.Write((short)position.y);

            SendMessage(msg);
        }

        public void SkillAttack()
        {
            var msg = StartMessage();

            msg.Write((byte)PacketType.Skill);

            SendMessage(msg);
        }

        public void StopPlayer()
        {
            var msg = StartMessage();

            msg.Write((byte)PacketType.StopAction);

            SendMessage(msg);
        }

        public void SendAttack(int target)
        {
            var msg = StartMessage();

            msg.Write((byte)PacketType.Attack);
            msg.Write(target);

            SendMessage(msg);
        }

        private void SendPing()
        {
            var msg = StartMessage();
            msg.Write((byte)PacketType.Ping);

            SendMessage(msg);
        }

        public void SendChangeAppearance(int mode, int id = -1, int subId = -1)
        {
            var msg = StartMessage();

            msg.Write((byte)PacketType.AdminChangeAppearance);
            msg.Write(mode);
            msg.Write(id);
            msg.Write(subId);

            SendMessage(msg);
        }


        public void SendRespawn(bool inPlace)
        {
            var msg = StartMessage();

            msg.Write((byte)PacketType.Respawn);
            msg.Write((byte)(inPlace ? 1 : 0));

            SendMessage(msg);
        }

        public void SendSay(string text, bool isShout = false)
        {
            var msg = StartMessage();

            msg.Write((byte)PacketType.Say);
            msg.Write(text);
            msg.Write(isShout);

            SendMessage(msg);
        }

        public void SendChangeName(string text)
        {
            var msg = StartMessage();

            msg.Write((byte)PacketType.ChangeName);
            msg.Write(text);

            SendMessage(msg);
        }

        public void SendAdminFind(string target)
        {
            var msg = StartMessage();

            msg.Write((byte)PacketType.AdminFindTarget);
            msg.Write(target);

            SendMessage(msg);
        }

        public void SendClientTextCommand(ClientTextCommand cmd, string text = "")
        {
            var msg = StartMessage();

            msg.Write((byte)PacketType.ClientTextCommand);
            msg.Write((byte)cmd);
            msg.Write(text);

            SendMessage(msg);
        }

        public void SendMoveRequest(string map, int x = -999, int y = -999, bool forcePosition = false)
        {
            if (map.ToLower() == "debug" || map.ToLower() == "debugroom")
                map = "2009rwc_03";

            var msg = StartMessage();

            msg.Write((byte)PacketType.AdminRequestMove);
            msg.Write(map);
            msg.Write((short)x);
            msg.Write((short)y);
            msg.Write(forcePosition);

            SendMessage(msg);
        }

        public void SendAdminCreateItem(int itemId, int count)
        {
            var msg = StartMessage();

            msg.Write((byte)PacketType.AdminCreateItem);
            msg.Write(itemId);
            msg.Write(count);

            SendMessage(msg);
        }

        public void SendAdminSummonMonster(string name, int count, bool isBoss = false)
        {
            var msg = StartMessage();

            Debug.Log("Summon: " + name);

            msg.Write((byte)PacketType.AdminSummonMonster);
            msg.Write(name);
            msg.Write((short)count);
            msg.Write(isBoss);

            SendMessage(msg);
        }

        public void SendAdminLevelUpRequest(int level)
        {
            var msg = StartMessage();

            msg.Write((byte)PacketType.AdminLevelUp);
            msg.Write((sbyte)level);

            SendMessage(msg);
        }

        public void SendUnEquipItem(int id)
        {
            var msg = StartMessage();

            msg.Write((byte)PacketType.EquipUnequipGear);
            msg.Write(id);
            msg.Write(false);

            SendMessage(msg);
        }

        public void SendSocketItem(int targetItem, int srcItem)
        {
            var msg = StartMessage();
            
            msg.Write((byte)PacketType.SocketEquipment);
            msg.Write(targetItem);
            msg.Write(srcItem);
            
            SendMessage(msg);
        }

        public void SendEquipItem(int id)
        {
            var msg = StartMessage();

            msg.Write((byte)PacketType.EquipUnequipGear);
            msg.Write(id);
            msg.Write(true);

            SendMessage(msg);
        }

        public void SendUseItem(int id, int target = -1)
        {
            //Debug.Log("Send usable item");
            var msg = StartMessage();

            msg.Write((byte)PacketType.UseInventoryItem);
            msg.Write(id);
            msg.Write(target);

            SendMessage(msg);
        }

        public void SendEmote(int id)
        {
            var msg = StartMessage();

            msg.Write((byte)PacketType.Emote);
            msg.Write(id);

            SendMessage(msg);
        }

        public void SendAdminAction(AdminAction action)
        {
            var msg = StartMessage();

            msg.Write((byte)PacketType.AdminServerAction);
            msg.Write((byte)action);

            SendMessage(msg);
        }


        public void SendAdminKillMobAction(bool clearMap)
        {
            var msg = StartMessage();

            msg.Write((byte)PacketType.AdminServerAction);
            msg.Write((byte)AdminAction.KillMobs);
            msg.Write(clearMap);

            SendMessage(msg);
        }

        public void SendPickUpItem(int target)
        {
            var msg = StartMessage();

            msg.Write((byte)PacketType.PickUpItem);
            msg.Write(target);

            SendMessage(msg);
        }

        public void SendNpcClick(int target)
        {
            var msg = StartMessage();

            msg.Write((byte)PacketType.NpcClick);
            msg.Write(target);

            SendMessage(msg);
        }

        public void SendNpcAdvance()
        {
            var msg = StartMessage();

            msg.Write((byte)PacketType.NpcAdvance);

            SendMessage(msg);
        }

        public void SendNpcSelectOption(int result)
        {
            var msg = StartMessage();

            msg.Write((byte)PacketType.NpcSelectOption);
            msg.Write(result);

            SendMessage(msg);
        }

        public void SendAdminHideCharacter(bool desireHidden)
        {
            var msg = StartMessage();

            msg.Write((byte)PacketType.AdminHideCharacter);
            msg.Write(desireHidden);

            SendMessage(msg);
        }

        public void SendAdminChangeSpeed(int value)
        {
            var msg = StartMessage();

            msg.Write((byte)PacketType.AdminChangeSpeed);
            msg.Write((Int16)value);

            SendMessage(msg);
        }

        public void SendSingleTargetSkillAction(int targetId, CharacterSkill skill, int lvl)
        {
            var msg = StartMessage();

            msg.Write((byte)PacketType.Skill);
            msg.Write((byte)SkillTarget.Enemy);
            msg.Write(targetId);
            msg.Write((byte)skill);
            msg.Write((byte)lvl);

            SendMessage(msg);
        }

        public void SendGroundTargetSkillAction(Vector2Int target, CharacterSkill skill, int lvl)
        {
            var msg = StartMessage();

            msg.Write((byte)PacketType.Skill);
            msg.Write((byte)SkillTarget.Ground);
            msg.Write(target);
            msg.Write((byte)skill);
            msg.Write((byte)lvl);

            SendMessage(msg);
        }

        public void SendSelfTargetSkillAction(CharacterSkill skill, int lvl)
        {
            var msg = StartMessage();

            msg.Write((byte)PacketType.Skill);
            msg.Write((byte)SkillTarget.Self);
            msg.Write((short)skill);
            msg.Write((byte)lvl);

            SendMessage(msg);
        }

        public void SendApplySkillPoint(CharacterSkill skill)
        {
            var msg = StartMessage();

            msg.Write((byte)PacketType.ApplySkillPoint);
            msg.Write((byte)skill);

            SendMessage(msg);
        }

        public void SendAdminResetSkillPoints()
        {
            var msg = StartMessage();

            msg.Write((byte)PacketType.AdminResetSkills);

            SendMessage(msg);
        }

        public void SendAdminResetStatPoints()
        {
            var msg = StartMessage();

            msg.Write((byte)PacketType.AdminResetStats);

            SendMessage(msg);
        }

        public void SendApplyStatPoints(int[] statChanges)
        {
            var msg = StartMessage();
            msg.Write((byte)PacketType.ApplyStatPoints);

            for (var i = 0; i < 6; i++)
                msg.Write(statChanges[i]);

            SendMessage(msg);
        }

        public void SendDropItem(int bagId, int count)
        {
            var msg = StartMessage();

            msg.Write((byte)PacketType.DropItem);
            msg.Write(bagId);
            msg.Write((short)count);

            SendMessage(msg);
        }

        public void SubmitShopPurchase(List<ShopEntry> items)
        {
            var msg = StartMessage();

            msg.Write((byte)PacketType.ShopBuySell);
            if (items == null || items.Count == 0)
                msg.Write(0);
            else
            {
                msg.Write(items.Count);
                for (var i = 0; i < items.Count; i++)
                {
                    msg.Write(items[i].ItemId);
                    msg.Write(items[i].Count);
                }
            }

            SendMessage(msg);
        }

        public void SendEnterServerMessage(string loginName)
        {
            var msg = StartMessage();

            msg.Write((byte)PacketType.EnterServer);
            msg.Write(false);
            msg.Write(loginName);

            SendMessage(msg);
        }


        public void SendEnterServerNewCharacterMessage(string chName, int slot, int head, int hair, int[] stats, bool isMale)
        {
            var msg = StartMessage();

            msg.Write((byte)PacketType.EnterServer);
            msg.Write(true);
            msg.Write(chName);
            msg.Write(head);
            msg.Write(hair);
            msg.Write((byte)slot);
            for (var i = 0; i < 6; i++)
                msg.Write((byte)stats[i]);
            msg.Write(isMale);

            SendMessage(msg);
        }

        public void AttachEffectToEntity(int effectId)
        {
        }

        private void Update()
        {
            if (socket == null)
                return;

            //Debug.Log("Socket Power! State: " + socket.State);

            //#if !UNITY_WEBGL || UNITY_EDITOR
            //			socket.DispatchMessageQueue();
            //#endif

            var state = socket.GetState();

            if (InboundMessages.Count > 0)
                DoPacketHandling();

            if (state == WebSocketState.Open)
            {
                if (lastPing + 5 < Time.time)
                {
                    SendPing();
                    //Debug.Log("Sending keep alive packet.");

                    lastPing = Time.time;
                }
            }
//
//             if (state == WebSocketState.Open && isReady && !isConnected)
//             {
//                 Debug.Log("We've been accepted! Lets try to enter the game.");
//                 SendPing();
//                 var msg = StartMessage();
// #if DEBUG
//                 if (!string.IsNullOrWhiteSpace(SpawnMap))
//                 {
//                     msg.Write((byte)PacketType.AdminEnterServerSpecificMap);
//                     msg.Write(SpawnMap);
//
//                     var prefx = PlayerPrefs.GetInt("DebugStartX", -1);
//                     var prefy = PlayerPrefs.GetInt("DebugStartY", -1);
//
//                     //Debug.Log(prefx + " : " + prefy);
//
//                     if (prefx > 0 && prefy > 0)
//                     {
//                         msg.Write(true);
//                         msg.Write((short)prefx);
//                         msg.Write((short)prefy);
//                         PlayerPrefs.DeleteKey("DebugStartX");
//                         PlayerPrefs.DeleteKey("DebugStartY");
//                     }
//
//                     msg.Write(false);
//                 }
//                 else
//                     msg.Write((byte)PacketType.EnterServer);
// #else
// 				msg.Write((byte)PacketType.EnterServer);
// #endif
//                 SendMessage(msg);
//                 isConnected = true;
//             }

            if (isConnected && state != WebSocketState.Open && state != WebSocketState.Connecting)
            {
                isConnected = false;
                CameraFollower.SetErrorUiText("Lost connection to server!\n<size=-4>(Press space to try to reconnect)");
                if (isOnLoginScreen)
                    Debug.LogWarning($"Client state has changed to: {state} (OnLoginScreen)");
                else
                    Debug.LogWarning($"Client state has changed to: {state} (Previous ready state: {isReady})");
                isReady = false;
            }

            if (!isConnected)
                return;

            if (state == WebSocketState.Closed)
            {
                Console.WriteLine("Disconnected!");
                return;
            }
        }

        public void OnDestroy()
        {
            Disconnect();
        }

        public void OnApplicationQuit()
        {
            Disconnect();
        }
    }
}