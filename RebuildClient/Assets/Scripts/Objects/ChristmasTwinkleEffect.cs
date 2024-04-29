using Assets.Scripts.Objects;
using UnityEngine;

namespace Objects
{
    public class ChristmasTwinkleEffect : SpriteEffect
    {

        public float AnimSpeed;
        public float SpriteScale;
        private float waitTime;
        

        public override void UpdateSpriteEffect()
        {
            waitTime -= Time.deltaTime;
            if (waitTime > 0f)
                return;
            
            var rnd = Random.Range(0, 10);
            
            Sprite.ChangeActionExact(rnd % 5);
            Sprite.AnimSpeed = AnimSpeed;
            
            transform.localScale = rnd % 3 == 1 ? Vector3.one * 0.01f : Vector3.one * SpriteScale;
            waitTime += 2.2f;
        }
    }
}