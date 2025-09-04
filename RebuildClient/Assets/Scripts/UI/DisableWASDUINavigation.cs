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
            // Skip scrollbars if you still want arrow keys to move them
            if (sel is Scrollbar)
                continue;

            // Disable automatic navigation
            Navigation nav = sel.navigation;
            nav.mode = Navigation.Mode.None;
            sel.navigation = nav;
        }
    }

    void Update()
    {
        /*
        // Block WASD and Arrow keys from selecting UI
        if (EventSystem.current != null)
        {
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A) ||
                Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D) ||
                Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow) ||
                Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow))
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }*/
    }
}