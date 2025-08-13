using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.UI.Hud
{
    public class VendAndChatManager : MonoBehaviour
    {
        public VendTitleBox VendTemplate;
        private Dictionary<int, VendTitleBox> vendingBoxes = new();
        private int selfVendId;

        public bool TryRemovingDialogNpc(int id)
        {
            // if (selfVendId == id)
            // {
            //     selfVendId = -1;
            //     return true;
            // }
            //
            if (!vendingBoxes.Remove(id, out var vend)) 
                return false;
            
            Destroy(vend.gameObject);
            if(vendingBoxes.Count <= 0)
                gameObject.SetActive(false);

            return true;
        }

        public void CreateVendDialog(int npcId, int characterId, GameObject followObject, string title)
        {
            // if (characterId == PlayerState.Instance.EntityId)
            // {
            //     selfVendId = npcId;
            //     return; //we don't show our own vend box
            // }

            gameObject.SetActive(true);
            
            var go = GameObject.Instantiate(VendTemplate.gameObject, transform);
            go.SetActive(true);
            var box = go.GetComponent<VendTitleBox>();
            box.Text.text = title;
            box.FollowObject = followObject;
            box.VendOwnerId = npcId;
            box.SnapDialog();
            
            vendingBoxes.Add(npcId, box);
        }

        public void RemoveAllDialogNpcs()
        {
            foreach(var (_, vend) in vendingBoxes)
                Destroy(vend.gameObject);
            
            vendingBoxes.Clear();
            selfVendId = -1;
            gameObject.SetActive(false);
        }
        
    }
}