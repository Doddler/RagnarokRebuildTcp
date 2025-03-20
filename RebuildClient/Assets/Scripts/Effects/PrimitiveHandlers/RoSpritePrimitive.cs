using Assets.Scripts.Utility;

namespace Assets.Scripts.Effects.PrimitiveHandlers
{
    [RoPrimitive("RoSprite")]
    public class RoSpritePrimitive: IPrimitiveHandler
    {
        public RagnarokEffectData.PrimitiveUpdateDelegate GetDefaultUpdateHandler() => UpdatePrimitive;
        public RagnarokEffectData.PrimitiveRenderDelegate GetDefaultRenderHandler() => RenderPrimitive;


        public void UpdatePrimitive(RagnarokPrimitive primitive)
        {
            if (!primitive.IsStepFrame || !primitive.IsActive)
                return;

            primitive.IsActive = primitive.Step < primitive.FrameDuration;
        }

        public static void RenderPrimitive(RagnarokPrimitive primitive, MeshBuilder mb)
        {
            mb.Clear();
        }
    }
}
