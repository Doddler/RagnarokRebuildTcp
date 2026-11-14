using System;
using System.Collections.Generic;
using Assets.Scripts.UI.Classic;
using Assets.Scripts.UI.ConfigWindow;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.Sprites;
using Assets.Scripts.Utility;
using RO_Flex_UI.Components;
using RO_Flex_UI.Panels;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ClassicItemDescriptionWindow : Window, IStyledWindow
{
    private static readonly Dictionary<int, ClassicItemDescriptionWindow> descriptions = new();

    public static void ToggleItemDescription(int bagSlotId)
    {
        if (descriptions.TryGetValue(bagSlotId, out var existing) && existing != null)
        {
            existing.HideWindow();
            return;
        }
        var manager = UiManagerV2.Instance;
        var state = PlayerState.Instance;
        if (manager == null || state == null || state.Inventory == null || !state.Inventory.TryGetInventoryItem(bagSlotId, out var item)) return;
        var prefab = manager.ItemDescriptionPrefab;
        if (prefab == null)
        {
            Debug.LogError("Assign Item Description Prefab on UiManagerV2.", manager);
            return;
        }
        if (manager.Get(WindowID.INVENTORY) is not Component inventoryWindow) return;
        var window = Instantiate(prefab, inventoryWindow.transform.parent);
        descriptions[bagSlotId] = window;
        window.Display(item, () => descriptions.Remove(bagSlotId));
        window.ShowWindow();
        window.CenterWindow();
        ((RectTransform)window.transform).anchoredPosition += new Vector2(20, -20) * (descriptions.Count - 1);
        window.FitWindowIntoPlayArea();
    }

    public override void ShowWindow()
    {
        base.ShowWindow();
        UiManagerV2.Instance?.TrackOpen(this);
    }

    private void RefreshStyle()
    {
        if (GameConfig.Data != null && GameConfig.Data.UiStyle == UiStyle.Modern) HideWindow();
    }

    private TMP_Text description;
    private Image portrait;
    private ItemLine nameLine;
    private Header header;
    private Action closed;
    private bool closing;
    private int request;
    private ClientInventory inventory;
    private int bagSlotId;
    private bool initialized;

    protected override void Awake()
    {
        base.Awake();
        InitializeReferences();
    }

    // Inactive prefab instances can receive Display before Unity invokes Awake.
    private void InitializeReferences()
    {
        if (initialized) return;
        initialized = true;
        GameConfig.OnGameConfigChanged += RefreshStyle;
        header = GetComponentInChildren<Header>(true);
        if (header != null) header.OnCloseButtonClick.AddListener(HideWindow);
        foreach (var text in GetComponentsInChildren<TMP_Text>(true))
            if (text.text == "Item move restrictions") text.gameObject.SetActive(false);
        // These are the named components of the ClassicUI description prefab.
        foreach (var line in GetComponentsInChildren<ItemLine>(true))
        {
            if (line.name == "Cards") line.gameObject.SetActive(false);
            else nameLine = line;
        }
        var scroll = GetComponentInChildren<ScrollRect>(true);
        if (scroll != null && scroll.content != null)
            description = scroll.content.GetComponentInChildren<TMP_Text>(true);
        foreach (var img in GetComponentsInChildren<Image>(true))
            if (img.name == "Item Splash") portrait = img;
        foreach (var button in GetComponentsInChildren<RoButton>(true))
            if (button.GetComponentInChildren<TMP_Text>(true)?.text == "Preview")
                button.gameObject.SetActive(false);
    }

    public void Display(InventoryItem item, Action onClose)
    {
        InitializeReferences();
        closed = onClose;
        if (inventory == null)
        {
            bagSlotId = item.BagSlotId;
            inventory = PlayerState.Instance.Inventory;
            inventory.OnChanged += RefreshItem;
        }
        if (header != null) header.Text = item.ProperName();
        if (nameLine != null) nameLine.Text = item.ProperName();
        if (description != null) description.text = ClientDataLoader.Instance.GetItemDescription(item.ItemData.Code);
        var fallback = UiManager.Instance != null && UiManager.Instance.ItemDescriptionWindow != null
            ? UiManager.Instance.ItemDescriptionWindow.DefaultItemPortrait : null;
        if (portrait != null) portrait.sprite = fallback;
        var version = ++request;
        var path = $"Assets/Sprites/Imported/Collections/{item.ItemData.Sprite}.png";
        if (ClientDataLoader.DoesAddressableExist<Sprite>(path))
            AddressableUtility.LoadSprite(gameObject, path, sprite =>
            {
                if (this != null && !closing && version == request && portrait != null)
                    portrait.sprite = sprite != null ? sprite : fallback;
            });
    }

    private void RefreshItem()
    {
        if (closing) return;
        if (!inventory.TryGetInventoryItem(bagSlotId, out var item)) HideWindow();
        else Display(item, closed);
    }

    private void OnDestroy()
    {
        GameConfig.OnGameConfigChanged -= RefreshStyle;
        UiManagerV2.Instance?.TrackClosed(this);
        if (inventory != null) inventory.OnChanged -= RefreshItem;
        if (!closing) closed?.Invoke();
    }

    public override void HideWindow()
    {
        if (closing) return;
        closing = true;
        UiManagerV2.Instance?.TrackClosed(this);
        closed?.Invoke();
        gameObject.SetActive(false);
        Destroy(gameObject);
    }
}
