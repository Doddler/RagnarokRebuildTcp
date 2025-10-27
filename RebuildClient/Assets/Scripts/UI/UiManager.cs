using System;
using System.Collections.Generic;
using Assets.Scripts;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.Sprites;
using Assets.Scripts.UI;
using Assets.Scripts.UI.ConfigWindow;
using Assets.Scripts.UI.Hud;
using Assets.Scripts.UI.Inventory;
using Assets.Scripts.UI.Stats;
using RebuildSharedData.Enum;
using TMPro;
using UnityEngine;

public class UiManager : MonoBehaviour
{
    public GameObject PrimaryUserUIContainer;
    public RectTransform PrimaryUserWindowContainer;
    public GameObject CharacterOverlayGroup;
    public GameObject WarpManager;
    public GameObject EmoteManager;
    public ActionTextDisplay ActionTextDisplay;
    public SkillWindow SkillManager;
    public OptionsWindow ConfigManager;
    public SkillHotbar SkillHotbar;
    public PlayerInventoryWindow InventoryWindow;
    public PlayerCartWindow CartWindow;
    public HelpWindow HelpWindow;
    public PartyPanel PartyPanel;
    public DragTrashBucket TrashBucket;
    public ToastNotificationArea ToastNotificationArea;
    public RightClickMenuWindow RightClickMenuWindow;
    public VendAndChatManager VendAndChatManager;

    public ItemOverlay ItemOverlay;
    public CharacterChat TooltipOverlay;
    public EquipmentWindow EquipmentWindow;
    public StatsWindow StatusWindow;
    public TextInputWindow TextInputWindow;
    public YesNoOptionWindow YesNoOptionsWindow;
    public DropCountConfirmationWindow DropCountConfirmationWindow;
    public ItemDescriptionWindow ItemDescriptionWindow;
    public ItemDescriptionWindow SubDescriptionWindow;
    public CardIllustrationWindow CardIllustrationWindow;

    public GameObject InventoryDropArea;
    public GameObject EquipmentDropArea;
    public GameObject GeneralItemListPrefab;
    public GameObject GenericItemListV2Prefab;
    public GameObject RefineWindowPrefab;
    public GameObject StorageWindowPrefab;
    public GameObject WarpMemoWindowPrefab;
    public GameObject NpcTradePrefab;

    private IItemDropTarget inventoryDropTarget;
    private IItemDropTarget equipmentWindowDropTarget;

    public ItemDragObject DragItemObject;
    public ItemObtainedToast ItemObtainedPopup;

    public List<Draggable> FloatingDialogBoxes;
    public List<IClosableWindow> WindowStack = new();

    public TextMeshProUGUI HelpWindowText;
    public string SpecialUiMode = "";

    private static UiManager _instance;
    private Canvas canvas;

    [NonSerialized] public bool IsDraggingItem;
    private IItemDropTarget hoveredDropTarget;
    private bool canChangeSkillLevel;
    public bool IsCanvasVisible => canvas.enabled;
    private GameObject hoveredObject;
    private CameraFollower cameraFollower;

    public static UiManager Instance
    {
        get
        {
            if (_instance != null)
                return _instance;

            _instance = GameObject.FindObjectOfType<UiManager>();
            return _instance;
        }
    }

    void Awake()
    {
        _instance = this;
    }

