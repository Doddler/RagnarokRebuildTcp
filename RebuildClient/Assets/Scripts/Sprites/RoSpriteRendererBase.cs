using RebuildSharedData.Enum;
using UnityEngine;

namespace Assets.Scripts.Sprites
{
    public interface IRoSpriteRenderer
    {
        void SetAction(int action, bool is8Direction = false);
        void SetColor(Color color);
        void SetDirection(Direction direction);
        void SetFrame(int frame);
        void SetSprite(RoSpriteData sprite);
        void SetOffset(float offset);
        RoFrame GetActiveRendererFrame();
        bool UpdateRenderer();
        void SetActive(bool isActive);
        void SetOverrideMaterial(Material mat);
        void SetLightProbeAnchor(GameObject anchor);


        void Rebuild();
        void Initialize(bool makeCollider = false);
    }
}
