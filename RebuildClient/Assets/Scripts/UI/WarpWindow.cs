using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Scripts.UI;
using Assets.Scripts.Utility;
using RebuildSharedData.ClientTypes;
using UnityEngine;
using UnityEngine.UI;

public class WarpWindow : WindowBase
{
    public TextAsset WarpListFile;
    public GameObject InitialRowPrefab;
    public GameObject RowContainer;

    public CanvasScaler Scaler;

    private bool isInitialized;

    struct MapWarpEntry
    {
        public string Name;
        public string Code;
        public Vector2Int Position;
    }

    public void Awake()
    {
        if (!isInitialized)
            Init();
    }

    private WarpRow CreateNewRow(string line)
    {
        //new section
        var row = GameObject.Instantiate(InitialRowPrefab);
        row.transform.SetParent(RowContainer.transform, true);
        row.transform.localScale = Vector3.one;

        var rowObj = row.GetComponent<WarpRow>();
        rowObj.Title.text = line;

        return rowObj;
    }

    public void Init()
    {
        if (isInitialized)
            return;

        var text = WarpListFile.text.Split("\r\n");
        WarpButton lastButton = null;

        var rows = 0;
        //var width = 0f;

        var warpList = new Dictionary<string, List<MapWarpEntry>>();
        var validMaps = GameObject.FindObjectOfType<SceneTransitioner>()?.GetMapEntries();

        var sectionName = "";
        var curSection = new List<MapWarpEntry>();

        foreach (var line in text)
        {
            if (string.IsNullOrWhiteSpace(line.Trim()) || line.Trim().StartsWith("//"))
                continue;
            
            if (!line.StartsWith("\t"))
            {
                if(curSection.Count > 0)
                    warpList.Add(sectionName, curSection);

                sectionName = line.Trim();
                curSection = new List<MapWarpEntry>();
            }
            else
            {
                var s = line.Trim().Split(',');

                var name = s[0].Trim();
                var code = s[1].Trim();
                
                if (validMaps != null && validMaps.All(m => m.Code.ToLower() != code.ToLower()))
                    continue;
                
                if(s.Length >= 4 && int.TryParse(s[2].Trim(), out var i1) && int.TryParse(s[3].Trim(), out var i2))
                    curSection.Add(new MapWarpEntry() {Name = name, Code = code, Position = new Vector2Int(i1, i2)} );
                else
                    curSection.Add(new MapWarpEntry() {Name = name, Code = code, Position = new Vector2Int(-999, -999)} );
            }
        }
        
        if(curSection.Count > 0)
            warpList.Add(sectionName, curSection);

        var sectionNames = warpList.Keys.ToList();
        //sectionNames.Sort();

        foreach (var s in sectionNames)
        {
            var section = warpList[s];
            var lastRow = CreateNewRow(s);
            
            rows++;
            var width = 0f;
            
            foreach (var map in section)
            {
                var button = lastRow.CreateNewEntry(map.Name, map.Code, map.Position.x, map.Position.y);
                if (width + button.GetSize().x > 440)
                {
                    Destroy(button.gameObject);
                    width = 0;
                    lastRow.FinalizeInit();
                    
                    lastRow = CreateNewRow("");
                    rows++;
                    
                    button = lastRow.CreateNewEntry(map.Name, map.Code, map.Position.x, map.Position.y);
                    
                }
                width += button.GetSize().x + 2f;
            }
            lastRow.FinalizeInit();
        }
        
        var rect = RowContainer.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(rect.sizeDelta.x, rows * 30 + 5);
        
        //
        // foreach (var line in text)
        // {
        //     if (!line.StartsWith("\t"))
        //     {
        //         var rowObj = CreateNewRow(line);
        //
        //         if(lastRow != null)
        //             lastRow.FinalizeInit();
        //         lastRow = rowObj;
        //         rows++;
        //         width = 0;
        //     }
        //     else
        //     {
        //         if (width > 396)
        //         {
        //             lastRow.FinalizeInit();
        //             var rowObj = CreateNewRow("");
        //             lastRow = rowObj;
        //             rows++;
        //             width = 0;
        //         }
        //
        //         var s = line.Trim().Split(',');
        //         
        //         if (lastRow != null && !string.IsNullOrWhiteSpace(s[0].Trim()))
        //         {
        //             var name = s[0].Trim();
        //             var map = s[1].Trim();
        //
        //             if (validMaps != null && validMaps.All(m => m.Code.ToLower() != map.ToLower()))
        //                 continue;
        //             
        //             if(s.Length >= 4 && int.TryParse(s[2].Trim(), out var i1) && int.TryParse(s[3].Trim(), out var i2))
        //                 lastButton = lastRow.CreateNewEntry(name, map, i1, i2);
        //             else
        //                 lastButton = lastRow.CreateNewEntry(name, map);
        //
        //             width += lastButton.GetSize().x;
        //         }
        //
        //         width += 2; //spacing
        //
        //     }
        // }
        //
        //
        //
        // if (lastRow != null)
        // {
        //     lastRow.FinalizeInit();
        //
        //     var rect = RowContainer.GetComponent<RectTransform>();
        //     rect.sizeDelta = new Vector2(rect.sizeDelta.x, rows * 30 + 5);
        //     
        // }

        //RowContainer.transform.localScale = new Vector3(Scaler.scaleFactor, Scaler.scaleFactor, Scaler.scaleFactor);



        Destroy(InitialRowPrefab);

        isInitialized = true;
    }

    //public void MoveToTop()
    //{
    //    UiManager.Instance.MoveToLast(this);
    //}

    //public void ShowWindow()
    //{
    //    gameObject.SetActive(true);
    //    var mgr = UiManager.Instance;
    //    if(!mgr.WindowStack.Contains(this))
    //        mgr.WindowStack.Add(this);

    //    transform.SetAsLastSibling(); //move to top

    //    Init();
    //}

    //public void HideWindow()
    //{
    //    gameObject.SetActive(false);
    //    var mgr = UiManager.Instance;
    //    if (mgr.WindowStack.Contains(this))
    //        mgr.WindowStack.Remove(this);
    //}
    
    // Update is called once per frame
    void Update()
    {
        
    }
}
