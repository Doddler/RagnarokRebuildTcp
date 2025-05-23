using System.Data;
using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Utility;
using UnityEngine;

namespace Assets.Scripts.Effects.PrimitiveHandlers
{
    [RoPrimitive("Particle3DSpline", typeof(Particle3DSplineData))]
    public class Particle3DSplinePrimitive : IPrimitiveHandler
    {
        public RagnarokEffectData.PrimitiveUpdateDelegate GetDefaultUpdateHandler() => UpdateParticle3DSplinePrimitive;
        public RagnarokEffectData.PrimitiveRenderDelegate GetDefaultRenderHandler() => RenderParticle3DSplinePrimitive;

        private static void UpdateSegments(RagnarokPrimitive primitive, Particle3DSplineData data, bool isFinished)
        {
            var shrink = data.Size / primitive.SegmentCount / 2f;
            var alphaDown = 255f / primitive.SegmentCount;

            if (!data.DoShrink)
                shrink = 0;
            
            for (var i = primitive.SegmentCount - 1; i > 0; i--)
            {
                primitive.Segments[i].Position = primitive.Segments[i - 1].Position;
                primitive.Segments[i].Alpha = primitive.Segments[i - 1].Alpha - alphaDown;
                primitive.Segments[i].Size = primitive.Segments[i - 1].Size - shrink;
            }

            if (isFinished)
                primitive.Segments[0].Alpha = -1;
            else
            {
                primitive.Segments[0].Position = data.Position;
                primitive.Segments[0].Alpha = 255;
                primitive.Segments[0].Size = data.Size;
            }
        }
        
        private static void UpdateParticle3DSplinePrimitive(RagnarokPrimitive primitive)
        {
            if (!primitive.IsStepFrame || !primitive.IsActive)
                return;
            
            var data = primitive.GetPrimitiveData<Particle3DSplineData>();

            var vel = new Vector3(0f, data.Velocity.y, data.Velocity.x);
            data.Position += vel;
            data.Velocity += data.Acceleration;
            
            if(data.CapVelocity)
                data.Velocity = new Vector2(Mathf.Clamp(data.Velocity.x, data.MinVelocity.x, data.MaxVelocity.x),
                    Mathf.Clamp(data.Velocity.y, data.MinVelocity.y, data.MaxVelocity.y));
            
            UpdateSegments(primitive, data, primitive.Step > primitive.FrameDuration);

            primitive.IsActive = true; //primitive.Step < primitive.FrameDuration + primitive.SegmentCount;
        }
        
        public static void RenderParticle3DSplineSpritePrimitive(RagnarokPrimitive primitive, MeshBuilder mb)
        {
            mb.Clear();
            
            var data = primitive.GetPrimitiveData<Particle3DSplineData>();
            var c = data.Color;
            var spr = data.SpriteData;
            
            for (var i = 0; i < primitive.SegmentCount; i++)
            {
                var seg = primitive.Segments[i];
                if (seg.Alpha <= 0)
                    continue;

                var a = (int)Mathf.Clamp((seg.Alpha + c.a) / 2, 0, 255);
                var pos = data.Rotation * seg.Position;

                var spriteCount = spr.Sprites.Length;
                var idx = ((primitive.Step + data.AnimOffset) / data.AnimTime) % spriteCount;
                
                primitive.AddTexturedBillboardSprite(spr.Sprites[idx], pos, seg.Size, seg.Size, new Color32(c.r, c.g, c.b, (byte)a));
            }
        }

        private static void RenderParticle3DSplinePrimitive(RagnarokPrimitive primitive, MeshBuilder mb)
        {
            mb.Clear();
            
            var data = primitive.GetPrimitiveData<Particle3DSplineData>();
            var c = data.Color;
            
            for (var i = 0; i < primitive.SegmentCount; i++)
            {
                var seg = primitive.Segments[i];
                if (seg.Alpha <= 0)
                    continue;

                var a = (int)Mathf.Clamp(seg.Alpha, 0, 255);
                var pos = data.Rotation * seg.Position;
                
                primitive.AddTexturedBillboardSprite(data.Sprite, pos, seg.Size, seg.Size, new Color32(c.r, c.g, c.b, (byte)a));
            }
        }
    }
}