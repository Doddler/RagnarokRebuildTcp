using Assets.Scripts.Network;
using UnityEngine;

namespace Assets.Scripts.Sprites
{
    public class RoSpriteTrailSpawner : MonoBehaviour
    {
        public float Interval = 0.1f;

        private ServerControllable controllable;
        private float delayTime;

        private void Awake()
        {
            controllable = GetComponent<ServerControllable>();
            RoSpriteTrailManager.Instance.AttachTrailToEntity(controllable);
        }
        //
        // private void Update()
        // {
        //     if (controllable == null || controllable.SpriteAnimator == null)
        //     {
        //         Destroy(gameObject);
        //         return;
        //     }
        //
        //     delayTime -= Time.deltaTime;
        //     if (delayTime > 0)
        //         return;
        //
        //     delayTime += Interval;
        //     
        //     ClientDataLoader.Instance.CloneObjectForTrail(controllable.SpriteAnimator);
        // }
    }
}