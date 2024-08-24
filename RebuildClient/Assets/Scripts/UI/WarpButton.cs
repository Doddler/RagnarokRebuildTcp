using Assets.Scripts.Network;
using TMPro;
using UnityEngine;

public class WarpButton : MonoBehaviour
{
    // Start is called before the first frame update
    public string MapName;
    public TextMeshProUGUI ButtonText;
    public int X = -999;
    public int Y = -999;

    public float SetTitle(string title)
    {
        ButtonText.text = title;
        ButtonText.ForceMeshUpdate();
        
        return ButtonText.mesh.bounds.size.x;
    }

    public void OnClick()
    {
        if (!string.IsNullOrWhiteSpace(MapName))
        {
            Debug.Log("Click button: " + MapName);
            NetworkManager.Instance.SendMoveRequest(MapName, X, Y);
            UiManager.Instance.WarpManager.GetComponent<WarpWindow>().HideWindow();
        }
    }

    public Vector2 GetSize()
    {
        var rect = GetComponent<RectTransform>();
        //Debug.Log(title + " : " + width);
        return rect.sizeDelta;
    }

    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
