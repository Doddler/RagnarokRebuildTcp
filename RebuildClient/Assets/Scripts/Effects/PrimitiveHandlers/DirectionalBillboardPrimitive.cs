using System.Data;
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
            var data = primitive.GetPrimitiveData<EffectSpriteData>();
            
            var fadeStartTime = primitive.Duration - data.FadeOutLength;
            if (primitive.CurrentPos > fadeStartTime)
                data.Alpha = Mathf.Clamp(data.Alpha - data.MaxAlpha / data.FadeOutLength * Time.deltaTime, 0, 255);
            else
                data.Alpha = Mathf.Clamp(data.Alpha + data.AlphaSpeed * 60 * Time.deltaTime, 0, data.MaxAlpha);
            
            primitive.IsDirty = true;
        }

        public static void RenderBillboardSprite(RagnarokPrimitive primitive, MeshBuilder mb)
        {
            var data = primitive.GetPrimitiveData<EffectSpriteData>();
            var c = new Color(data.Color.r, data.Color.g, data.Color.b, data.Alpha / 255f);
            if (data.Atlas)
            {
                var id = 0;
                if(data.SpriteList.Length > 1)
                    id = Mathf.FloorToInt(primitive.CurrentPos / (1f / data.FrameRate)) % data.SpriteList.Length;
                var sprite = data.Atlas.GetSprite(data.SpriteList[id]);

                primitive.Material.mainTexture = sprite.texture;
                primitive.AddTexturedSpriteQuad(sprite, Vector3.zero, data.Width, data.Height, c);
            }
            else
            {
                primitive.Material.mainTexture = data.Texture;
                primitive.AddTexturedRectangleQuad(Vector3.zero, data.Width, data.Height, c);
            }
            
            var dir = (primitive.Velocity).normalized;
            
            var multiplier = new Vector3(0, 0, -1);
            
            var axis = Quaternion.LookRotation(dir) * multiplier;
            
            primitive.SetBillboardAxis(axis);
            primitive.SetBillboardSubRotation(Quaternion.Euler(data.BaseRotation));
        }
    }
}