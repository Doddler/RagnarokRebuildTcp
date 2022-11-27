using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Assets.Scripts.MapEditor;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.Sprites;
using Assets.Scripts.Utility;
using HybridWebSocket;
using Lidgren.Network;
using RebuildSharedData.ClientTypes;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

namespace Assets.Scripts.Network
{
    class NetworkManager : MonoBehaviour
    {
        public static NetworkManager Instance;

        private WebSocket socket;

        public CameraFollower CameraFollower;
        public GameObject DamagePrefab;
        public GameObject HealPrefab;
        public GameObject TargetNoticePrefab;
        public Dictionary<int, ServerControllable> entityList = new Dictionary<int, ServerControllable>();
        public int PlayerId;
        public NetQueue<ClientInboundMessage> InboundMessages = new NetQueue<ClientInboundMessage>(30);
        public NetQueue<ClientOutgoingMessage> OutboundMessages = new NetQueue<ClientOutgoingMessage>(30);

        public PlayerState PlayerState = new PlayerState();

        //private static NetClient client;

        private float lastPing = 0;
        private bool isReady = false;
        private bool isConnected = false;

        public Color FakeAmbient = Color.white;

        public string CurrentMap = "";

        private List<Vector2Int> pathData = new List<Vector2Int>(20);

        private Scene currentScene;

        private AsyncOperationHandle spritePreload;
        private AsyncOperationHandle uiPreload;

        public Guid CharacterGuid;


#if DEBUG
        public static string SpawnMap = "";
#endif

        private async void Start()
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
            //update addressables
            AsyncOperationHandle<List<IResourceLocator>> updateHandle = Addressables.UpdateCatalogs();
            yield return updateHandle;

            spritePreload = Addressables.DownloadDependenciesAsync($"Assets/Sprites/Monsters/poring.spr");
            uiPreload = Addressables.DownloadDependenciesAsync($"gridicon");

            yield return spritePreload;
            yield return uiPreload;

#if UNITY_EDITOR
            var target = "ws://127.0.0.1:5000/ws";
            StartConnectServer(target);
            yield break; //end coroutine
#endif

            if (Application.platform == RuntimePlatform.WindowsPlayer)
            {
                var path = File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "serverconfig.txt"));
                StartConnectServer(path);
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
                Debug.Log("Connecting to server at: " + www.text);

                while (!spritePreload.IsDone || !uiPreload.IsDone)
                    yield return new WaitForSeconds(0.1f);

