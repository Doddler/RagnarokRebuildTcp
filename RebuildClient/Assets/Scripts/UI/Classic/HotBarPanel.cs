using RO_Flex_UI.Components;
using UnityEngine;

namespace Assets.Scripts.UI.Classic
{
    public class HotBarPanel : MonoBehaviour
    {
        [SerializeField] private RoText lineLabel;
        public IconAmount[] Slots { get; private set; }

        private void Awake()
        {
            if (lineLabel == null)
            {
                Debug.LogError("Line label is not assigned.", this);
            }
        }

        public void Initialize(int slotIndex, int slotCount, IconAmount slotPrefab, Transform slotContainer)
        {
            lineLabel.SetText("{0}", slotIndex);
            Slots = new IconAmount[slotCount];
            for (var i = 0; i < slotCount; i++)
            {
                var slot = Instantiate(slotPrefab, gameObject.transform);
                slot.gameObject.SetActive(true);
                Slots[i] = slot;
                Debug.Log($"Slot {slotCount - i - 1} initialized.", this);
            }
        }
    }
}