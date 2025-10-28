namespace Assets.Scripts.Sprites
{
    public class RoAct<T> where T : IRoAct
    {
        public char[] Signature { get; set; }
        public byte VersionMinor { get; set; }
        public byte VersionMajor { get; set; }
        public ushort AnimationClipCount { get; set; }
        public AnimationClip[] AnimationClips { get; set; }
        public uint AnimationEventCount { get; set; }
        public AnimationEvent[] AnimationEvents { get; set; }
        public float[] FrameTimes { get; set; }

        private T roActData;
        
        public void WriteBytes(string filePath)
        {
            
        }

        public void ReadBytes(string filePath)
        {
            
        }
    }
}