using Assets.Scripts.Effects;
using UnityEngine;

namespace Assets.Scripts.Sprites
{
    internal class EmoteController : MonoBehaviour
    {
        public GameObject Target;
        public int AnimationId;
        public RoSpriteAnimator RoSpriteAnimator;

        private static Material mat;

        //private bool isStarted;

        public void OnFinishLoad(RoSpriteData data)
        {
            if (mat == null)
            {
                mat = new Material(ShaderCache.Instance.AlphaBlendNoZTestShader);
                mat.renderQueue = 3015;
            }
            
            RoSpriteAnimator.CurrentMotion = SpriteMotion.Dead;
            RoSpriteAnimator.OnSpriteDataLoadNoCollider(data);
            RoSpriteAnimator.ChangeActionExact(AnimationId);
            
            RoSpriteAnimator.OnFinishAnimation = () =>
            {
                gameObject.SetActive(false);
                if (gameObject != null) Destroy(gameObject);
            };
            
            ((RoSpriteRendererStandard)RoSpriteAnimator.SpriteRenderer).SetOverrideMaterial(mat);

            //isStarted = true;
        }

        public void Update()
        {
            if (Target == null)
                Destroy(gameObject);
            else
                transform.position = Target.transform.position;
        }
    }
}
