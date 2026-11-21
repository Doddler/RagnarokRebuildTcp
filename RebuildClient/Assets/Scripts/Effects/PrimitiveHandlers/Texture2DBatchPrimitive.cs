using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Utility;
using UnityEngine;
using static Assets.Scripts.Effects.RagnarokEffectData;

namespace Assets.Scripts.Effects.PrimitiveHandlers
{
    //many Texture2D-style quads in one mesh; the primitive ends with its longest item
    [RoPrimitive("Texture2DBatch", typeof(Texture2DBatchData))]
    public class Texture2DBatchPrimitive : IPrimitiveHandler
    {
        private static readonly Vector2 Uv0 = new(0, 1);
        private static readonly Vector2 Uv1 = new(1, 1);
        private static readonly Vector2 Uv2 = new(0, 0);
        private static readonly Vector2 Uv3 = new(1, 0);

        public PrimitiveUpdateDelegate GetDefaultUpdateHandler() => UpdateTexture2DBatch;
        public PrimitiveRenderDelegate GetDefaultRenderHandler() => RenderTexture2DBatch;

        private void UpdateTexture2DBatch(RagnarokPrimitive primitive)
        {
            var batch = primitive.GetPrimitiveData<Texture2DBatchData>();
            var anyLeft = false;

            for (var i = 0; i < batch.ItemCount; i++)
            {
                ref var item = ref batch.Items[i];
                if (primitive.CurrentPos > item.Duration)
                    continue;

                anyLeft = true;
                item.Size += item.ScalingSpeed * Time.deltaTime;
                item.ScalingSpeed += item.ScalingAccel * Time.deltaTime;

                item.Position += item.Speed * Time.deltaTime;
                item.Speed += item.Acceleration * Time.deltaTime;

                if (item.FadeOutLength > 0 && primitive.CurrentPos > item.Duration - item.FadeOutLength)
                    item.Alpha = Mathf.Clamp(item.Alpha - 255 / item.FadeOutLength * Time.deltaTime, 0, 255);
                else
                    item.Alpha = Mathf.Clamp(item.Alpha + item.AlphaSpeed * Time.deltaTime, 0, 255);
            }

            if (!anyLeft)
            {
                primitive.EndPrimitive();
                return;
            }

            primitive.IsDirty = true;
        }

        private void RenderTexture2DBatch(RagnarokPrimitive primitive, MeshBuilder mb)
        {
            var batch = primitive.GetPrimitiveData<Texture2DBatchData>();

            for (var i = 0; i < batch.ItemCount; i++)
            {
                ref var item = ref batch.Items[i];
                if (primitive.CurrentPos > item.Duration)
                    continue;

                var color = new Color(1f, 1f, 1f, item.Alpha / 255f);
                var angle = -item.Angle * Mathf.Deg2Rad;

                if (item.Shape == Texture2DBatchShape.Pointy)
                {
                    var halfWidth = item.Size.x / 2f;
                    var halfHeight = item.Size.y / 2f;
                    var shoulder = halfHeight - halfHeight / 1.2f;
                    primitive.AddPlacedQuad(new Vector2(-halfWidth, -shoulder), new Vector2(0, halfHeight), new Vector2(0, -halfHeight),
                        new Vector2(halfWidth, shoulder), Uv0, Uv1, Uv2, Uv3, item.Position, angle, color);
                    continue;
                }

                var w = item.Size.x;
                var h = item.Size.y;
                var lift = item.PivotFromBottom ? h : 0f;
                var c0 = new Vector2(-w, h + lift);
                var c1 = new Vector2(w, h + lift);
                var c2 = new Vector2(-w, -h + lift);
                var c3 = new Vector2(w, -h + lift);

                if (item.Sprite != null)
                {
                    var uvs = RagnarokPrimitive.GetSpriteUVs(item.Sprite);
                    primitive.AddPlacedQuad(c0, c1, c2, c3, uvs[0], uvs[1], uvs[2], uvs[3], item.Position, angle, color);
                }
                else
                    primitive.AddPlacedQuad(c0, c1, c2, c3, Uv0, Uv1, Uv2, Uv3, item.Position, angle, color);
            }
        }
    }
}