    public void Initialize()
    {
        CharacterOverlayGroup.SetActive(true);

        //kinda dumb way to make sure the windows are initialized and their contents cached
        var warp = WarpManager.GetComponent<WarpWindow>();
        warp.ShowWindow();
        warp.HideWindow();

        var emote = EmoteManager.GetComponent<EmoteWindow>();
        emote.ShowWindow();
        emote.EnsureInitialized();
        emote.HideWindow();

        //SkillManager.ShowWindow();
        SkillManager.HideWindow();

        ConfigManager.ShowWindow();
        ConfigManager.HideWindow();

        canvas = PrimaryUserUIContainer.GetComponent<Canvas>();

        ConfigManager.Initialize();
        SkillHotbar.Initialize();
        LoadWindowPositionData();

        HelpWindow.HideWindow();
        HelpWindowText.text = ClientDataLoader.Instance.PatchNotes + HelpWindowText.text;

        InventoryWindow.ShowWindow();
        //InventoryWindow.HideWindow();
        ItemDescriptionWindow.HideWindow();
        SubDescriptionWindow.HideWindow();
        RightClickMenuWindow.HideWindow();

        CartWindow.HideWindow();

        PartyPanel.gameObject.SetActive(false);

        ActionTextDisplay.EndActionTextDisplay();

        TooltipOverlay.gameObject.SetActive(false);
        DropCountConfirmationWindow.gameObject.SetActive(false);
        TextInputWindow.gameObject.SetActive(false);

        inventoryDropTarget = InventoryDropArea.GetComponent<InventoryDropZone>();
        equipmentWindowDropTarget = EquipmentDropArea.GetComponent<InventoryDropZone>();

        canvas.enabled = false;
        cameraFollower = CameraFollower.Instance;
    }

    public void ShowTooltip(GameObject src, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;
        var pos = Input.mousePosition;
        hoveredObject = src.gameObject;
        TooltipOverlay.gameObject.SetActive(true);
        TooltipOverlay.SetText(text);

        UpdateOverlayPosition();
    }

    public void HideTooltip(GameObject src)
    {
        //if a different tooltip took over, we don't want to hide it
        if (hoveredObject != null && hoveredObject.activeInHierarchy && src != hoveredObject)
            return;
        TooltipOverlay.gameObject.SetActive(false);
        hoveredObject = null;
    }

    public void ForceHideTooltip()
    {
        TooltipOverlay.gameObject.SetActive(false);
        hoveredObject = null;
    }

    public void RefreshTooltip()
    {
        if (hoveredObject == null || !hoveredObject.activeInHierarchy)
        {
            TooltipOverlay.gameObject.SetActive(false);
            hoveredObject = null;
            return;
        }

        var drag = hoveredObject.GetComponent<DraggableItem>();
        if (drag == null)
            return;

        if (drag.Type == DragItemType.Item && drag.ItemCount == 0)
        {
            TooltipOverlay.gameObject.SetActive(false);
            hoveredObject = null;
            return;
        }

        drag.OnPointerEnter(null);
    }

