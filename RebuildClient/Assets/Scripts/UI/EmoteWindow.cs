using System;
using System.Collections.Generic;
using System.Linq;
using PlayerControl;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.EventSystems.EventTrigger;
using static UnityEngine.Rendering.DebugUI.Table;

namespace Assets.Scripts.UI
{
    public class EmoteWindow : WindowBase
    {
        public TextAsset EmoteListFile;
        public GameObject ContentArea;
        public GameObject EntryTemplate;
        private bool isInitialized;

        private List<EmoteEntry> EmoteEntries = new();

        void Awake()
        {
            if (!isInitialized)
                Init();
        }

        private EmoteEntry CreateEntry(string line)
        {
            var split = line.Split(new[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);

            var entry = GameObject.Instantiate(EntryTemplate);
            entry.transform.SetParent(ContentArea.transform, true);
            
            //Debug.Log("Parse line: " + line);
            var cmd = split[5].Split(',').Select(e => "/" + e).ToList();

            var id = int.Parse(split[0]);
            var frame = int.Parse(split[1]);
            var x = int.Parse(split[2]);
            var y = int.Parse(split[3]);
            var size = float.Parse(split[4]);
            var desc = string.Join(", ", cmd);

            entry.transform.localScale = Vector3.one;

            var emote = entry.GetComponent<EmoteEntry>();
            emote.SetEmote(id, frame, new Vector2(x, y), size, desc);
            
            foreach(var c in cmd)
                ClientCommandHandler.RegisterEmoteCommand(c, id);

            return emote;
        }

        void Init()
        {
            var text = EmoteListFile.text.Split("\r\n");

            for (var i = 1; i < text.Length; i++)
            {
                var line = text[i];
                var entry = CreateEntry(line);

                EmoteEntries.Add(entry);
            }

            Destroy(EntryTemplate);
            
            isInitialized = true;
        }

        // Update is called once per frame
        void Update()
        {
            if (!isInitialized)
                return;
        }
        
    }
}
