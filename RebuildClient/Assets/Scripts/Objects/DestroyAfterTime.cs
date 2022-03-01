using UnityEngine;

namespace Assets.Scripts.Objects
{
    public class DestroyAfterTime : MonoBehaviour
    {
        public float Lifetime;

        private float time;

        // Start is called before the first frame update
        void Start()
        {
            time = Time.timeSinceLevelLoad;
        }

        // Update is called once per frame
        void Update()
        {
            if(time + Lifetime < Time.timeSinceLevelLoad)
                Destroy(gameObject);
        }
    }
}
