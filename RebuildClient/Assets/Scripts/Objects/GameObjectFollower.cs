using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameObjectFollower : MonoBehaviour
{
    public GameObject Target;
    public Vector3 Offset;
    public bool DestroyOnTargetDead;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if(Target != null)
            transform.position = Target.transform.position + Offset;
        else
        {
            if(DestroyOnTargetDead && gameObject == null)
                Destroy(gameObject);
        }
    }
}
