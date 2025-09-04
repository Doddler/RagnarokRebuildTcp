using Assets.Scripts.Sprites;
using PlayerControl;
using RebuildSharedData.ClientTypes;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Assets.Scripts.UI
{
    public class EmoteWindow : WindowBase
    {
        public GameObject ContentArea;
        public GameObject EntryTemplate;
        public RoSpriteRendererUI EmoteRenderer;
        private bool isInitialized;

        private List<EmoteEntry> EmoteEntries = new();

        void Start()
        {
            if (!isInitialized)
                Init();
        }

        public void EnsureInitialized()
        {
            if (!isInitialized)
                Init();
        }
        
        //
        // private EmoteEntry CreateEntry(string line)
        // {
        //     var split = line.Split(new[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
        //
        //     var entry = GameObject.Instantiate(EntryTemplate);
        //     entry.transform.SetParent(ContentArea.transform, true);
        //     
        //     //Debug.Log("Parse line: " + line);
        //     var cmd = split[5].Split(',').Select(e => "/" + e).ToList();
        //
        //     var id = int.Parse(split[0]);
        //     var frame = int.Parse(split[1]);
        //     var x = int.Parse(split[2]);
        //     var y = int.Parse(split[3]);
        //     var size = float.Parse(split[4]);
        //     var desc = string.Join(", ", cmd);
        //
        //     entry.transform.localScale = Vector3.one;
        //
        //     var emote = entry.GetComponent<EmoteEntry>();
        //     emote.SetEmote(id, frame, new Vector2(x, y), size, desc);
        //     
        //     foreach(var c in cmd)
        //         ClientCommandHandler.RegisterEmoteCommand(c, id);
        //
        //     return emote;
        // }
        
        private EmoteEntry CreateEntry(EmoteData data)
        {
            var entry = GameObject.Instantiate(EntryTemplate);
            entry.transform.SetParent(ContentArea.transform, true);
            
            //Debug.Log("Parse line: " + line);
            var cmd = data.Commands.Split('/').Select(e => "/" + e).ToList();

            // var id = int.Parse(split[0]);
            // var frame = int.Parse(split[1]);
            // var x = int.Parse(split[2]);
            // var y = int.Parse(split[3]);
            // var size = float.Parse(split[4]);
            var desc = string.Join(", ", cmd);

            entry.transform.localScale = Vector3.one;

            var emote = entry.GetComponent<EmoteEntry>();
            emote.SetEmote(data.Id, data.Sprite, data.Frame, new Vector2(data.X, data.Y), data.Size, desc);
            
            foreach(var c in cmd)
                ClientCommandHandler.RegisterEmoteCommand(c, data.Id);

            return emote;
        }

        private void OnLoadEmotesDone(UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<RoSpriteData> spriteData)
        {
            if (spriteData.Result != null)
            {
                EmoteRenderer.SpriteData = spriteData.Result;

                var emotes = ClientDataLoader.Instance.GetEmoteTable;

                foreach (var (_, emote) in emotes)
                {
                    var entry = CreateEntry(emote);
                    EmoteEntries.Add(entry);
                }
                //
                // var text = EmoteListFile.text.Split("\r\n");
                //
                // for (var i = 1; i < text.Length; i++)
                // {
                //     var line = text[i];
                //     var entry = CreateEntry(line);
                //
                //     EmoteEntries.Add(entry);
                // }

                Destroy(EntryTemplate);

                isInitialized = true;
            }
            else
            {
                Debug.LogWarning("Emote Sprite Data could not be found at: Assets/Sprites/Imported/Miscss/emotionsas.asset");
            }
        }

        void Init()
        {
            Addressables.LoadAssetAsync<RoSpriteData>("Assets/Sprites/Misc/emotion.spr").Completed += OnLoadEmotesDone;
        }

        // Update is called once per frame
        void Update()
        {
            if (!isInitialized)
                return;
        }
        
    }
}
