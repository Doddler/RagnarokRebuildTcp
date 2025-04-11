using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Sprites;
using Assets.Scripts.Utility;
using UnityEngine;

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

            if (data.Flags.HasFlag(RoPrimitiveHandlerFlags.NoAnimation))
                return;

            data.Size = VectorHelper.Clamp(data.Size + data.ScalingSpeed * Time.deltaTime, data.MinSize, data.MaxSize);

            data.ScalingSpeed += data.ScalingAccel * Time.deltaTime;

            if (data.Flags.HasFlag(RoPrimitiveHandlerFlags.CycleColors))
            {
                if (data.CurCycleDelay > 0)
                    data.CurCycleDelay -= Time.deltaTime;

                if (data.CurCycleDelay <= 0)
                {
                    if (EffectHelpers.TryChangeAndCycleColor(data.Color, data.ColorChange, out data.Color))
                        data.CurCycleDelay = data.RGBCycleDelay;
                }
            }
            else
            {
                if (primitive.CurrentPos < primitive.Duration - data.FadeOutTime)
                    data.Alpha += data.AlphaSpeed * Time.deltaTime;
                else
                {
                    var remaining = primitive.Duration - primitive.CurrentPos;
                    if (remaining < 0)
                        data.Alpha = 0;
                    else
                        data.Alpha -= data.Alpha * Time.deltaTime / remaining;
                }

                data.Alpha = Mathf.Clamp(data.Alpha, 0, data.AlphaMax);
                data.Color = new Color(data.Color.r, data.Color.g, data.Color.b, data.Alpha / 255f);
                data.Angle += data.AngleSpeed * Time.deltaTime;
            }

            primitive.IsDirty = true; // primitive.Step == 0 || data.ScalingSpeed != Vector2.zero || data.Flags != RoPrimitiveHandlerFlags.None;
            primitive.IsActive = primitive.CurrentPos < primitive.Duration;
        }
        
        private void RenderTexture3D(RagnarokPrimitive primitive, MeshBuilder mb)
        {
            mb.Clear();
            
            var data = primitive.GetPrimitiveData<Texture3DData>();
            
            //data.Color = Color.blue;
            //
            // #if DEBUG
            // primitive.DebugString = data.ToString();
            // #endif

            if (data.IsStandingQuad)
            {
                if(data.Sprite == null)
                    primitive.AddTextured2DQuad(Vector3.zero, data.Size.x, data.Size.y, data.Color);
                else
                    primitive.AddTexturedSpriteQuad(data.Sprite, Vector3.zero, data.Size.x, data.Size.y, data.Color, data.Angle);
                
            }
            else
            {
                primitive.AddTexturedRectangleQuad(Vector3.zero, data.Size.x, data.Size.y, data.Color);
            }
        }
    }
}