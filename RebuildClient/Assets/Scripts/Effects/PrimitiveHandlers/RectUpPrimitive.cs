using Assets.Scripts.Utility;
using UnityEngine;

namespace Assets.Scripts.Effects.PrimitiveHandlers
{
    [RoPrimitive("RectUp")]
    public class RectUpPrimitive : IPrimitiveHandler
    {
        public RagnarokEffectData.PrimitiveUpdateDelegate GetDefaultUpdateHandler() => UpdatePrimitive;
        public RagnarokEffectData.PrimitiveRenderDelegate GetDefaultRenderHandler() => RenderPrimitive;

        //anatomy of RectUp data
        //RiseAngle = animation position (fed into sin so wraps every 360)
        //Heights[0] = x size
        //Heights[1] = y size
        //Heights[2] = current height
        //MaxHeight = actual max height

        public void UpdatePrimitive(RagnarokPrimitive primitive)
        {
            var part = primitive.Parts[0];
            part.RiseAngle += 60 * Time.deltaTime;

            if (primitive.Step > primitive.FrameDuration)
                part.Alpha -= 120 * Time.deltaTime;

            part.Heights[2] = part.MaxHeight * Mathf.Sin(Mathf.Deg2Rad * part.RiseAngle) * 0.35f; // 1/3 of height is variable
            part.Heights[2] += part.MaxHeight * 0.65f; // 2/3 of height is fixed

            if (primitive.CurrentPos < 1.5f)
                part.Heights[2] *= Mathf.Sin(Mathf.Deg2Rad * primitive.CurrentPos * 60);

            primitive.IsActive = part.Alpha > 0;
        }

        public static void RenderPrimitive(RagnarokPrimitive primitive, MeshBuilder mb)
        {
            mb.Clear();

            if (!primitive.IsActive)
                return;

            var p = primitive.Parts[0];

            var x = p.Heights[0];
            var y = p.Heights[1];
            var height = p.Heights[2];

            mb.AddVertex(new Vector3(-x, 0, -y)); //0
            mb.AddVertex(new Vector3(x, 0, -y)); //1
            mb.AddVertex(new Vector3(x, 0, y)); //2
            mb.AddVertex(new Vector3(-x, 0, y)); //3

            mb.AddVertex(new Vector3(-x, height, -y)); //4
            mb.AddVertex(new Vector3(x, height, -y)); //5
            mb.AddVertex(new Vector3(x, height, y)); //6
            mb.AddVertex(new Vector3(-x, height, y)); //7

            mb.AddUV(new Vector2(0, 0));
            mb.AddUV(new Vector2(1, 0));
            mb.AddUV(new Vector2(2, 0)); //since we aren't using a texture atlas we can rely on texture wrapping
            mb.AddUV(new Vector2(3, 0));

            mb.AddUV(new Vector2(0, 1));
            mb.AddUV(new Vector2(1, 1));
            mb.AddUV(new Vector2(2, 1));
            mb.AddUV(new Vector2(3, 1));

            mb.AddTriangle(0, 4, 1);
            mb.AddTriangle(4, 5, 1);

            mb.AddTriangle(1, 5, 2);
            mb.AddTriangle(5, 6, 2);
            
            mb.AddTriangle(2, 6, 3);
            mb.AddTriangle(6, 7, 3);
            
            mb.AddTriangle(3, 7, 0);
            mb.AddTriangle(7, 4, 0);
            
            mb.SetAllVertexColors(new Color32(255, 255, 255, (byte)Mathf.Clamp(p.Alpha, 0, 255)));
            mb.FillEmptyNormals();
        }
    }
}