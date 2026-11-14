using Assets.Scripts;
using Assets.Scripts.Network;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.UI;
using Assets.Scripts.UI.Classic;
using Assets.Scripts.UI.Classic.DragAndDrop;
using Assets.Scripts.UI.Hud;
using RO_Flex_UI.Components.DragAndDrop;
using UnityEngine;

[RequireComponent(typeof(DropTrigger))]
public class GroundDropZone : MonoBehaviour, IDragTarget, ICanvasRaycastFilter
{
    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        // Let world clicks pass through unless a Classic drag needs a drop target.
        if (!isActiveAndEnabled) return false;
        var manager = DraggableManager.Instance;
        return manager != null && manager.isActiveAndEnabled && manager.Dragging;
    }

    public bool CanDrop(DragPayload payload)
    {
        var state = PlayerState.Instance;
        if (payload == null || state == null || !state.IsValid || NetworkManager.Instance == null)
            return false;

        var bagId = payload.source switch
        {
            Assets.Scripts.UI.Classic.DragAndDrop.DraggableItem item => item.ItemId,
            DraggableGearSlot gear => gear.ItemId,
            _ => 0
        };

        return bagId > 0 && state.Inventory.TryGetInventoryItem(bagId, out var entry)
            && entry.ItemData != null && entry.Count > 0;
    }

    public void OnDropComplete(DragPayload payload)
    {
        if (!CanDrop(payload)) return;

        if (payload.source is DraggableGearSlot gear)
        {
            NetworkManager.Instance.SendUnEquipItem(gear.ItemId);
            return;
        }

        if (ClassicEquipmentWindow.Instance != null && ClassicEquipmentWindow.Instance.isActiveAndEnabled)
        {
            CameraFollower.Instance.AppendNotice("Cannot drop items while equipment window is open.");
            return;
        }

        if (StorageUI.Instance != null)
        {
            CameraFollower.Instance.AppendNotice("Cannot drop items while storage window is open.");
            return;
        }

        var source = (Assets.Scripts.UI.Classic.DragAndDrop.DraggableItem)payload.source;
        if (!PlayerState.Instance.Inventory.TryGetInventoryItem(source.ItemId, out var item)) return;

        // The server confirms inventory removal and creates the ground item.
        if (item.Count == 1)
            NetworkManager.Instance.SendDropItem(source.ItemId, 1);
        else
            UiManager.Instance.DropCountConfirmationWindow.BeginItemDrop(item, DropConfirmationType.DropOnGround);
    }
}
