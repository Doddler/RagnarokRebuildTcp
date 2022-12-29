using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.UI;
using UnityEngine;

public class UiManager : MonoBehaviour
{
    public GameObject WarpManager;
    public GameObject EmoteManager;

    public List<IClosableWindow> WindowStack = new List<IClosableWindow>();

    private static UiManager _instance;

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

        //kinda dumb way to make sure the windows are initialized and their contents cached
        var warp = WarpManager.GetComponent<WarpWindow>();
        warp.ShowWindow();
        warp.HideWindow();

        var emote = EmoteManager.GetComponent<EmoteWindow>();
        emote.ShowWindow();
        emote.HideWindow();
    }

    public void MoveToLast(IClosableWindow entry)
    {
        WindowStack.Remove(entry);
        WindowStack.Add(entry);
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

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
