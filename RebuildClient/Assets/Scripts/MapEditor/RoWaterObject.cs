using UnityEngine;
using UnityEngine.Rendering;

public class RoWaterObject : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
	    var mr = gameObject.GetComponent<MeshRenderer>();
	    var mat = mr.material;
	    mat.renderQueue = 3005;
	    var sg = gameObject.AddComponent<SortingGroup>();
	    sg.sortingOrder = 20005;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
