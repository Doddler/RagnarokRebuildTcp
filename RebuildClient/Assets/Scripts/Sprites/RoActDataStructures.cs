namespace Assets.Scripts.Sprites
{
    public interface IRoAct
    {
        char[] Signature { get; set; }
        byte VersionMinor { get; set; }
        byte VersionMajor { get; set; }
        ushort AnimationClipCount { get; set; }
        AnimationClip[] AnimationClips { get; set; }
    }
    
    public struct RoActV20 : IRoAct
    {
        public char[] Signature { get; set; }
        public byte VersionMinor { get; set; }
        public byte VersionMajor { get; set; }
        public ushort AnimationClipCount { get; set; }
        public AnimationClip[] AnimationClips { get; set; }
    }

    public struct RoActV21 : IRoAct
    {
        public char[] Signature { get; set; }
        public byte VersionMinor { get; set; }
        public byte VersionMajor { get; set; }
        public ushort AnimationClipCount { get; set; }
        public AnimationClip[] AnimationClips { get; set; }
        private uint AnimationEventCount { get; set; }
        private AnimationEvent AnimationEvents { get; set; }
    }

    public struct RoActV23 : IRoAct
    {
        public char[] Signature { get; set; }
        public byte VersionMinor { get; set; }
        public byte VersionMajor { get; set; }
        public ushort AnimationClipCount { get; set; }
        public AnimationClip[] AnimationClips { get; set; }
        private uint AnimationEventCount { get; set; }
        private AnimationEvent AnimationEvents { get; set; }
        private float[] FrameTimes { get; set; }
    }

    public interface IRoActSpriteLayer
    {
        public uint PositionU {get; set;}
        public uint PositionV {get; set;}
        public uint SpritesheetCellIndex {get; set;}
        public uint IsFlippedV {get; set;}
        public byte ColorTintRed {get; set;}
        public byte ColorTintGreen {get; set;}
        public byte ColorTintBlue {get; set;}
        public byte ColorTintAlpha {get; set;}
        public uint RotationDegrees {get; set;}
        public uint ImageTypeID {get; set;}
    }
    
    public struct SpriteLayerV20 : IRoActSpriteLayer
    {
        public uint PositionU {get; set;}
        public uint PositionV {get; set;}
        public uint SpritesheetCellIndex {get; set;}
        public uint IsFlippedV {get; set;}
        public byte ColorTintRed {get; set;}
        public byte ColorTintGreen {get; set;}
        public byte ColorTintBlue {get; set;}
        public byte ColorTintAlpha {get; set;}
        public float Scale {get; set;}
        public uint RotationDegrees {get; set;}
        public uint ImageTypeID {get; set;}
    }

    public struct SpriteLayerV24 :  IRoActSpriteLayer
    {
        public uint PositionU {get; set;}
        public uint PositionV {get; set;}
        public uint SpritesheetCellIndex {get; set;}
        public uint IsFlippedV {get; set;}
        public byte ColorTintRed {get; set;}
        public byte ColorTintGreen {get; set;}
        public byte ColorTintBlue {get; set;}
        public byte ColorTintAlpha {get; set;}
        public float ScaleU {get; set;}
        public float ScaleV {get; set;}
        public uint RotationDegrees {get; set;}
        public uint ImageTypeID {get; set;}
    }
    
    public struct SpriteLayerV25 : IRoActSpriteLayer
    {
        public uint PositionU {get; set;}
        public uint PositionV {get; set;}
        public uint SpritesheetCellIndex {get; set;}
        public uint IsFlippedV {get; set;}
        public byte ColorTintRed {get; set;}
        public byte ColorTintGreen {get; set;}
        public byte ColorTintBlue {get; set;}
        public byte ColorTintAlpha {get; set;}
        public float ScaleU {get; set;}
        public float ScaleV {get; set;}
        public uint RotationDegrees {get; set;}
        public uint ImageTypeID {get; set;}
        public uint ImageWidth {get; set;}
        public uint ImageHeight {get; set;}
    }

    public struct SpriteAnchor {
        public uint PositionU;
        public uint PositionV;
        public uint UnknownFlag;
    }

    public interface IRoActAnimationFrame
    {
        public uint SpriteLayerCount {get; set;}
        public IRoActSpriteLayer[] SpriteLayers {get; set;}
        public uint AnimationEventID {get; set;}
    }
    
    public struct AnimationFrameV20 : IRoActAnimationFrame
    {
        public uint SpriteLayerCount {get; set;}
        public IRoActSpriteLayer[] SpriteLayers {get; set;}
        public uint AnimationEventID {get; set;}
    }
    
    public struct AnimationFrameV23 : IRoActAnimationFrame
    {
        public uint SpriteLayerCount {get; set;}
        public IRoActSpriteLayer[] SpriteLayers {get; set;}
        public uint AnimationEventID {get; set;}
        public uint SpriteAnchorCount {get; set;}
        public SpriteAnchor[] AnchorPoints {get; set;}
    }

    public struct AnimationClip
    {
        public uint AnimationFrameCount ;
        public IRoActAnimationFrame[] AnimationFrames;
    }

    public struct AnimationEvent
    {
        public char[] Name; //40 chars
    }
}