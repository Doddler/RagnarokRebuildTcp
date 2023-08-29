using System.Data;
using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Utility;
using UnityEngine;
using UnityEngine.Video;

namespace Assets.Scripts.Effects.PrimitiveHandlers
{
    [RoPrimitive("Texture3D", typeof(Texture3DData))]
    public class Texture3DPrimitive : IPrimitiveHandler
    {


        public RagnarokEffectData.PrimitiveUpdateDelegate GetDefaultUpdateHandler() => UpdateTexture3D;
        public RagnarokEffectData.PrimitiveRenderDelegate GetDefaultRenderHandler() => RenderTexture3D;
        
        public void UpdateTexture3D(RagnarokPrimitive primitive)
        {
            var data = primitive.GetPrimitiveData<Texture3DData>();

            data.Size = VectorHelper.Clamp(data.Size + data.ScalingSpeed * Time.deltaTime, data.MinSize, data.MaxSize);

            data.ScalingSpeed += data.ScalingAccel * Time.deltaTime;

            if (data.Flags.HasFlag(RoPrimitiveHandlerFlags.CycleColors))
            {
                if (data.CurCycleDelay > 0)
                    data.CurCycleDelay -= Time.deltaTime;

                if (data.CurCycleDelay < 0)
                {
                    if (EffectHelpers.TryChangeAndCycleColor(data.Color, data.ColorChange, out data.Color))
                        data.CurCycleDelay = data.RGBCycleDelay;
                }
            }
            
            primitive.IsDirty = primitive.Step == 0 || data.ScalingSpeed != Vector2.zero || data.Flags != RoPrimitiveHandlerFlags.None;
        }
        
        private void RenderTexture3D(RagnarokPrimitive primitive, MeshBuilder mb)
        {
            var data = primitive.GetPrimitiveData<Texture3DData>();
            
            primitive.AddTexturedRectangleQuad(Vector3.zero, data.Size.x, data.Size.y, data.Color);
        }
    }
}