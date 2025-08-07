// using UnityEngine;
//
// namespace Assets.Scripts.Misc
// {
//     public class DescendingPathObject : MonoBehaviour
//     {
//         private float height;
//         private float time;
//         private float progress;
//         private Transform parent;
//         
//         public void Init(Transform parent, float height, float time)
//         {
//
//             this.height = height;
//             this.time = time;
//             this.parent = parent;
//             progress = 0;
//             transform.localPosition = new Vector3(0, height, 0);
//             
//             var lt = LeanTween.moveLocalX(transform.gameObject, 0, 0.5f);
//             lt.setEaseOutQuad();
//             lt.setOnComplete(() => Destroy(this));
//
//         }
//         //
//         // public void Update()
//         // {
//         //     transform.position = parent.transform.position + VectorHelper.SampleParabola(start, end, height, progress / time);
//         //     progress += Time.deltaTime;
//         //     if (progress > time)
//         //     {
//         //         transform.localPosition = end;
//         //         Destroy(this);
//         //     }
//         // }
//     }
// }