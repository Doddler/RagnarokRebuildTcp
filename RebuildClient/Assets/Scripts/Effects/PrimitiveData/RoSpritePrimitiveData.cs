using Assets.Scripts.Utility;

namespace Assets.Scripts.Effects.PrimitiveData
{
    public class RoSpritePrimitiveData : IResettable
    {
        public RoSpriteData Sprite;
        public int Action;
        public int Frame;
        public int FrameTime = -1;
        
        public void Reset()
        {
            Sprite = null;
            Action = 0;
            Frame = 0;
            FrameTime = -1;
        }
    }
}