using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Sprites;
using Assets.Scripts.Utility;
using UnityEngine;

namespace Assets.Scripts.Effects.PrimitiveHandlers
{
    [RoPrimitive("ParticleAnimatedSprite", typeof(SpriteParticleData))]
    public class ParticleAnimatedSpritePrimitive : IPrimitiveHandler
    {
        public RagnarokEffectData.PrimitiveUpdateDelegate GetDefaultUpdateHandler() => UpdatePrimitive;
        public RagnarokEffectData.PrimitiveRenderDelegate GetDefaultRenderHandler() => RenderPrimitive;
        
        
        private static void UpdateSegments(RagnarokPrimitive primitive, Particle3DSplineData data, bool isFinished)
        {
            var shrink = data.Size / primitive.SegmentCount / 2f;
            var alphaDown = 255f / primitive.SegmentCount;
            
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
        
        public void UpdatePrimitive(RagnarokPrimitive primitive)
        {
            if (!primitive.IsStepFrame || !primitive.IsActive)
                return;

            var data = primitive.GetPrimitiveData<SpriteParticleData>();

            if (data.ScalingSpeed != Vector2.zero || data.ScalingAccel != Vector2.zero)
            {
                data.Size = VectorHelper.Clamp(data.Size + data.ScalingSpeed * (1 / 60f), data.MinSize, data.MaxSize);
                data.ScalingSpeed += data.ScalingAccel * (1 / 60f);
            }

            primitive.transform.localScale = data.Size;

            if (data.FrameSpeed > 0 && primitive.IsStepFrame)
            {
                var maxFrame = data.SpriteData.Actions[0].Frames.Length;
                if (primitive.Step % data.FrameSpeed == 0)
                {
                    data.Frame++;
                    if (data.Frame > maxFrame)
                        data.Frame = 0;
                }
            }

            data.Alpha += data.AlphaSpeed * (1 / 60f);

            primitive.IsActive = primitive.Step < primitive.FrameDuration;
        }

        public static void RenderPrimitive(RagnarokPrimitive primitive, MeshBuilder mb)
        {
            mb.Clear();

            var data = primitive.GetPrimitiveData<SpriteParticleData>();
            var sp = data.SpriteData;
            var color = new Color(1f, 1f, 1f, data.Alpha / 255f);
            
            var meshCache = SpriteMeshCache.GetMeshCacheForSprite(sp.Name);

            var id = data.Frame;

            if (!meshCache.TryGetValue(id, out var mesh))
            {
                mesh = SpriteMeshBuilder.BuildSpriteMesh(sp, 0, 0, data.Frame);
                meshCache.Add(id, mesh);
            }

            mb.AddVertices(mesh.vertices);
            mb.AddTriangles(mesh.triangles);
            mb.AddUVs(mesh.uv);
            for (var i = 0; i < mesh.colors.Length; i++)
                mb.AddColor((Color32)color);
            // mb.AddVertices(mesh.vertices);
            // mb.AddTriangles(mesh.triangles);
            // mb.AddUVs(mesh.uv);
            // for (var i = 0; i < mesh.colors.Length; i++)
            //     mb.AddColor((Color32)new Color(1, 1, 1, 0));
            // mb.AddVertices(mesh.vertices);
            // mb.AddTriangles(mesh.triangles);
            // mb.AddUVs(mesh.uv);
            // for (var i = 0; i < mesh.colors.Length; i++)
            //     mb.AddColor((Color32)new Color(1, 1, 1, 0));
            //mb.AddColors(mesh.colors);
        }
    }
}