using RO_Flex_UI.Components;
using RO_Flex_UI.Panels;
using UnityEngine;

namespace Assets.Scripts.UI.Classic
{
    public class ClassicHotBar : Window
    {
        [SerializeField] private IconAmount slotPrefab;
        [SerializeField] private Transform slotContainer;
        [SerializeField] private int slotCount;

        private IconAmount[] slots;

        protected override void Awake()
        {
            base.Awake();
            slots = new IconAmount[slotCount];
            for (var i = 0; i < slotCount; i++)
            {
                var slot = Instantiate(slotPrefab, slotContainer);
                slot.SetActive(false);
                slots[i] = slot;
            }

            slotPrefab.gameObject.SetActive(false);
        }
    }
}