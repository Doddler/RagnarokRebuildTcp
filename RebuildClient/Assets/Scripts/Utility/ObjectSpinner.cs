using UnityEngine;

public class ObjectSpinner : MonoBehaviour
{
    public GameObject target;
    public float Distance = 5f;
    public float Speed = 1f;
    public float Rotation = 180f;
    
    // Start is called before the first frame update
    void Start()
    {
        if(Distance > 0)
            LeanTween.moveLocalX(target, Distance, Speed).setEaseInOutSine().setLoopPingPong();
    }

    // Update is called once per frame
    void Update()
    {
        transform.localRotation = Quaternion.Euler(transform.localRotation.eulerAngles.x, transform.localRotation.eulerAngles.y + Rotation * Time.deltaTime, transform.localRotation.eulerAngles.z);
    }
}
