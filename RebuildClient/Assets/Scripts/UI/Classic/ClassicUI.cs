using UnityEngine;

public class ClassicUI : MonoBehaviour
{
    public GameObject Canvas;

    public void Start()
    {
        // Enable the canvas if it is assigned
        if (Canvas != null)
            Canvas.SetActive(true);
    }
}
