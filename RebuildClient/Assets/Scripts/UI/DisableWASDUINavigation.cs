using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DisableWASDNavigation : MonoBehaviour
{
    void Start()
    {
        // Loop through all selectable UI elements (buttons, toggles, etc.)
        Selectable[] selectables = FindObjectsOfType<Selectable>(true);
        foreach (Selectable sel in selectables)
        {
            // Disable automatic navigation
            Navigation nav = sel.navigation;
            nav.mode = Navigation.Mode.None;
            sel.navigation = nav;
        }
    }
}