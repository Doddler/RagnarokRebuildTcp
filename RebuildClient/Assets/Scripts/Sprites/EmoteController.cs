using UnityEngine;

namespace Assets.Scripts.Sprites
{
    internal class EmoteController : MonoBehaviour
    {
        public GameObject Target;
        public int AnimationId;
        public RoSpriteAnimator RoSpriteAnimator;

        //private bool isStarted;

        public void OnFinishLoad(RoSpriteData data)
        {
            RoSpriteAnimator.CurrentMotion = SpriteMotion.Dead;
            RoSpriteAnimator.OnSpriteDataLoadNoCollider(data);
            RoSpriteAnimator.ChangeActionExact(AnimationId);
            
            RoSpriteAnimator.OnFinishAnimation = () =>
            {
                gameObject.SetActive(false);
                if (gameObject != null) Destroy(gameObject);
            };
            

            //isStarted = true;
        }

        public void Update()
        {
            if (Target == null)
                Destroy(gameObject);
            else
                transform.position = Target.transform.position + new Vector3(0, 3f, 0f);
        }
    }
}
