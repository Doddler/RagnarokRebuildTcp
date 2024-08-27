using UnityEngine;

namespace Assets.Scripts.Misc
{
    public class ArcPathObject : MonoBehaviour
    {
        private Vector3 start;
        private Vector3 end;
        private float height;
        private float time;
        private float progress;
        private Transform parent;
        
        public void Init(Transform parent, Vector3 start, Vector3 end, float height, float time)
        {
            this.start = start;
            this.end = end;
            this.height = height;
            this.time = time;
            this.parent = parent;
            progress = 0;
        }

        public void Update()
        {
            transform.position = parent.transform.position + VectorHelper.SampleParabola(start, end, height, progress / time);
            progress += Time.deltaTime;
            if (progress > time)
            {
                transform.localPosition = end;
                Destroy(this);
            }
        }
    }
}