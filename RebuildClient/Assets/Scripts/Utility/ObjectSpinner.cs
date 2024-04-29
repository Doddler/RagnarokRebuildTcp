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
        var bounce = LeanTween.moveLocalX(target, Distance, Speed).setEaseInOutSine().setLoopPingPong();
    }

    // Update is called once per frame
    void Update()
    {
        transform.localRotation = Quaternion.Euler(0f, transform.localRotation.eulerAngles.y + Rotation * Time.deltaTime, 0f);
    }
}