                StartConnectServer(www.text);
            }
        }

        private void StartConnectServer(string serverPath)
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
            };


            socket.OnError += e =>
            {
                Debug.LogError("Socket connection had an error: " + e);
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

        public ClientOutgoingMessage StartMessage()
        {
            //we should use a pool for these
            return new ClientOutgoingMessage();
        }

        public void SendMessage(ClientOutgoingMessage message)
        {
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

            entityList.Clear();
        }

        private Vector2Int ReadPosition(ClientInboundMessage msg)
        {
            var x = msg.ReadInt16();
            var y = msg.ReadInt16();
            return new Vector2Int(x, y);
        }

        private void LoadMoveData(ClientInboundMessage msg, ServerControllable ctrl)
        {
            var moveSpeed = msg.ReadFloat();
            var moveCooldown = msg.ReadFloat();
            var totalSteps = (int)msg.ReadByte();
            var curStep = (int)msg.ReadByte();

            pathData.Clear();
            if (totalSteps > 0) //should always be true but whatever
            {
                pathData.Add(ReadPosition(msg));
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
            //for (var i = 0; i < totalSteps; i++)
            //	pathData.Add(ReadPosition(msg));

            //if(ctrl.Id == PlayerId)
            //	Debug.Log("Doing move for player!");

            ctrl.StartMove(moveSpeed, moveCooldown, totalSteps, curStep, pathData);
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

            ServerControllable controllable;
            if (type == CharacterType.Player)
            {
                lvl = (int)msg.ReadByte();
                maxHp = (int)msg.ReadUInt16();
                hp = (int)msg.ReadUInt16();

                var headFacing = (HeadFacing)msg.ReadByte();
                var headId = msg.ReadByte();
                var isMale = msg.ReadBoolean();
                var name = msg.ReadString();
                var isMain = PlayerId == id;
                
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
                    IsMainCharacter = isMain,
                };
                
                controllable = SpriteDataLoader.Instance.InstantiatePlayer(ref playerData);


                if (id == PlayerId)
                {
                    PlayerState.Level = lvl;
                    
                    CameraFollower.UpdatePlayerHP(hp, maxHp);
                    var max = CameraFollower.Instance.ExpForLevel(controllable.Level-1);
                    CameraFollower.UpdatePlayerExp(PlayerState.Exp, max);
                }
            }
            else
            {
                var interactable = false;
                var name = string.Empty;

                if (type == CharacterType.Monster)
                {
                    lvl = (int)msg.ReadByte();
                    maxHp = (int)msg.ReadUInt16();
                    hp = (int)msg.ReadUInt16();
                }

                if (type == CharacterType.NPC)
                {
                    name = msg.ReadString();
                    interactable = msg.ReadBoolean();
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
                controllable = SpriteDataLoader.Instance.InstantiateMonster(ref monData);
            }

            entityList.Add(id, controllable);

            if (controllable.SpriteMode == ClientSpriteType.Prefab)
                return controllable;

            if (state == CharacterState.Moving)
                LoadMoveData(msg, controllable);
            if (state == CharacterState.Sitting)
            {
                controllable.SpriteAnimator.ChangeMotion(SpriteMotion.Sit);
                controllable.SpriteAnimator.State = SpriteState.Sit;
            }

            if (PlayerId == controllable.Id)
            {
                CameraFollower.Target = controllable.gameObject;
                //Debug.Log($"Player entity sent, we're at position {pos}");

                SceneTransitioner.Instance.FadeIn();
                CameraFollower.Instance.SnapLookAt();
            }

            return controllable;
        }


        private void OnMessageChangeSitStand(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();
            var isSitting = msg.ReadBoolean();

            if (!entityList.TryGetValue(id, out var controllable))
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
            var facing = (Direction)msg.ReadByte();

            if (!entityList.TryGetValue(id, out var controllable))
            {
                Debug.LogWarning("Trying to move entity " + id + ", but it does not exist in scene!");
                return;
            }

            controllable.SpriteAnimator.Direction = facing;
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

            if (!entityList.TryGetValue(id, out var controllable))
            {
                Debug.LogWarning("Trying to move entity " + id + ", but it does not exist in scene!");
                return;
            }

            controllable.MovePosition(ReadPosition(msg));
        }

        private void OnMessageStartMove(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();

            if (!entityList.TryGetValue(id, out var controllable))
            {
                Debug.LogWarning("Trying to move entity " + id + ", but it does not exist in scene!");
                return;
            }

            LoadMoveData(msg, controllable);
        }

        private void OnMessageChangeMaps(ClientInboundMessage msg)
        {
            var mapName = msg.ReadString();

            entityList.Clear();

            CurrentMap = mapName;
            //var mapLoad = SceneManager.LoadSceneAsync(mapName, LoadSceneMode.Additive);
            //mapLoad.completed += OnMapLoad;

            SceneTransitioner.Instance.DoTransitionToScene(currentScene, CurrentMap, OnMapLoad);

            //SceneManager.UnloadSceneAsync(currentScene);
        }

        private void OnMessageEnterServer(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();
            var mapName = msg.ReadString();
            var bytes = new byte[16];
            msg.ReadBytes(bytes, 16);
            CharacterGuid = new Guid(bytes);
            PlayerPrefs.SetString("characterid", CharacterGuid.ToString());

            Debug.Log($"We're id {id} on map {mapName} with guid {CharacterGuid}");

            CurrentMap = mapName;

            //var mapLoad = SceneManager.LoadSceneAsync(mapName, LoadSceneMode.Additive);
            //mapLoad.completed += OnMapLoad;

            PlayerId = id;

            SceneTransitioner.Instance.LoadScene(CurrentMap, OnMapLoad);
        }

        private void OnMessageRemoveEntity(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();
            var reason = (CharacterRemovalReason)msg.ReadByte();

            if (!entityList.TryGetValue(id, out var controllable))
            {
                Debug.LogWarning("Trying to remove entity " + id + ", but it does not exist in scene!");
                return;
            }
            
            if (id == PlayerId)
            {
                //Debug.Log("We're removing the player object! Hopefully the server knows what it's doing. We're just going to pretend we didn't see it.");
                //return;

                Debug.LogWarning("Whoa! Trying to delete player object. Is that right...?");
                CameraFollower.Instance.Target = null;
            }


            entityList.Remove(id);

            if (reason == CharacterRemovalReason.Dead)
            {
                if (controllable.SpriteAnimator.Type != SpriteType.Player)
                {
                    controllable.MonsterDie(1);
                    //               var vDir = -controllable.CounterHitDir;
                    //Debug.Log(vDir);
                    //var newDir = new Vector3(vDir.x, 0, vDir.z).normalized * 8f;
                    //               newDir.y = 8f;
                    //controllable.BlastOff(newDir * 5f);
                }
                else
                    controllable.FadeOutAndVanish(0.1f);
            }
            else
            {
                if (CameraFollower.SelectedTarget == controllable.gameObject)
                    CameraFollower.ClearSelected();

                controllable.FadeOutAndVanish(0.1f);
            }
            //GameObject.Destroy(controllable.gameObject);
        }

        private void OnMessageRemoveAllEntities(ClientInboundMessage msg)
        {
            foreach (var entity in entityList)
            {
                GameObject.Destroy(entity.Value.gameObject);
            }

            entityList.Clear();
        }


        private void OnMessageStopImmediate(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();

            if (!entityList.TryGetValue(id, out var controllable))
            {
                Debug.LogWarning("Trying to stop entity " + id + ", but it does not exist in scene!");
                return;
            }

            var pos = ReadPosition(msg);

            controllable.StopImmediate(pos);
        }

        private void OnMessageStopPlayer(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();

            if (!entityList.TryGetValue(id, out var controllable))
            {
                Debug.LogWarning("Trying to stop entity " + id + ", but it does not exist in scene!");
                return;
            }

            if (id == PlayerId)
                CameraFollower.ClearSelected();

            controllable.StopWalking();
        }

        private void OnMessageAttack(ClientInboundMessage msg)
        {
            var id1 = msg.ReadInt32();
            var id2 = msg.ReadInt32();

            if (!entityList.TryGetValue(id1, out var controllable))
            {
                Debug.LogWarning("Trying to attack entity " + id1 + ", but it does not exist in scene!");
                return;
            }

            var hasTarget = entityList.TryGetValue(id2, out var controllable2);

            var dir = (Direction)msg.ReadByte();
            var pos = ReadPosition(msg);
            var dmg = msg.ReadInt16();

            if (hasTarget)
            {
                var cd = controllable.transform.localPosition - controllable2.transform.localPosition;
                cd.y = 0;
                controllable2.CounterHitDir = cd.normalized;
                //Debug.Log("Counter hit: " + cd);
            }
            else
            {
                var v = dir.GetVectorValue();
                controllable.CounterHitDir = new Vector3(v.x, 0, v.y);
            }

            controllable.StopImmediate(pos);
            controllable.SpriteAnimator.Direction = dir;
            controllable.SpriteAnimator.State = SpriteState.Idle;
            controllable.SpriteAnimator.AnimSpeed = 1f;
            if (controllable.SpriteAnimator.Type == SpriteType.Player)
            {
                if (controllable.IsMale)
                    controllable.SpriteAnimator.ChangeMotion(SpriteMotion.Attack2, true);
                else
                    controllable.SpriteAnimator.ChangeMotion(SpriteMotion.Attack3, true);
            }
            else
                controllable.SpriteAnimator.ChangeMotion(SpriteMotion.Attack1, true);

            //controllable2.SpriteAnimator.ChangeMotion(SpriteMotion.Hit);

            if (hasTarget && controllable.SpriteAnimator.IsInitialized)
            {
                if (controllable.SpriteAnimator.SpriteData == null)
                {
                    throw new Exception("AAA? " + controllable.gameObject.name + " " + controllable.gameObject);
                }

                var damageTiming = controllable.SpriteAnimator.SpriteData.AttackFrameTime / 1000f;
                if (controllable.SpriteAnimator.Type == SpriteType.Player)
                    damageTiming = 0.5f;

                StartCoroutine(DamageEvent(dmg, damageTiming, 1, controllable2));
            }
        }

        private IEnumerator DamageEvent(int damage, float delay, int hitCount, ServerControllable target)
        {
            yield return new WaitForSeconds(delay);
            if (target != null && target.SpriteAnimator.IsInitialized)
            {
                for (var i = 0; i < hitCount; i++)
                {
                    var go = GameObject.Instantiate(DamagePrefab, target.transform.localPosition, Quaternion.identity);
                    var di = go.GetComponent<DamageIndicator>();
                    var red = target.SpriteAnimator.Type == SpriteType.Player;
                    var height = 1f;
                    di.DoDamage(damage.ToString(), target.gameObject.transform.localPosition, height,
                        target.SpriteAnimator.Direction, red, false);
                    yield return new WaitForSeconds(0.2f);
                }
            }
        }

        private void OnMessageHit(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();
            var delay = msg.ReadFloat();
            var damage = msg.ReadInt32();

            if (!entityList.TryGetValue(id, out var controllable))
            {
                //Debug.LogWarning("Trying to do hit entity " + id1 + ", but it does not exist in scene!");
                return;
            }

            controllable.Hp -= damage;
            if (controllable.Hp < 0)
                controllable.Hp = 0;

            if (id == PlayerId)
                CameraFollower.UpdatePlayerHP(controllable.Hp, controllable.MaxHp);

            if (delay < 0)
                return;

            //Debug.Log("Move delay is " + delay);
            controllable.SetHitDelay(delay);

            if (controllable.SpriteAnimator.CurrentMotion == SpriteMotion.Dead)
                return;

            if (controllable.SpriteAnimator.Type == SpriteType.Player)
                controllable.SpriteAnimator.State = SpriteState.Standby;

            if (!controllable.SpriteAnimator.IsAttackMotion)
                controllable.SpriteAnimator.ChangeMotion(SpriteMotion.Hit);
        }

        public void OnMessageChangeTarget(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();

            //Debug.Log("Packet Change Target to target: " + id);

            if (id == 0)
            {
                CameraFollower.ClearSelected();
                return;
            }

            if (!entityList.TryGetValue(id, out var controllable))
                return;

            CameraFollower.SetSelectedTarget(controllable.gameObject, controllable.DisplayName, controllable.IsAlly, false);
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

            if (!entityList.TryGetValue(PlayerId, out var controllable))
                return;
            

            var go = GameObject.Instantiate(HealPrefab, controllable.transform.localPosition, Quaternion.identity);
            var di = go.GetComponent<DamageIndicator>();
            var height = 72f / 50f;

            if (controllable.SpriteAnimator != null)
                height = controllable.SpriteAnimator.SpriteData.Size / 50f;

            di.DoDamage($"<color=yellow>+{exp} Exp", controllable.gameObject.transform.localPosition, height, Direction.None, false, false);

            PlayerState.Exp += exp;
            var max = CameraFollower.Instance.ExpForLevel(controllable.Level-1);
            CameraFollower.Instance.UpdatePlayerExp(PlayerState.Exp, max);
        }

        public void OnMessageLevelUp(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();
            var lvl = msg.ReadByte();

            if (!entityList.TryGetValue(id, out var controllable))
            {
                //Debug.LogWarning("Trying to do hit entity " + id1 + ", but it does not exist in scene!");
                return;
            }

            var go = GameObject.Instantiate(CameraFollower.LevelUpPrefab);
            go.transform.SetParent(controllable.transform, true);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            controllable.Level = lvl;
            var req = CameraFollower.Instance.ExpForLevel(lvl-2);
            PlayerState.Exp -= req;
            PlayerState.Level = lvl;
            CameraFollower.Instance.UpdatePlayerExp(PlayerState.Exp, req);
        }
        
        public void OnMessageResurrection(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();
            var pos = ReadPosition(msg);

            if (!entityList.TryGetValue(id, out var controllable))
            {
                //Debug.LogWarning("Trying to do hit entity " + id1 + ", but it does not exist in scene!");
                return;
            }

            var go = GameObject.Instantiate(CameraFollower.ResurrectPrefab);
            go.transform.SetParent(controllable.transform, true);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            controllable.SpriteAnimator.State = SpriteState.Idle;
            controllable.SpriteAnimator.ChangeMotion(SpriteMotion.Idle, true);
        }

        public void OnMessageDeath(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();
            var pos = ReadPosition(msg);

            if (!entityList.TryGetValue(id, out var controllable))
            {
                Debug.LogWarning("Trying to do death on entity " + id + ", but it does not exist in scene!");
                return;
            }

            Debug.Log($"{controllable.Name} is dead!");

            if (id == PlayerId)
                CameraFollower.AppendChatText("You have died! Press R key to respawn, or press shift + R to resurrect in place.");

            controllable.StopImmediate(pos);
            controllable.SpriteAnimator.State = SpriteState.Dead;
            controllable.SpriteAnimator.ChangeMotion(SpriteMotion.Dead, true);

            if (id == PlayerId)
            {
                var go = GameObject.Instantiate(CameraFollower.DeathPrefab);
                go.transform.SetParent(controllable.transform, true);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
            }
        }

        public void OnMessageHpRecovery(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();
            var amnt = msg.ReadInt32();
            var hp = msg.ReadInt32();
            var maxHp = msg.ReadInt32();
            var type = (HealType)msg.ReadByte();

            if (!entityList.TryGetValue(id, out var controllable))
            {
                //Debug.LogWarning("Trying to do hit entity " + id1 + ", but it does not exist in scene!");
                return;
            }

            controllable.Hp = hp;
            controllable.MaxHp = maxHp;

            if (id == PlayerId)
                CameraFollower.UpdatePlayerHP(hp, maxHp);
        }

        public void OnMessageMonsterTarget(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();

            //Debug.Log("TARGET! " + id);

            if (!entityList.TryGetValue(id, out var controllable))
                return;

            var targetIcon = GameObject.Instantiate(TargetNoticePrefab);
            targetIcon.transform.SetParent(controllable.transform);
            targetIcon.transform.localPosition = Vector3.zero;
        }

        public void OnMessageSay(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();
            var text = msg.ReadString();

            if (!entityList.TryGetValue(id, out var controllable))
            {
                CameraFollower.AppendChatText("Unknown: " + text);
                return;
            }
            controllable.DialogBox(controllable.Name + ": " + text);

            CameraFollower.AppendChatText(controllable.Name + ": " + text);
        }

        public void OnMessageChangeName(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();
            var text = msg.ReadString();

            if (!entityList.TryGetValue(id, out var controllable))
                return;

            CameraFollower.AppendChatText($"{controllable.Name} has changed their name to {text}.");
            controllable.Name = text;
        }

        public void OnMessageRequestFailed(ClientInboundMessage msg)
        {
            var error = (ClientErrorType)msg.ReadByte();

            switch (error)
            {
                case ClientErrorType.InvalidCoordinates:
                    CameraFollower.Instance.AppendChatText("<color=#FF3030>Error</color>: Coordinates were invalid.");
                    break;
                case ClientErrorType.TooManyRequests:
                    CameraFollower.Instance.AppendChatText("<color=yellow>Warning</color>: Too many actions or requests.");
                    break;
                case ClientErrorType.UnknownMap:
                    CameraFollower.Instance.AppendChatText("<color=#FF3030>Error</color>: Could not find map.");
                    break;
                case ClientErrorType.MalformedRequest:
                    CameraFollower.Instance.AppendChatText("<color=#FF3030>Error</color>: Request could not be completed due to malformed data.");
                    break;
                case ClientErrorType.RequestTooLong:
                    CameraFollower.Instance.AppendChatText("<color=#FF3030>Error</color>: The request data was too long.");
                    break;
            }
        }

        public void OnMessageEffect(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();
            var effect = msg.ReadInt32();


            if (!entityList.TryGetValue(id, out var controllable))
                return;

            CameraFollower.AttachEffectToEntity(effect, controllable);
        }

        void HandleDataPacket(ClientInboundMessage msg)
        {
            var type = (PacketType)msg.ReadByte();

            //Debug.Log("Received packet type: " + type);

            switch (type)
            {
                case PacketType.ConnectionApproved:
                    isReady = true;
                    break;
                case PacketType.StartMove:
                    OnMessageStartMove(msg);
                    break;
                case PacketType.RemoveAllEntities:
                    OnMessageRemoveAllEntities(msg);
                    break;
                case PacketType.RemoveEntity:
                    OnMessageRemoveEntity(msg);
                    break;
                case PacketType.CreateEntity:
                    OnMessageCreateEntity(msg);
                    break;
                case PacketType.EnterServer:
                    OnMessageEnterServer(msg);
                    break;
                case PacketType.LookTowards:
                    OnMessageChangeFacing(msg);
                    break;
                case PacketType.SitStand:
                    OnMessageChangeSitStand(msg);
                    break;
                case PacketType.ChangeMaps:
                    OnMessageChangeMaps(msg);
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
                case PacketType.Attack:
                    OnMessageAttack(msg);
                    break;
                case PacketType.HitTarget:
                    OnMessageHit(msg);
                    break;
                case PacketType.ChangeTarget:
                    OnMessageChangeTarget(msg);
                    break;
                case PacketType.GainExp:
                    OnMessageGainExp(msg);
                    break;
                case PacketType.LevelUp:
                    OnMessageLevelUp(msg);
                    break;
                case PacketType.Resurrection:
                    OnMessageResurrection(msg);
                    break;
                case PacketType.Death:
                    OnMessageDeath(msg);
                    break;
                case PacketType.HpRecovery:
                    OnMessageHpRecovery(msg);
                    break;
                case PacketType.Targeted:
                    OnMessageMonsterTarget(msg);
                    break;
                case PacketType.RequestFailed:
                    OnMessageRequestFailed(msg);
                    break;
                case PacketType.Say:
                    OnMessageSay(msg);
                    break;
                case PacketType.ChangeName:
                    OnMessageChangeName(msg);
                    break;
                case PacketType.Effect:
                    OnMessageEffect(msg);
                    break;
                default:
                    Debug.LogWarning($"Failed to handle packet type: {type}");
                    break;
            }
        }

        private void DoPacketHandling()
        {
            while (InboundMessages.TryDequeue(out var msg))
            {
                HandleDataPacket(msg);
                //switch (msg.MessageType)
                //{
                //	case NetIncomingMessageType.Data:
                //		HandleDataPacket(msg);
                //		break;
                //	case NetIncomingMessageType.DebugMessage:
                //	case NetIncomingMessageType.VerboseDebugMessage:
                //	case NetIncomingMessageType.WarningMessage:
                //	case NetIncomingMessageType.ErrorMessage:
                //		Debug.Log(msg.MessageType + ": " + msg.ReadString());
                //		break;
                //	case NetIncomingMessageType.StatusChanged:
                //		Debug.Log("Status changed: " + client.Status);
                //		break;
                //	default:
                //		Debug.Log("We received a packet type we didn't handle: " + msg.MessageType);
                //		break;
                //}
            }
        }

        private void OnMapLoad()
        {
            var msg = StartMessage();

            msg.Write((byte)PacketType.PlayerReady);

            currentScene = SceneManager.GetSceneByName(CurrentMap);
            SceneManager.SetActiveScene(currentScene);

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
            msg.Write((byte)PacketType.StartMove);
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


        public void SendRandomizeAppearance(int mode, int id = -1)
        {
            var msg = StartMessage();

            msg.Write((byte)PacketType.AdminRandomizeAppearance);
            msg.Write(mode);
            msg.Write(id);

            SendMessage(msg);
        }


        public void SendRespawn(bool inPlace)
        {
            var msg = StartMessage();

            msg.Write((byte)PacketType.Respawn);
            msg.Write((byte)(inPlace ? 1 : 0));

            SendMessage(msg);
        }

        public void SendSay(string text)
        {
            var msg = StartMessage();

            msg.Write((byte)PacketType.Say);
            msg.Write(text);

            SendMessage(msg);
        }

        public void SendChangeName(string text)
        {
            var msg = StartMessage();

            msg.Write((byte)PacketType.ChangeName);
            msg.Write(text);

            SendMessage(msg);
        }

        public void SendMoveRequest(string map, int x = -999, int y = -999)
        {
            if (map.ToLower() == "debug" || map.ToLower() == "debugroom")
                map = "2009rwc_03";

            var msg = StartMessage();

            msg.Write((byte)PacketType.AdminRequestMove);
            msg.Write(map);
            msg.Write((short)x);
            msg.Write((short)y);

            SendMessage(msg);
        }

        public void SendAdminLevelUpRequest(int level)
        {
            var msg = StartMessage();

            msg.Write((byte)PacketType.AdminLevelUp);
            msg.Write((sbyte)level);

            SendMessage(msg);
        }

        public void SendUseItem(int id)
        {
            //Debug.Log("Send usable item");
            var msg = StartMessage();

            msg.Write((byte)PacketType.UseInventoryItem);
            msg.Write(id);

            SendMessage(msg);
        }

        public void SendReloadScript()
        {
            var msg = StartMessage();

            msg.Write((byte)PacketType.AdminServerAction);
            msg.Write((byte)AdminAction.ReloadScripts);

            SendMessage(msg);
        }
        
        public void SendNpcClick(int target)
        {
            var msg = StartMessage();

            msg.Write((byte)PacketType.NpcClick);
            msg.Write(target);

            SendMessage(msg);
        }

        public void AttachEffectToEntity(int effectId)
        {

        }

        private void Update()
        {
            Shader.SetGlobalColor("_FakeAmbient", FakeAmbient);

            if (socket == null)
                return;

            //Debug.Log("Socket Power! State: " + socket.State);

            //#if !UNITY_WEBGL || UNITY_EDITOR
            //			socket.DispatchMessageQueue();
            //#endif

            var state = socket.GetState();

            if (state == WebSocketState.Open)
                DoPacketHandling();

            if (state == WebSocketState.Open && isReady && !isConnected)
            {
                Debug.Log("We've been accepted! Lets try to enter the game.");
                SendPing();
                var msg = StartMessage();
#if DEBUG
                if (!string.IsNullOrWhiteSpace(SpawnMap))
                {
                    msg.Write((byte)PacketType.AdminEnterServerSpecificMap);
                    msg.Write(SpawnMap);

                    var prefx = PlayerPrefs.GetInt("DebugStartX", -1);
                    var prefy = PlayerPrefs.GetInt("DebugStartY", -1);

                    //Debug.Log(prefx + " : " + prefy);

                    if (prefx > 0 && prefy > 0)
                    {
                        msg.Write(true);
                        msg.Write((short)prefx);
                        msg.Write((short)prefy);
                        PlayerPrefs.DeleteKey("DebugStartX");
                        PlayerPrefs.DeleteKey("DebugStartY");

                    }
                    msg.Write(false);
                }
                else
                    msg.Write((byte)PacketType.EnterServer);
#else
				msg.Write((byte)PacketType.EnterServer);
#endif
                SendMessage(msg);
                isConnected = true;
            }

            if (isConnected && state != WebSocketState.Open && state != WebSocketState.Connecting)
            {
                isConnected = false;
                Debug.LogWarning("Client state has changed to: " + state);
            }

            if (!isConnected)
                return;


            if (state == WebSocketState.Closed)
            {
                Console.WriteLine("Disconnected!");
                return;
            }

            if (lastPing + 5 < Time.time)
            {
                SendPing();
                //Debug.Log("Sending keep alive packet.");

                lastPing = Time.time;
            }
        }

        public void OnApplicationQuit()
        {
            if (socket == null)
                return;
            var outmsg = StartMessage();
            outmsg.Write((byte)PacketType.Disconnect);
            SendMessage(outmsg);
        }
    }
}
