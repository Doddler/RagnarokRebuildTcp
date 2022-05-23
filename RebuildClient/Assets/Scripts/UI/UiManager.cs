using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.UI;
using UnityEngine;

public class UiManager : MonoBehaviour
{
    public GameObject WarpManager;

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
        var warp = WarpManager.GetComponent<WarpWindow>();
        warp.ShowWindow();
        warp.HideWindow();
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
