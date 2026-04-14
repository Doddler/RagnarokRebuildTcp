using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Assets.Scripts.Sprites
{
    public class RoAct
    {
        private readonly struct Versions
        {
            public const string V20 = "2.0";
            public const string V21 = "2.1";
            public const string V23 = "2.3";
            public const string V24 = "2.4";
            public const string V25 = "2.5";
        }

        public string Version
        {
            get { return $"{VersionMajor}.{VersionMinor}"; }
        }

        public RoAnimationClip[] AnimationClips
        {
            get { return roActData.AnimationClips; }
            set
            {
                roActData.AnimationClips = value;
                roActData.AnimationClipCount = (ushort)value.Length;
            }
        }
        public RoAnimationEvent[] AnimationEvents
        {
            get
            {
                var animationEvents = Version switch
                {
                    Versions.V21 or Versions.V23 or Versions.V24 or Versions.V25 => roActData.AnimationEvents,
                    Versions.V20 => throw new InvalidDataException("Animation events are available on version 2.1+"),
                    _ => throw new InvalidDataException("Invalid act version?")
                };
                return animationEvents;
            }
            set
            {
                switch (Version)
                {
                    case Versions.V21:
                    case Versions.V23:
                    case Versions.V24:
                    case Versions.V25:
                        roActData.AnimationEvents = value;
                        roActData.AnimationEventCount = (uint)value.Length;
                        break;
                    case Versions.V20:
                        throw new InvalidDataException("Animation events are available on version 2.1+");
                    default:
                        throw new InvalidDataException("Invalid act version?");
                }
            }
        }
        public float[] FrameTimes
        {
            get
            {
                var frameTimes = Version switch
                {
                    Versions.V23 or Versions.V24 or Versions.V25 => roActData.FrameTimes,
                    Versions.V20 or Versions.V21 => throw new InvalidDataException("Frame times are available on version 2.3+"),
                    _ => throw new InvalidDataException("Invalid act version?")
                };
                return frameTimes;
            }
            set
            {
                switch (Version)
                {
                    case Versions.V23:
                    case Versions.V24:
                    case Versions.V25:
                        if (value.Length != roActData.AnimationClipCount)
                            throw new InvalidDataException("Invalid frameTime array size");
                        roActData.FrameTimes = value;
                        break;
                    case Versions.V20:
                    case Versions.V21:
                        throw new InvalidDataException("Frame times are available on version 2.3+");
                    default:
                        throw new InvalidDataException("Invalid act version?");
                }
            }
        }

        private char[] Signature
        {
            get { return roActData.Signature; }
            set { roActData.Signature = value; }
        }
        private byte VersionMajor
        {
            get { return roActData.VersionMajor; }
            set { roActData.VersionMajor = value; }
        }
        private byte VersionMinor
        {
            get { return roActData.VersionMinor; }
            set { roActData.VersionMinor = value; }
        }
        private ushort AnimationClipCount
        {
            get { return roActData.AnimationClipCount; }
            set { roActData.AnimationClipCount = value; }
        }
        private uint AnimationEventCount
        {
            get { return roActData.AnimationEventCount; }
            set { roActData.AnimationEventCount = value; }
        }

        private RoActData roActData;

        public RoAct() {}
        public RoAct(string filepath)
        {
            Debug.Log("LETS FUCKING GOOOO");
            ReadBytes(filepath);
        }
        public RoAct(FileStream fileStream)
        {
            ReadBytes(fileStream);
        }
        public RoAct(BinaryReader binaryReader)
        {
            ReadBytes(binaryReader);
        }
        
        public void WriteBytes(string filePath)
        {

        }

        public void ReadBytes(string filePath)
        {
            Debug.Log("ReadBytes string");
            var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            try
            {
                ReadBytes(fileStream);
            }
            catch (NotSupportedException)
            {
                fileStream.Close();
                throw;
            }
            fileStream.Close();
        }

        public void ReadBytes(FileStream fileStream)
        {
            Debug.Log("ReadBytes filestream");
            var binaryReader = new BinaryReader(fileStream);
            try
            {
                ReadBytes(binaryReader);
            }
            catch (NotSupportedException)
            {
                binaryReader.Close();
                throw;
            }
            binaryReader.Close();
        }

        public void ReadBytes(BinaryReader binaryReader)
        {
            Debug.Log("ReadBytes binaryreader");
            roActData = new RoActData();

            Debug.Log("Reading signature");
            Signature = binaryReader.ReadChars(2);
            if (new string(Signature) != "AC")
            {
                throw new NotSupportedException("Not a act file");
            }
            
            Debug.Log("Reading version");
            VersionMinor = binaryReader.ReadByte();
            VersionMajor = binaryReader.ReadByte();
            if (!new[] { "2.0", "2.1", "2.3", "2.4", "2.5" }.Contains(Version))
            {
                throw new NotSupportedException("Unsupported act version");
            }
            Debug.Log($"Version is {Version}");
            
            AnimationClipCount = binaryReader.ReadUInt16();
            Debug.Log($"We have {AnimationClipCount} animations");
            
            var skip = binaryReader.ReadBytes(10);
            Debug.Log($"Skipping irrelevant bytes: {BitConverter.ToString(skip)}");
            
            AnimationClips = new RoAnimationClip[AnimationClipCount];
            for (var cIndex = 0; cIndex < AnimationClipCount; cIndex++)
            {
                Debug.Log($"Reading animation clip {cIndex}");
                var animationClip = new RoAnimationClip();
                animationClip.AnimationFrameCount = binaryReader.ReadUInt32();
                Debug.Log($"Animation clip {cIndex} has {animationClip.AnimationFrameCount} frames");
                
                animationClip.AnimationFrames = new RoAnimationFrame[animationClip.AnimationFrameCount];
                for (var fIndex = 0; fIndex < animationClip.AnimationFrameCount; fIndex++)
                {
                    var animationFrame = new RoAnimationFrame();
                    _ = binaryReader.ReadBytes(32);
                    animationFrame.SpriteLayerCount = binaryReader.ReadUInt32();
                    animationFrame.SpriteLayers = new RoSpriteLayer[animationFrame.SpriteLayerCount];
                    for (var sIndex = 0; sIndex < animationFrame.SpriteLayerCount; sIndex++)
                    {
                        var spriteLayer = new RoSpriteLayer();
                        spriteLayer.PositionU = binaryReader.ReadUInt32();
                        spriteLayer.PositionV = binaryReader.ReadUInt32();
                        spriteLayer.SpritesheetCellIndex = binaryReader.ReadUInt32();
                        spriteLayer.IsFlippedV = binaryReader.ReadUInt32();
                        spriteLayer.ColorTintRed = binaryReader.ReadByte();
                        spriteLayer.ColorTintGreen = binaryReader.ReadByte();
                        spriteLayer.ColorTintBlue = binaryReader.ReadByte();
                        spriteLayer.ColorTintAlpha = binaryReader.ReadByte();
                        switch (Version)
                        {
                            case Versions.V20:
                            case Versions.V21:
                            case Versions.V23:
                                spriteLayer.Scale = binaryReader.ReadSingle();
                                break;
                            case Versions.V24:
                            case Versions.V25:
                                spriteLayer.ScaleU = binaryReader.ReadSingle();
                                spriteLayer.ScaleV = binaryReader.ReadSingle();
                                break;
                            default:
                                throw new InvalidDataException("Invalid act version?");
                        }
                        spriteLayer.RotationDegrees = binaryReader.ReadUInt32();
                        spriteLayer.ImageTypeID = binaryReader.ReadUInt32();
                        switch (Version)
                        {
                            case Versions.V25:
                                spriteLayer.ImageWidth = binaryReader.ReadUInt32();
                                spriteLayer.ImageHeight = binaryReader.ReadUInt32();
                                break;
                            case Versions.V20:
                            case Versions.V21:
                            case Versions.V23:
                            case Versions.V24:
                                break;
                            default:
                                throw new InvalidDataException("Invalid act version?");
                        }
                        animationFrame.SpriteLayers[sIndex] = spriteLayer;
                    }
                    animationFrame.AnimationEventId = binaryReader.ReadUInt32();
                    switch (Version)
                    {
                        case Versions.V23:
                        case Versions.V24:
                        case Versions.V25:
                            animationFrame.SpriteAnchorCount = binaryReader.ReadUInt32();
                            animationFrame.AnchorPoints = new RoSpriteAnchor[animationFrame.SpriteAnchorCount];
                            for (var saIndex = 0; saIndex < animationFrame.SpriteAnchorCount; saIndex++)
                            {
                                var anchorPoints = new RoSpriteAnchor();
                                _ = binaryReader.ReadBytes(4);
                                anchorPoints.PositionU = binaryReader.ReadUInt32();
                                anchorPoints.PositionV = binaryReader.ReadUInt32();
                                _ = binaryReader.ReadUInt32();
                                animationFrame.AnchorPoints[saIndex] = anchorPoints;
                            }
                            break;
                        case Versions.V20:
                        case Versions.V21:
                            break;
                        default:
                            throw new InvalidDataException("Invalid act version?");
                    }
                    animationClip.AnimationFrames[fIndex] = animationFrame;
                }
                AnimationClips[cIndex] = animationClip;
                switch (Version)
                {
                    case Versions.V21:
                    case Versions.V23:
                    case Versions.V24:
                    case Versions.V25:
                        AnimationEventCount = binaryReader.ReadUInt32();
                        AnimationEvents = new RoAnimationEvent[AnimationEventCount];
                        for (var eIndex = 0; eIndex < AnimationEventCount; eIndex++)
                        {
                            var animationEvent = new RoAnimationEvent();
                            animationEvent.Name = binaryReader.ReadChars(40);
                            AnimationEvents[eIndex] = animationEvent;
                        }
                        break;
                    case Versions.V20:
                        break;
                    default:
                        throw new InvalidDataException("Invalid act version?");
                }
                switch (Version)
                {
                    case Versions.V23:
                    case Versions.V24:
                    case Versions.V25:
                        FrameTimes = new float[AnimationClipCount];
                        for (var tIndex = 0; tIndex < AnimationClipCount; tIndex++)
                        {
                            var frameTime = binaryReader.ReadSingle();
                            FrameTimes[tIndex] = frameTime;
                        }
                        break;
                    case Versions.V20:
                    case Versions.V21:
                        break;
                    default:
                        throw new InvalidDataException("Invalid act version?");
                }
            }
        }
    }
}