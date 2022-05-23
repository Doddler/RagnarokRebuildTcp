using System.Collections;
using System.Collections.Generic;
using System.IO;
using Assets.Scripts.UI;
using UnityEngine;
using UnityEngine.UI;

public class WarpWindow : WindowBase
{
    public TextAsset WarpListFile;
    public GameObject InitialRowPrefab;
    public GameObject RowContainer;

    public CanvasScaler Scaler;

    private bool isInitialized;

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
        WarpRow lastRow = null;

        var rows = 0;
        var width = 0f;

        foreach (var line in text)
        {
            if (!line.StartsWith("\t"))
            {
                var rowObj = CreateNewRow(line);

                if(lastRow != null)
                    lastRow.FinalizeInit();
                lastRow = rowObj;
                rows++;
                width = 0;
            }
            else
            {
                if (width > 400)
                {
                    lastRow.FinalizeInit();
                    var rowObj = CreateNewRow("");
                    lastRow = rowObj;
                    rows++;
                    width = 0;
                }

                var s = line.Trim().Split(',');
                
                if (lastRow != null && !string.IsNullOrWhiteSpace(s[0].Trim()))
                {
                    if(s.Length >= 4 && int.TryParse(s[2].Trim(), out var i1) && int.TryParse(s[3].Trim(), out var i2))
                        width += lastRow.CreateNewEntry(s[0].Trim(), s[1].Trim(), i1, i2);
                    else
                        width += lastRow.CreateNewEntry(s[0].Trim(), s[1].Trim());
                }

                width += 2; //spacing

            }
        }

        

        if (lastRow != null)
        {
            lastRow.FinalizeInit();

            var rect = RowContainer.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, rows * 30 + 5);
            
        }

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
