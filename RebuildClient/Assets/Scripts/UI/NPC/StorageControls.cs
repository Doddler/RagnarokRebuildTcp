using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI
{
    public class StorageControls : MonoBehaviour
    {
        public List<Button> StorageTabs;

        public void ClickStorageButton(int type)
        {
            Debug.Log($"Click storage button {(StorageSectionType)type}");
            StorageUI.Instance.ChangeStorageTab((StorageSectionType)type);
            for (var i = 0; i < StorageTabs.Count; i++)
            {
                StorageTabs[i].interactable = i != type;
                StorageTabs[i].transform.localPosition = new Vector3(i != type ? -24 : -30, StorageTabs[i].transform.localPosition.y, 0);
                StorageTabs[i].transform.SetAsLastSibling();
            }
            StorageTabs[type].transform.SetAsLastSibling();
        }
    }
}