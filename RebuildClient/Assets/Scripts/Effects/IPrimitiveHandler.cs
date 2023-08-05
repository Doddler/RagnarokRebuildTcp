using Assets.Scripts.Utility;
using UnityEngine;
using static Assets.Scripts.Effects.RagnarokEffectData;

namespace Assets.Scripts.Effects
{
    public interface IPrimitiveHandler
    {
        public PrimitiveUpdateDelegate GetDefaultUpdateHandler();
        public PrimitiveRenderDelegate GetDefaultRenderHandler();
    }
}