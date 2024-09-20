using System;
using System.Collections.Generic;
using Assets.Scripts;
using Assets.Scripts.Sprites;
using Assets.Scripts.UI;
using Assets.Scripts.UI.ConfigWindow;
using Assets.Scripts.UI.Hud;
using RebuildSharedData.Enum;
using UnityEngine;
using UnityEngine.Serialization;

public class UiManager : MonoBehaviour
{
    public GameObject CharacterOverlayGroup;
    public GameObject WarpManager;
    public GameObject EmoteManager;
    public ActionTextDisplay ActionTextDisplay;
    public SkillWindow SkillManager;
    public OptionsWindow ConfigManager;
    public SkillHotbar SkillHotbar;
    public HelpWindow HelpWindow;
    public DragTrashBucket TrashBucket;
    public GameObject PrimaryUserUIContainer;
    public ItemOverlay ItemOverlay;

    public ItemDragObject DragItemObject;
    
    public List<Draggable> FloatingDialogBoxes;
    public List<IClosableWindow> WindowStack = new();

    private static UiManager _instance;
    private Canvas canvas;

    [NonSerialized] public bool IsDraggingItem;
    private IItemDropTarget HoveredDropTarget;
    private bool canChangeSkillLevel;
    public bool IsCanvasVisible => canvas.enabled;

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
        
        CharacterOverlayGroup.SetActive(true);

        //kinda dumb way to make sure the windows are initialized and their contents cached
        var warp = WarpManager.GetComponent<WarpWindow>();
        warp.ShowWindow();
        warp.HideWindow();

        var emote = EmoteManager.GetComponent<EmoteWindow>();
        emote.ShowWindow();
        emote.HideWindow();
        
        //SkillManager.ShowWindow();
        SkillManager.HideWindow();
        
        ConfigManager.ShowWindow();
        ConfigManager.HideWindow();

        canvas = PrimaryUserUIContainer.GetComponent<Canvas>();
        
        ConfigManager.Initialize();
        SkillHotbar.Initialize();
        LoadWindowPositionData();
        
        HelpWindow.ShowWindow();
        
        ActionTextDisplay.EndActionTextDisplay();
    }

    public void SyncFloatingBoxPositionsWithSaveData()
    {
        var positions = GameConfig.Data.WindowPositions;
        for (var i = 0; i < positions.Length; i++)
            positions[i] = FloatingDialogBoxes[i].Target.position;
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
                positions[i] = FloatingDialogBoxes[i].Target.position;
        }

        if (positions.Length < FloatingDialogBoxes.Count)
        {
            Debug.Log($"We have fewer windows than we expected, time to expand teh array.");
            var oldSize = positions.Length;
            Array.Resize(ref positions, FloatingDialogBoxes.Count);
            for(var i = oldSize; i < positions.Length; i++)
                positions[i] = FloatingDialogBoxes[i].Target.position;
        }

        // Debug.Log($"{positions.Length} {FloatingDialogBoxes.Count}");
        
        for (var i = 0; i < FloatingDialogBoxes.Count; i++)
        {
            FloatingDialogBoxes[i].Target.position = positions[i];
        }

        GameConfig.Data.WindowPositions = positions;
    }

    public void MoveToLast(IClosableWindow entry)
    {
        WindowStack.Remove(entry);
        WindowStack.Add(entry);
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
        if (dragItem.Type == DragItemType.Skill)
        {
            canChangeSkillLevel = ClientDataLoader.Instance.GetSkillData((CharacterSkill)dragItem.ItemId).AdjustableLevel;
            if (!canChangeSkillLevel)
                DragItemObject.UpdateCount(0);
        }
    }
    
    public bool EndItemDrag()
    {
        Debug.Log("Ending item drag");
        IsDraggingItem = false;
        DragItemObject.gameObject.SetActive(false);
        TrashBucket.gameObject.SetActive(false);
        if (HoveredDropTarget != null)
        {
            HoveredDropTarget.DropItem();
            HoveredDropTarget = null;
            return true;
        }

        return false;
    }

    public void RegisterDragTarget(IItemDropTarget target)
    {
        Debug.Log($"Registering drop target");
        HoveredDropTarget = target;
    }

    public void UnregisterDragTarget(IItemDropTarget target)
    {
        Debug.Log($"Removing drop target");
        if (HoveredDropTarget == target)
            HoveredDropTarget = null;
    }

    public bool CloseLastWindow()
    {
        Debug.Log("CloseLastWindow: " + WindowStack.Count);

        if (WindowStack.Count == 0)
            return false;

        //this function is flawed, it should close the top most window rather than last created one.
        //now that we have more than 2 windows this'll become an issue.

        var close = WindowStack[^1];
        close.HideWindow();
        return true;
    }

    public void FitFloatingWindowsIntoPlayArea()
    {
        if (FloatingDialogBoxes == null)
            return;
        
        for(var i = 0; i < FloatingDialogBoxes.Count; i++)
            FloatingDialogBoxes[i].FitWindowIntoPlayArea();
    }

    public void SetEnabled(bool enabled)
    {
        var c = PrimaryUserUIContainer.GetComponent<Canvas>();
        canvas.enabled = enabled;
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F11) && canvas != null)
        {
            SetEnabled(!canvas.enabled);
        }
        
        if(Input.GetKeyDown(KeyCode.F10) || Input.GetKeyDown(KeyCode.F12))
            SkillHotbar.ToggleVisibility();
        
        if(Input.GetKeyDown(KeyCode.O) && (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)))
            ConfigManager.ToggleVisibility();
        
        if (!IsDraggingItem && !CameraFollower.Instance.InTextBox && !CameraFollower.Instance.IsInNPCInteraction)
        {
            SkillHotbar.UpdateHotkeyPresses();
        }

        if (IsDraggingItem && canChangeSkillLevel)
        {
            var oldLvl = (float)DragItemObject.ItemCount;
            if (oldLvl == 0)
                return;
            var lvl = oldLvl + Input.GetAxis("Mouse ScrollWheel") * 10f;
            lvl = Mathf.Clamp(lvl, 1, 10);
            var newLevel = Mathf.RoundToInt(lvl);
            if(newLevel != oldLvl)
                DragItemObject.UpdateCount(newLevel);
        }

    }
}
