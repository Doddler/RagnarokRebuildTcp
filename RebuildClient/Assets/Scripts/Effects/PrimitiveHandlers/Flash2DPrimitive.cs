using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Utility;
using UnityEngine;
using static Assets.Scripts.Effects.RagnarokEffectData;

namespace Assets.Scripts.Effects.PrimitiveHandlers
{
    [RoPrimitive("Flash2D", typeof(FlashData))]
    public class Flash2DPrimitive : IPrimitiveHandler
    {
        public PrimitiveUpdateDelegate GetDefaultUpdateHandler() => UpdateFlash2D;
        public PrimitiveRenderDelegate GetDefaultRenderHandler() => RenderFlash2D;

        private void UpdateFlash2D(RagnarokPrimitive primitive)
        {
            var data = primitive.GetPrimitiveData<FlashData>();
            
            var fadeStartTime = primitive.Duration - data.FadeOutLength;
            if (primitive.CurrentPos > fadeStartTime)
                data.Alpha = Mathf.Clamp(data.Alpha - data.MaxAlpha / data.FadeOutLength * Time.deltaTime, 0, data.MaxAlpha);
            else
                data.Alpha = Mathf.Clamp(data.Alpha + data.AlphaSpeed * Time.deltaTime, 0, data.MaxAlpha);

            // Debug.Log($"{primitive.CurrentPos} fadeStart:{fadeStartTime} alpha:{data.Alpha} alphaSpeed:{data.AlphaSpeed} fadeSpeed:{data.MaxAlpha/data.FadeOutLength}");

            data.RotationSpeed += data.RotationAccel * Time.deltaTime * 60;
            data.RotationAngle += data.RotationSpeed * Time.deltaTime;

            data.Length += data.LengthSpeed * Time.deltaTime;

            if (data.Length < 0)
            {
                data.LengthSpeed *= -1; //flip direction if we hit 0 size
                data.Length = 0;
            }
            
            // Debug.Log($"{Time.deltaTime}: Angle: {data.RotationAngle} RotationSpeed: {data.RotationSpeed} Accel: {data.RotationAccel} Length: {data.Length} Arc: {data.ArcLength} Alpha: {data.Alpha}");

            primitive.IsDirty = true;
        }
        
        private void RenderFlash2D(RagnarokPrimitive primitive, MeshBuilder mb)
        {
            var data = primitive.GetPrimitiveData<FlashData>();
            
            var color = new Color(1f, 1f, 1f, data.Alpha / 255f);
            
            var size = data.Length;
            var angle = data.RotationAngle;
            var arc = data.ArcLength;
            
            var i = 0;
            
            var c1 = Mathf.Cos((angle - arc) * Mathf.Deg2Rad);
            var s1 = Mathf.Sin((angle - arc) * Mathf.Deg2Rad);
            var c2 = Mathf.Cos((angle + arc) * Mathf.Deg2Rad);
            var s2 = Mathf.Sin((angle + arc) * Mathf.Deg2Rad);

            var point1 = new Vector3(c1 * size, s1 * size, 0f);
            var point2 = new Vector3(c2 * size, s2 * size, 0f);

            var uv0 = new Vector2(0.5f, 0f);
            var uv1 = new Vector2(1f, 1f);
            var uv2 = new Vector2(0f, 1f);
            
            primitive.AddTriangle(Vector3.zero, point1, point2, uv0, uv1, uv2, color);
        }

    }
}