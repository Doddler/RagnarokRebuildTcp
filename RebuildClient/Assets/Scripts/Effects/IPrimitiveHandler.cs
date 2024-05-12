using static Assets.Scripts.Effects.RagnarokEffectData;

namespace Assets.Scripts.Effects
{
    public interface IPrimitiveHandler
    {
        public void Init(RagnarokPrimitive primitive) {}
        public PrimitiveUpdateDelegate GetDefaultUpdateHandler()
        { 
            return null;
        }
        public PrimitiveRenderDelegate GetDefaultRenderHandler()
        {
            return null;
        }
        
    }
}