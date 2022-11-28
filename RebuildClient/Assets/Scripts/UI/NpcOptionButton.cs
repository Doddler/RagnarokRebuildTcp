using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.Network;
using Assets.Scripts.UI;
using TMPro;
using UnityEngine;

public class NpcOptionButton : MonoBehaviour
{
    public TextMeshProUGUI TextBox;
    public int Id;

    public NpcOptionWindow Parent;


    public void OnClick()
    {
        Parent.gameObject.SetActive(false);
        NetworkManager.Instance.SendNpcSelectOption(Id);
    }
}
