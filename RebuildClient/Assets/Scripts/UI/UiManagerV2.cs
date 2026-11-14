using Assets.Scripts.UI.ConfigWindow;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.UI.Classic
{
    public enum WindowID
    {
        MAIN_MENU,
        PLAYER_INFO,
        PLAYER_INFO_MODERN, //TODO just for swap test
        INVENTORY,
        EQUIPMENT,
        SKILLS,
    }

    public class WindowEntry
    {
        public WindowID id;
        public IStyledWindow window;
        public KeyCode trigger;
    }

    public class UiManagerV2 : MonoBehaviour
    {
        private static UiManagerV2 instance;
        public static UiManagerV2 Instance
        {
            get
            {
                if (instance == null)
                    instance = FindFirstObjectByType<UiManagerV2>(FindObjectsInactive.Include);
                return instance;
            }
        }

        [Tooltip("Prefab cloned when an item description is opened.")]
        [SerializeField] private ClassicItemDescriptionWindow itemDescriptionPrefab;
        private readonly Dictionary<WindowID, WindowEntry> windows = new();
        public ClassicItemDescriptionWindow ItemDescriptionPrefab => itemDescriptionPrefab;
        private readonly List<IStyledWindow> openWindows = new();
        private static int escapeFrame = -1;

        public void TrackOpen(IStyledWindow window)
        {
            openWindows.Remove(window);
            openWindows.Add(window);
        }

        public void TrackClosed(IStyledWindow window) => openWindows.Remove(window);

        public bool HandleEscape()
        {
            if (escapeFrame == Time.frameCount) return true;
            if (!Input.GetKeyDown(KeyCode.Escape)) return false;
            while (openWindows.Count > 0)
            {
                var last = openWindows.Count - 1;
                var window = openWindows[last];
                openWindows.RemoveAt(last);
                if (window is Component component && (component == null || !component.gameObject.activeInHierarchy)) continue;
                escapeFrame = Time.frameCount;
                window.HideWindow();
                return true;
            }
            return false;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
            GameConfig.OnGameConfigChanged -= RefreshWindows;
            openWindows.Clear();
        }

        private void Start()
        {
            GameConfig.OnGameConfigChanged += RefreshWindows;
            RefreshWindows();
        }

        public void RegisterWindow(WindowID windowId, IStyledWindow window, KeyCode trigger = KeyCode.None)
        {
            if (!windows.ContainsKey(windowId))
            {
                windows[windowId] = new WindowEntry
                {
                    id = windowId,
                    window = null,
                    trigger = trigger,
                };
            }
            else
                Debug.LogWarning($"Overriding Window Entry with ID: {windowId}");

            windows[windowId].window = window;
        }

        public IStyledWindow Get(WindowID windowId)
        {
            windows.TryGetValue(windowId, out var entry);
            if (entry == null || entry.window == null)
                Debug.LogWarning($"Invalid window entry with id: {windowId}");

            return entry?.window;
        }

        private void Update()
        {
            if (HandleEscape()) return;

            foreach (var entry in windows.Values)
            {
                if (entry.trigger == KeyCode.None || !Input.GetKeyDown(entry.trigger)) continue;
                entry.window.ToggleVisibility();
                return;
            }
        }

        private void RefreshWindows()
        {
            if (GameConfig.Data == null) return;

            var style = GameConfig.Data.UiStyle;

            var classic = Get(WindowID.PLAYER_INFO);
            var modern = Get(WindowID.PLAYER_INFO_MODERN);

            classic?.HideWindow();
            modern?.HideWindow();

            if (style == UiStyle.Modern)
                modern?.ShowWindow();
            else
                classic?.ShowWindow();


        }
    }
}
