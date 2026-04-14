namespace Assets.Scripts.Sprites
{
    public class RoActData
    {
        public char[] Signature { get; set; } //2.0
        public byte VersionMinor { get; set; } //2.0
        public byte VersionMajor { get; set; } //2.0
        public ushort AnimationClipCount { get; set; } //2.0
        public RoAnimationClip[] AnimationClips { get; set; } //2.0
        public uint AnimationEventCount { get; set; } //2.1
        public RoAnimationEvent[] AnimationEvents { get; set; } //2.1
        public float[] FrameTimes { get; set; } //2.3
    }
    
    public class RoAnimationClip
    {
        public uint AnimationFrameCount; //2.0
        public RoAnimationFrame[] AnimationFrames; //2.0
    }
    
    public class RoAnimationFrame
    {
        public uint SpriteLayerCount {get; set;} //2.0
        public RoSpriteLayer[] SpriteLayers {get; set;} //2.0
        public uint AnimationEventId {get; set;} //2.0
        public uint SpriteAnchorCount {get; set;} //2.3
        public RoSpriteAnchor[] AnchorPoints {get; set;} //2.3
    }
    
    public class RoAnimationEvent
    {
        public char[] Name; //2.1
    }
    

    public class RoSpriteLayer
    {
        public uint PositionU {get; set;} //2.0
        public uint PositionV {get; set;} //2.0
        public uint SpritesheetCellIndex {get; set;} //2.0
        public uint IsFlippedV {get; set;} //2.0
        public byte ColorTintRed {get; set;} //2.0
        public byte ColorTintGreen {get; set;} //2.0
        public byte ColorTintBlue {get; set;} //2.0
        public byte ColorTintAlpha {get; set;} //2.0
        public float Scale {get; set;} //2.0 - //2.3
        public float ScaleU {get; set;} //2.4
        public float ScaleV {get; set;} //2.4
        public uint RotationDegrees {get; set;} //2.0
        public uint ImageTypeID {get; set;} //2.0
        public uint ImageWidth {get; set;} //2.5
        public uint ImageHeight {get; set;} //2.5
    }
    
    public class RoSpriteAnchor {
        public uint PositionU; //2.3
        public uint PositionV; //2.3
    }
}