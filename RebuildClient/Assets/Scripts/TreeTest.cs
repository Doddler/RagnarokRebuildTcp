using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TreeTest : MonoBehaviour
{
    public MeshRenderer MeshRenderer;
    public Material Material;

    // Start is called before the first frame update
    void Start()
    {
        MeshRenderer = gameObject.GetComponent<MeshRenderer>();
        Material = MeshRenderer.material;

        Debug.Log(Material.shader);
        Debug.Log(Material.IsKeywordEnabled("_ALPHATEST_ON"));
    }

    private float time = 3f;

    // Update is called once per frame
    void Update()
    {
        time -= Time.deltaTime;

        if (time > 0)
            return;

        time += 3f;

        transform.localPosition += Vector3.right * 0.1f;

        Debug.Log(Material.shader);
        Debug.Log(Material.IsKeywordEnabled("_ALPHATEST_ON"));
    }
}
