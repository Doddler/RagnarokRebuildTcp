using Assets.Scripts.UI.ConfigWindow;
using System.Collections.Generic;
using UnityEngine;
using Utility;

namespace Assets.Scripts.UI.Classic
{
    public enum WindowID
    {
        MAIN_MENU,
        PLAYER_INFO,
        PLAYER_INFO_MODERN, //TODO just for swap test
        INVENTORY,
        EQUIPMENT,
    }

    public class WindowEntry
    {
        public WindowID id;
        public IStyledWindow window;
        public KeyCode trigger;
    }

    public class UiManagerV2 : MonoBehaviorSingleton<UiManagerV2>
    {
        private readonly Dictionary<WindowID, WindowEntry> windows = new();
        private readonly List<WindowID> openWindows = new();

        private void Start()
        {
            Debug.Log("[UI MANAGER] Initialize.");
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

            return entry.window;
        }

        private void Push(WindowID windowId)
        {
            openWindows.Add(windowId);
        }

        private IStyledWindow Pop()
        {
            var last = openWindows.Count - 1;
            var windowId = openWindows[last];
            openWindows.RemoveAt(openWindows.Count - 1);

            return Get(windowId);
        }

        private IStyledWindow Remove(WindowID windowId)
        {
            openWindows.Remove(windowId);

            return Get(windowId);
        }

        private void Update()
        {
            // Priority is to close a window first
            if (Input.GetKeyDown(KeyCode.Escape) && openWindows.Count > 0)
            {
                Pop().HideWindow();
                Debug.Log($"STACK {openWindows.Count}");
                return;
            }

            //TODO handle modals that can have multiple copies
            foreach (var window in windows.Values)
            {
                if (Input.GetKeyDown(window.trigger))
                {
                    Get(window.id).ToggleVisibility();

                    if (openWindows.Contains(window.id))
                        Remove(window.id);
                    else
                        Push(window.id);

                    Debug.Log($"STACK {openWindows.Count}");
                }
            }
        }

        private void RefreshWindows()
        {
            if (GameConfig.Data == null) return;

            var style = GameConfig.Data.UiStyle;

            var classic = Get(WindowID.PLAYER_INFO);
            var modern = Get(WindowID.PLAYER_INFO_MODERN);

            classic.HideWindow();
            modern.HideWindow();

            if (style == UiStyle.Modern)
                modern.ShowWindow();
            else
                classic.ShowWindow();
        }
    }
}