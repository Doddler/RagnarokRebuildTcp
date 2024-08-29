using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Utility;
using UnityEngine;

namespace Assets.Scripts.Effects.PrimitiveHandlers
{
    //a whole lot like Texture3D primitive, but with controls to move it in 2d space
    [RoPrimitive("Texture2D", typeof(Texture2DData))]
    public class Texture2DPrimitive : IPrimitiveHandler
    {
        public RagnarokEffectData.PrimitiveUpdateDelegate GetDefaultUpdateHandler() => UpdateTexture2D;
        public RagnarokEffectData.PrimitiveRenderDelegate GetDefaultRenderHandler() => RenderTexture2D;


        public void UpdateTexture2D(RagnarokPrimitive primitive)
        {
            var data = primitive.GetPrimitiveData<Texture2DData>();

            data.Size = VectorHelper.Clamp(data.Size + data.ScalingSpeed * Time.deltaTime, data.MinSize, data.MaxSize);

            data.ScalingSpeed += data.ScalingAccel * Time.deltaTime;

            primitive.IsDirty = primitive.Step == 0 || data.ScalingSpeed != Vector2.zero;

            primitive.transform.localPosition += (Vector3)data.Speed * Time.deltaTime;
            data.Speed += data.Acceleration * Time.deltaTime;
            // Debug.Log(Vector2.Distance(Vector2.zero, data.Speed));

            data.Alpha += data.AlphaSpeed * Time.deltaTime;
            data.Color = new Color(1, 1, 1, Mathf.Clamp01(data.Alpha));
            
            
            if(primitive.CurrentPos > primitive.Duration)
                primitive.EndPrimitive();
            // Debug.Log($"{primitive.Step} {primitive.IsDirty}");
            primitive.IsDirty = true;
        }

        private void RenderTexture2D(RagnarokPrimitive primitive, MeshBuilder mb)
        {
            var data = primitive.GetPrimitiveData<Texture2DData>();

            var offset = new Vector3(0, data.Size.y);
            
            primitive.Material.mainTexture = data.Sprite.texture;
            primitive.AddTexturedSpriteQuad(data.Sprite, offset, data.Size.x, data.Size.y, data.Color);
        }
    }
}