    public Vector3 GetScreenPositionOfCursor()
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas.transform as RectTransform,
            Input.mousePosition, canvas.worldCamera, out var screenPos);

        return canvas.transform.TransformPoint(screenPos);
    }

    private void UpdateOverlayPosition()
    {
        if (!TooltipOverlay.gameObject.activeInHierarchy)
            return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas.transform as RectTransform,
            Input.mousePosition, canvas.worldCamera, out var screenPos);

        var initialScreenPos = canvas.transform.TransformPoint(screenPos);
        screenPos = initialScreenPos;

        var scale = GameConfig.Data.MasterUIScale;
        var height = TooltipOverlay.RectTransform.rect.yMax * scale;
        var width = TooltipOverlay.RectTransform.rect.xMax * scale;

        screenPos.y += 10;

        if (screenPos.y + height > Screen.height)
            screenPos.y = Screen.height - height;

        if (screenPos.x + width > Screen.width)
            screenPos.x -= width;

        TooltipOverlay.transform.position = screenPos;

        // Debug.Log($"{screenPos} {initialScreenPos} {width} {height} {GameConfig.Data.MasterUIScale}");
    }

    public void OnLogIn()
    {
        InventoryWindow.UpdateActiveVisibleBag();
        InventoryWindow.HideWindow();
        EquipmentWindow.HideWindow();
        StatusWindow.HideWindow();
    }

    public void SyncFloatingBoxPositionsWithSaveData()
    {
        var positions = GameConfig.Data.WindowPositions;
        if (positions.Length != FloatingDialogBoxes.Count)
            Array.Resize(ref positions, FloatingDialogBoxes.Count);
        for (var i = 0; i < positions.Length; i++)
            positions[i] = FloatingDialogBoxes[i].Target.anchoredPosition;
        GameConfig.Data.WindowPositions = positions;
    }

    public void LoadWindowPositionData()
    {
        if (FloatingDialogBoxes == null || FloatingDialogBoxes.Count <= 0)
            return;

        var positions = GameConfig.Data.WindowPositions;

        if (positions == null)
        {
            Debug.Log($"We have no window positions saved, re-initializing.");


            positions = new Vector2[FloatingDialogBoxes.Capacity];
            for (var i = 0; i < positions.Length; i++)
                positions[i] = FloatingDialogBoxes[i].Target.anchoredPosition;
        }

        if (positions.Length < FloatingDialogBoxes.Count)
        {
            Debug.Log($"We have fewer windows than we expected, time to expand teh array.");
            var oldSize = positions.Length;
            Array.Resize(ref positions, FloatingDialogBoxes.Count);
            for (var i = oldSize; i < positions.Length; i++)
                positions[i] = FloatingDialogBoxes[i].Target.anchoredPosition;
        }

        // Debug.Log($"{positions.Length} {FloatingDialogBoxes.Count}");

        for (var i = 0; i < FloatingDialogBoxes.Count; i++)
        {
            FloatingDialogBoxes[i].Target.anchoredPosition = positions[i];
        }

        GameConfig.Data.WindowPositions = positions;
    }

    public void MoveToLast(IClosableWindow entry)
    {
        WindowStack.Remove(entry);
        WindowStack.Add(entry);
    }

    public void StartStoreItemDrag(int itemId, Sprite sprite, ItemListRole role, int count)
    {
        Debug.Log($"Starting Equipment Drag for {itemId}");
        IsDraggingItem = true;
        DragItemObject.gameObject.SetActive(true);
        DragItemObject.transform.position = Input.mousePosition;
        DragItemObject.Assign(DragItemType.Equipment, sprite, itemId, count);
        DragItemObject.Origin = ItemDragOrigin.ShopWindow;
        DragItemObject.OriginId = (int)role;
        DragItemObject.UpdateCount(count);
        TrashBucket.gameObject.SetActive(true);
        canChangeSkillLevel = false;
        ShopUI.Instance.OnStartDrag(role);
    }

    public void StartEquipmentDrag(InventoryItem item, Sprite sprite)
    {
        Debug.Log($"Starting Equipment Drag for {item}");
        IsDraggingItem = true;
        DragItemObject.gameObject.SetActive(true);
        DragItemObject.transform.position = Input.mousePosition;
        DragItemObject.Assign(DragItemType.Equipment, sprite, item.ItemData.Id, 0);
        DragItemObject.Origin = ItemDragOrigin.EquipmentWindow;
        DragItemObject.OriginId = item.BagSlotId;
        TrashBucket.gameObject.SetActive(true);
        canChangeSkillLevel = false;
        InventoryDropArea.SetActive(true);
    }

    public void StartItemDrag(DragItemBase dragItem)
    {
        Debug.Log($"Starting Item Drag from {dragItem}");
        IsDraggingItem = true;
        DragItemObject.gameObject.SetActive(true);
        DragItemObject.transform.position = Input.mousePosition;
        DragItemObject.Assign(dragItem);
        DragItemObject.Origin = ItemDragOrigin.None;
        TrashBucket.gameObject.SetActive(true);
        canChangeSkillLevel = false;
        switch (dragItem.Type)
        {
            case DragItemType.Skill:
            {
                canChangeSkillLevel = ClientDataLoader.Instance.GetSkillData((CharacterSkill)dragItem.ItemId).AdjustableLevel;
                if (!canChangeSkillLevel)
                    DragItemObject.UpdateCount(0);
                break;
            }
            case DragItemType.Item:
            {
                var itemData = ClientDataLoader.Instance.GetItemById(dragItem.ItemId);
                if (itemData.IsUnique || itemData.ItemClass == ItemClass.Ammo)
                    EquipmentDropArea.SetActive(true);
                if (StorageUI.Instance != null)
                    StorageUI.Instance.UpdateDropArea(true);
                if (PlayerState.Instance.HasCart)
                    CartWindow.UpdateDropArea(true);
                break;
            }
            case DragItemType.StorageItem:
            {
                InventoryDropArea.SetActive(true);
                if (PlayerState.Instance.HasCart)
                    CartWindow.UpdateDropArea(true);
                break;
            }
            case DragItemType.CartItem:
            {
                InventoryDropArea.SetActive(true);
                if (StorageUI.Instance != null)
                    StorageUI.Instance.UpdateDropArea(true);
                break;
            }
            case DragItemType.VendSetupSource:
            case DragItemType.VendSetupTarget:
                VendingSetupManager.Instance.StartDrag(dragItem.Type);
                break;
            case DragItemType.VendShop:
            case DragItemType.VendPurchase:
                VendingShopViewUI.ActiveTradeWindow.OnStartDrag(dragItem.Type);
                break;
        }
    }

    public bool EndItemDrag(bool allowDrop = true)
    {
        Debug.Log("Ending item drag");
        IsDraggingItem = false;
        DragItemObject.gameObject.SetActive(false);
        TrashBucket.gameObject.SetActive(false);
        inventoryDropTarget.DisableDropArea();
        equipmentWindowDropTarget.DisableDropArea();
        InventoryDropArea.SetActive(false);
        EquipmentDropArea.SetActive(false);
        CartWindow.UpdateDropArea(false);
        if (StorageUI.Instance != null)
            StorageUI.Instance.UpdateDropArea(false);
        VendingSetupManager.Instance?.HideDropArea();
        VendingShopViewUI.ActiveTradeWindow?.OnStopDrag();;

        if (hoveredDropTarget != null)
        {
            if (allowDrop)
                hoveredDropTarget.DropItem();
            hoveredDropTarget = null;
            return true;
        }

        return false;
    }

    public void RegisterDragTarget(IItemDropTarget target)
    {
        // Debug.Log($"Registering drop target");
        hoveredDropTarget = target;
    }

    public void UnregisterDragTarget(IItemDropTarget target)
    {
        // Debug.Log($"Removing drop target");
        if (hoveredDropTarget == target)
            hoveredDropTarget = null;
    }

    public bool CloseLastWindow()
    {
        Debug.Log("CloseLastWindow: " + WindowStack.Count);

        if (WindowStack.Count == 0)
            return false;

        //this function is flawed, it should close the top most window rather than last created one.
        //now that we have more than 2 windows this'll become an issue.

        var close = WindowStack[^1];
        if (!close.CanCloseWindow())
            return false;
        close.CloseWindow();
        return true;
    }

    public void FitFloatingWindowsIntoPlayArea()
    {
        if (FloatingDialogBoxes == null)
            return;

        for (var i = 0; i < FloatingDialogBoxes.Count; i++)
            FloatingDialogBoxes[i].FitWindowIntoPlayArea();
    }

    public void SetEnabled(bool enabled)
    {
        // var c = PrimaryUserUIContainer.GetComponent<Canvas>();
        canvas.enabled = enabled;
    }

    // Update is called once per frame
    void Update()
    {
        if (!NetworkManager.IsLoaded || cameraFollower == null) return;
        if (Input.GetKeyDown(KeyCode.F11) && canvas != null)
        {
            SetEnabled(!canvas.enabled);
        }

        if (Input.GetKeyDown(KeyCode.F10) || Input.GetKeyDown(KeyCode.F12))
            SkillHotbar.ToggleVisibility();

        // if(Input.GetKeyDown(KeyCode.O) && (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)))
        //     ConfigManager.ToggleVisibility();

        if (!IsDraggingItem && !cameraFollower.InTextBox && !cameraFollower.InItemInputBox && !cameraFollower.IsInNPCInteraction)
        {
            SkillHotbar.UpdateHotkeyPresses();
        }

        if (IsDraggingItem || cameraFollower.InTextBox)
            TooltipOverlay.gameObject.SetActive(false);

        UpdateOverlayPosition();

        if (IsDraggingItem && canChangeSkillLevel)
        {
            var oldLvl = (float)DragItemObject.ItemCount;
            if (oldLvl == 0)
                return;
            var lvl = oldLvl + Input.GetAxis("Mouse ScrollWheel") * 10f;
            lvl = Mathf.Clamp(lvl, 1, 10);
            var newLevel = Mathf.RoundToInt(lvl);
            if (newLevel != oldLvl)
                DragItemObject.UpdateCount(newLevel);
        }
    }
}