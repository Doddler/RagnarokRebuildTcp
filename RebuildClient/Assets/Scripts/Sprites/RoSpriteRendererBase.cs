using RebuildSharedData.Enum;
using UnityEngine;

namespace Assets.Scripts.Sprites
{
    public interface IRoSpriteRenderer
    {
        void SetAction(int action);
        void SetColor(Color color);
        void SetDirection(Direction direction);
        void SetFrame(int frame);
        void SetSprite(RoSpriteData sprite);
        void SetOffset(float offset);
        RoFrame GetActiveRendererFrame();
        bool UpdateRenderer();


        void Rebuild();
        void Initialize(bool makeCollider = false);
    }
}
