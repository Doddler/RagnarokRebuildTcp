using UnityEngine;
using TMPro;

public class WarpRow : MonoBehaviour
{
    public GameObject ButtonPrefab;
    public GameObject RowGroup;
    public TextMeshProUGUI Title;

    public WarpButton CreateNewEntry(string title, string mapName, int x = -999, int y = -999)
    {
        var button = GameObject.Instantiate(ButtonPrefab);
        button.transform.SetParent(RowGroup.transform);
        button.transform.localScale = Vector3.one;
        
        var obj = button.GetComponent<WarpButton>();
        obj.MapName = mapName;
        obj.X = x;
        obj.Y = y;
        var width = obj.SetTitle(title);
        var rect = button.GetComponent<RectTransform>();
        //Debug.Log(title + " : " + width);
        rect.sizeDelta = new Vector2(width + 12, 25);

        return obj;
    }

    public void FinalizeInit()
    {
        Destroy(ButtonPrefab);
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
