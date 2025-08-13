using Assets.Scripts.Utility;
using UnityEngine.U2D;

namespace Assets.Scripts.Effects.PrimitiveData
{
    public class ParticleUpData : IResettable
    {
        public string[] SpriteNames;
        public SpriteAtlas Atlas;
        
        public void Reset()
        {
            SpriteNames = null;
            Atlas = null;
        }
    }
}