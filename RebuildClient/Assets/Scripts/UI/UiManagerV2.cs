using Assets.Scripts.UI.ConfigWindow;
using Assets.Scripts.PlayerControl;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.UI.Classic
{
    public enum WindowID
    {
        MAIN_MENU,
        PLAYER_INFO,
        INVENTORY,
        EQUIPMENT,
        SKILLS,
        HOTBAR,
        OPTIONS,
        STATS,
        EMOTES,
        HELP,
        CLIENT_DATABASE,
        CART,
        WARP,
    }

    public class WindowEntry
    {
        public WindowID id;
        public IStyledWindow windowModern;
        public IStyledWindow windowClassic;
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
        private bool initialized;
        private UiStyle? appliedStyle;

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

        private IEnumerator Start()
        {
            GameConfig.OnGameConfigChanged += RefreshWindows;
            // Let window Start methods establish their initial visibility first.
            yield return null;
            initialized = true;
            RefreshWindows();
        }

        public void RegisterWindow(WindowID windowId, IStyledWindow window, UiStyle style, KeyCode trigger = KeyCode.None)
        {
            if (!windows.TryGetValue(windowId, out var entry))
            {
                entry = new WindowEntry { id = windowId };
                windows.Add(windowId, entry);
            }

            if (entry.trigger == KeyCode.None)
                entry.trigger = trigger;
            else if (trigger != KeyCode.None && entry.trigger != trigger)
                Debug.LogWarning($"Conflicting shortcut for {windowId}: keeping {entry.trigger}, ignoring {trigger}.");

            var existing = style == UiStyle.Classic ? entry.windowClassic : entry.windowModern;
            if (existing != null && !ReferenceEquals(existing, window))
                Debug.LogWarning($"Overriding {style} window entry with ID: {windowId}");

            if (style == UiStyle.Classic)
                entry.windowClassic = window;
            else
                entry.windowModern = window;
        }

        // Resolve the configured style, falling back to the other registered variant.
        public IStyledWindow Get(WindowID windowId)
        {
            windows.TryGetValue(windowId, out var entry);
            var style = GameConfig.Data?.UiStyle ?? UiStyle.Modern;
            var window = style == UiStyle.Classic
                ? entry?.windowClassic ?? entry?.windowModern
                : entry?.windowModern ?? entry?.windowClassic;

            if (window == null)
                Debug.LogWarning($"Invalid window entry with id: {windowId}");

            return window;
        }

        // Exact lookup: callers that manage both variants must not receive a fallback.
        public IStyledWindow Get(WindowID windowId, UiStyle style)
        {
            windows.TryGetValue(windowId, out var entry);
            var window = style == UiStyle.Classic ? entry?.windowClassic : entry?.windowModern;
            if (window == null)
                Debug.LogWarning($"Missing {style} window entry with id: {windowId}");

            return window;
        }

        private void Update()
        {
            if (HandleEscape()) return;

            var camera = CameraFollower.Instance;
            var selectedObject = EventSystem.current?.currentSelectedGameObject;
            if ((camera != null && (camera.InTextBox || camera.InItemInputBox || camera.InTextInputBox))
                || (selectedObject != null && selectedObject.GetComponent<TMP_InputField>() != null))
                return;

            foreach (var entry in windows.Values)
            {
                if (entry.trigger == KeyCode.None || !Input.GetKeyDown(entry.trigger)) continue;

                if (GameConfig.Data != null && GameConfig.Data.EnableWASDControls
                    && (entry.id == WindowID.STATS || entry.id == WindowID.EMOTES
                        || entry.id == WindowID.CART || entry.id == WindowID.SKILLS))
                    continue;
                if (entry.id == WindowID.CART && !PlayerState.Instance.HasCart)
                    continue;

                var activeWindow = Get(entry.id);
                activeWindow?.ToggleVisibility();
                return;
            }
        }

        private static bool IsAlive(IStyledWindow window)
        {
            return window != null && (!(window is Object obj) || obj != null);
        }

        private void RefreshWindows()
        {
            if (!initialized || GameConfig.Data == null) return;
            var style = GameConfig.Data.UiStyle;
            if (appliedStyle == style) return;

            var previousOrder = new List<IStyledWindow>(openWindows);
            var replacements = new Dictionary<IStyledWindow, IStyledWindow>();
            var swaps = new List<(IStyledWindow selected, IStyledWindow other, bool visible, bool persistent)>();

            // Snapshot everything before visibility callbacks can change tracking.
            foreach (var entry in new List<WindowEntry>(windows.Values))
            {
                var classic = IsAlive(entry.windowClassic) ? entry.windowClassic : null;
                var modern = IsAlive(entry.windowModern) ? entry.windowModern : null;
                var selected = style == UiStyle.Classic ? classic ?? modern : modern ?? classic;
                var other = ReferenceEquals(selected, classic) ? modern : classic;
                var persistent = entry.id == WindowID.PLAYER_INFO;
                var visible = persistent || (classic?.IsVisible ?? false) || (modern?.IsVisible ?? false);
                swaps.Add((selected, other, visible, persistent));
                if (classic != null) replacements[classic] = persistent ? null : selected;
                if (modern != null) replacements[modern] = persistent ? null : selected;
            }

            appliedStyle = style;
            // Hide outgoing variants first, then show replacements in open-window order.
            foreach (var swap in swaps)
            {
                if (IsAlive(swap.other) && !ReferenceEquals(swap.other, swap.selected) && swap.other.IsVisible)
                    swap.other.HideWindow();
                if (swap.persistent && IsAlive(swap.selected) && !swap.selected.IsVisible)
                    swap.selected.ShowWindow();
            }

            var orderedWindows = new List<IStyledWindow>();
            foreach (var window in previousOrder)
            {
                var replacement = replacements.TryGetValue(window, out var mapped) ? mapped : window;
                if (!IsAlive(replacement)) continue;
                // Only restore entries that were visible in the snapshot.
                var wasVisible = swaps.Exists(swap => ReferenceEquals(swap.selected, replacement) && swap.visible);
                if (!wasVisible && !replacement.IsVisible) continue;
                orderedWindows.Remove(replacement);
                orderedWindows.Add(replacement);
            }
            // Include visible windows that predate tracking (for example at startup).
            foreach (var swap in swaps)
                if (!swap.persistent && swap.visible && IsAlive(swap.selected) && !orderedWindows.Contains(swap.selected))
                    orderedWindows.Add(swap.selected);

            foreach (var window in orderedWindows)
            {
                if (!window.IsVisible) window.ShowWindow();
                if (window is Component component)
                    component.transform.SetAsLastSibling();
            }

            // Callbacks may reorder tracking. Restore prior order with the new variants.
            var callbackOrder = new List<IStyledWindow>(openWindows);
            openWindows.Clear();
            foreach (var window in previousOrder)
            {
                var replacement = replacements.TryGetValue(window, out var mapped) ? mapped : window;
                if (!IsAlive(replacement) || !replacement.IsVisible) continue;
                openWindows.Remove(replacement);
                openWindows.Add(replacement);
            }
            foreach (var window in orderedWindows)
                if (IsAlive(window) && window.IsVisible && !openWindows.Contains(window))
                    openWindows.Add(window);
            foreach (var window in callbackOrder)
            {
                if (replacements.TryGetValue(window, out var mapped) && !ReferenceEquals(mapped, window))
                    continue;
                if (IsAlive(window) && window.IsVisible && !openWindows.Contains(window))
                    openWindows.Add(window);
            }
        }
    }
}
