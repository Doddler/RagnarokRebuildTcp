using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.SkillHandlers;
using Assets.Scripts.Utility;
using UnityEngine;

namespace Assets.Scripts.Effects.PrimitiveHandlers
{
    [RoPrimitive("Spike3D", typeof(Spike3DData))]
    public class Spike3DPrimitive : IPrimitiveHandler
    {
        public RagnarokEffectData.PrimitiveUpdateDelegate GetDefaultUpdateHandler() => UpdatePrimitive;
        public RagnarokEffectData.PrimitiveRenderDelegate GetDefaultRenderHandler() => RenderPrimitive;

        public void UpdatePrimitive(RagnarokPrimitive primitive)
        {
            if (!primitive.IsActive)
                return;
            
            var data = primitive.GetPrimitiveData<Spike3DData>();
            
            data.Speed += data.Acceleration * Time.deltaTime;
            if (primitive.IsStepFrame && data.Flags.HasFlag(Spike3DFlags.SpeedLimit) && data.StopStep == primitive.Step)
            {
                data.Speed = 0;
                data.Acceleration = 0;
            }

            primitive.transform.localPosition += primitive.transform.localRotation * Vector3.up * (data.Speed * 60 * Time.deltaTime);
            
            var fadeStartTime = primitive.Duration - data.FadeOutLength;
            if (primitive.CurrentPos > fadeStartTime)
                data.Alpha = Mathf.Clamp(data.Alpha - data.AlphaMax / data.FadeOutLength * Time.deltaTime, 0, 255);
            else
                data.Alpha = Mathf.Clamp(data.Alpha + data.AlphaSpeed * Time.deltaTime, 0, data.AlphaMax);

            primitive.IsActive = primitive.Step < primitive.FrameDuration;
            if (!primitive.IsActive)
                data.Alpha = 0;
        }

        public static void RenderPrimitive(RagnarokPrimitive primitive, MeshBuilder mb)
        {
            mb.Clear();
            if (!primitive.IsActive)
                return;
            
            var data = primitive.GetPrimitiveData<Spike3DData>();
            
            var color = new Color(1f, 1f, 1f, data.Alpha / 255f);
            // Debug.Log(color);
            
            mb.AddVertex(new Vector3(0f, data.Height, 0f)); //top
            mb.AddVertex(new Vector3(-data.Size, 0, -data.Size)); //bl
            mb.AddVertex(new Vector3(-data.Size, 0, data.Size)); //tl
            
            mb.AddVertex(new Vector3(0f, data.Height, 0f)); //top
            mb.AddVertex(new Vector3(-data.Size, 0, data.Size)); //tl
            mb.AddVertex(new Vector3(data.Size, 0, data.Size)); //tr
            
            mb.AddVertex(new Vector3(0f, data.Height, 0f)); //top
            mb.AddVertex(new Vector3(data.Size, 0, data.Size)); //tr
            mb.AddVertex(new Vector3(data.Size, 0, -data.Size)); //br
            
            mb.AddVertex(new Vector3(0f, data.Height, 0f)); //top
            mb.AddVertex(new Vector3(data.Size, 0, -data.Size)); //br
            mb.AddVertex(new Vector3(-data.Size, 0, -data.Size)); //bl
            
            mb.AddTriangle(0, 1, 2);
            mb.AddTriangle(3, 4, 5);
            mb.AddTriangle(6, 7, 8);
            mb.AddTriangle(9, 10, 11);

            for (var i = 0; i < 4; i++)
            {
                mb.AddUV(new Vector2(i * 0.2f, 1));
                mb.AddUV(new Vector2(i * 0.2f, 0));
                mb.AddUV(new Vector2((i + 1) * 0.2f, 1));
                
                mb.AddColor(color);
                mb.AddColor(color);
                mb.AddColor(color);
            }
        }
    }
}