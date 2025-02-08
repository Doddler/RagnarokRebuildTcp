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

            if (data.ScalingSpeed != Vector2.zero || data.ScalingAccel != Vector2.zero)
            {
                data.Size = VectorHelper.Clamp(data.Size + data.ScalingSpeed * Time.deltaTime, data.MinSize, data.MaxSize);
                data.ScalingSpeed += data.ScalingAccel * Time.deltaTime;
            }

            if (data.ScalingChangeStep > 0 && primitive.IsStepFrame && data.ScalingChangeStep == primitive.Step)
                data.ScalingSpeed = data.ChangedScalingSpeed;

            primitive.transform.localPosition += (Vector3)data.Speed * Time.deltaTime;
            data.Speed += data.Acceleration * Time.deltaTime;
            // Debug.Log(Vector2.Distance(Vector2.zero, data.Speed));

            var fadeStartTime = primitive.Duration - data.FadeOutLength;
            if (primitive.CurrentPos > fadeStartTime)
                data.Alpha = Mathf.Clamp(data.Alpha - 255 / data.FadeOutLength * Time.deltaTime, 0, 255);
            else
                data.Alpha = Mathf.Clamp(data.Alpha + data.AlphaSpeed * Time.deltaTime, 0, 255);
            
            data.Color = new Color(1, 1, 1, Mathf.Clamp01(data.Alpha));
            
            
            if(primitive.CurrentPos > primitive.Duration)
                primitive.EndPrimitive();
            // Debug.Log($"{primitive.Step} {primitive.IsDirty}");
            primitive.IsDirty = true;
        }

        public static void RenderPointyQuad2D(RagnarokPrimitive primitive, MeshBuilder mb)
        {
            var data = primitive.GetPrimitiveData<Texture2DData>();

            var halfWidth = data.Size.x / 2f;
            var halfHeight = data.Size.y / 2f;
            var halfHeight1 = halfHeight / 1.2f;

            var v1 = new Vector3(-halfWidth, -halfHeight + halfHeight1);
            var v2 = new Vector3(0, halfHeight);
            var v3 = new Vector3(0, -halfHeight);
            var v4 = new Vector3(halfWidth, halfHeight - halfHeight1);
            

            var uv0 = new Vector2(0, 1);
            var uv1 = new Vector2(1, 1);
            var uv2 = new Vector2(0, 0);
            var uv3 = new Vector2(1, 0);
            
            var color = new Color(data.Color.r, data.Color.g, data.Color.b, Mathf.Clamp(data.Alpha / 255f, 0f, 1f));
            
            primitive.AddTexturedQuad(v1, v2, v3, v4, uv0, uv1, uv2, uv3, color);
        }

        private void RenderTexture2D(RagnarokPrimitive primitive, MeshBuilder mb)
        {
            var data = primitive.GetPrimitiveData<Texture2DData>();

            var offset = data.PivotFromBottom ? new Vector3(0, data.Size.y) : Vector3.zero;
            
            var color = new Color(data.Color.r, data.Color.g, data.Color.b, Mathf.Clamp(data.Alpha / 255f, 0f, 1f));

            if (data.Sprite != null)
            {
                primitive.Material.mainTexture = data.Sprite.texture;
                primitive.AddTexturedSpriteQuad(data.Sprite, offset, data.Size.x, data.Size.y, color);
            }
            else
                primitive.AddTextured2DQuad(offset, data.Size.x, data.Size.y, color);
        }
    }
}