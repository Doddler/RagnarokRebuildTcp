using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Utility;
using UnityEngine;
using static Assets.Scripts.Effects.RagnarokEffectData;

namespace Assets.Scripts.Effects.PrimitiveHandlers
{
    [RoPrimitive("DirectionalBillboard", typeof(EffectSpriteData))]
    public class DirectionalBillboardPrimitive : IPrimitiveHandler
    {
        public void Init(RagnarokPrimitive primitive)
        {
            var data = primitive.GetPrimitiveData<EffectSpriteData>();
            primitive.SetBillboardMode(data.Style);
        }

        public PrimitiveUpdateDelegate GetDefaultUpdateHandler() => UpdateBillboardSprite;
        public PrimitiveRenderDelegate GetDefaultRenderHandler() => RenderBillboardSprite;

        public static void UpdateBillboardSprite(RagnarokPrimitive primitive)
        {
            primitive.IsDirty = true;
        }

        public static void RenderBillboardSprite(RagnarokPrimitive primitive, MeshBuilder mb)
        {
            var data = primitive.GetPrimitiveData<EffectSpriteData>();
            if (data.Atlas)
            {
                var id = Mathf.FloorToInt(primitive.CurrentPos / (1f / data.FrameRate)) % data.SpriteList.Length;
                var sprite = data.Atlas.GetSprite(data.SpriteList[id]);

                primitive.Material.mainTexture = sprite.texture;
                primitive.AddTexturedSpriteQuad(sprite, Vector3.zero, data.Width, data.Height, Color.white);
            }
            else
            {
                primitive.Material.mainTexture = data.Texture;
                primitive.AddTexturedRectangleQuad(Vector3.zero, data.Width, data.Height, Color.white);
            }
            
            var dir = (primitive.Velocity).normalized;
            
            var multiplier = new Vector3(0, 0, -1);
            
            var axis = Quaternion.LookRotation(dir) * multiplier;
            
            primitive.SetBillboardAxis(axis);
            primitive.SetBillboardSubRotation(Quaternion.Euler(data.BaseRotation));
        }
    }
}