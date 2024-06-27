
using UnityEngine;

namespace Assets.Scripts
{
    public class RoKeyframeRotator : MonoBehaviour
    {
        public float[] Keyframes;
        public Quaternion[] Rotations;

        //private bool visible = false;

        private float time;
        private float endTime;

        public void Awake()
        {
            if (Keyframes == null || Keyframes.Length == 0 || Keyframes.Length != Rotations.Length)
            {
                enabled = false;
                return;
            }

            endTime = Keyframes[Keyframes.Length - 1];
        }

        public void Update()
        {
            //if (!visible)
            //    return;

            time += Time.deltaTime * 2;
            if (time > endTime)
                time -= endTime;

            var id = 0;
            for (var i = 0; i < Keyframes.Length - 1; i++)
            {
                if (Keyframes[i + 1] > time)
                {
                    id = i;
                    break;
                }
            }

            var dist = time.Remap(Keyframes[id], Keyframes[id + 1], 0, 1);
            transform.localRotation =
                Quaternion.Lerp(Rotations[id], Rotations[id+1], dist);
        }
    }
}
