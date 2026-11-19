using System;
using System.IO;
using Assets.Scripts;
using UnityEngine;

namespace Assets.Scripts.Sprites
{
    /// <summary>Parsed ACT actions with source sprite indices and delay units.</summary>
    public sealed class RoAct
    {
        private const int HeaderReservedBytes = 10;
        private const int FrameReservedBytes = 32;
        private const int SoundNameLength = 40;
        private const int VersionRadix = 10;
        private const int Version20 = 20;
        private const int Version21 = 21;
        private const int Version22 = 22;
        private const int Version23 = 23;
        private const int Version25 = 25;
        private const int MaxActions = 4096;
        private const int MaxFramesPerAction = 65536;
        private const int MaxLayersPerFrame = 65536;
        private const int MaxSounds = 65536;

        public int Version { get; private set; }
        public RoAction[] Actions { get; private set; }
        public float[] ActionDelays { get; private set; }
        public string[] Sounds { get; private set; }

        private RoAct() { }

        public static RoAct Load(string path)
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream);
            return Read(reader);
        }

        public static RoAct Read(BinaryReader reader)
        {
            if (reader.ReadByte() != (byte)'A' || reader.ReadByte() != (byte)'C')
                throw new InvalidDataException("Not an ACT file.");

            var minor = reader.ReadByte();
            var major = reader.ReadByte();
            var version = major * VersionRadix + minor;
            var actionCount = reader.ReadUInt16();
            if (actionCount > MaxActions)
                throw new InvalidDataException($"ACT action count exceeds {MaxActions}.");
            SkipReservedBytes(reader, HeaderReservedBytes);

            var actions = new RoAction[actionCount];
            for (var actionIndex = 0; actionIndex < actionCount; actionIndex++)
            {
                var frameCount = ReadCount(reader, MaxFramesPerAction, "frame");
                var frames = new RoFrame[frameCount];
                for (var frameIndex = 0; frameIndex < frameCount; frameIndex++)
                {
                    SkipReservedBytes(reader, FrameReservedBytes);
                    frames[frameIndex] = ReadFrame(reader, version);
                }
                actions[actionIndex] = new RoAction { Frames = frames };
            }

            string[] sounds = null;
            if (version >= Version21)
            {
                var soundCount = ReadCount(reader, MaxSounds, "sound");
                sounds = new string[soundCount];
                for (var i = 0; i < soundCount; i++)
                {
                    var name = reader.ReadChars(SoundNameLength);
                    if (name.Length != SoundNameLength)
                        throw new EndOfStreamException("Truncated ACT sound name.");
                    sounds[i] = new string(name).TrimEnd('\0');
                }

                foreach (var action in actions)
                foreach (var frame in action.Frames)
                {
                    if (frame.Sound < 0) continue;
                    if (frame.Sound >= sounds.Length)
                        throw new InvalidDataException("ACT frame references an invalid sound event.");
                }
            }

            float[] actionDelays = null;
            if (version >= Version22)
            {
                actionDelays = new float[actionCount];
                for (var i = 0; i < actionCount; i++)
                    actionDelays[i] = reader.ReadSingle();
            }

            return new RoAct { Version = version, Actions = actions, ActionDelays = actionDelays, Sounds = sounds };
        }

        private static void SkipReservedBytes(BinaryReader reader, int count)
        {
            if (reader.BaseStream.Length - reader.BaseStream.Position < count)
                throw new EndOfStreamException("Truncated ACT record.");
            reader.BaseStream.Seek(count, SeekOrigin.Current);
        }

        private static RoFrame ReadFrame(BinaryReader reader, int version)
        {
            var layerCount = ReadCount(reader, MaxLayersPerFrame, "layer");
            var layers = new RoLayer[layerCount];
            for (var i = 0; i < layerCount; i++)
            {
                var layer = new RoLayer
                {
                    Position = new Vector2(reader.ReadInt32(), reader.ReadInt32()),
                    Index = reader.ReadInt32(),
                    IsMirror = reader.ReadInt32() != 0,
                    Scale = Vector2.one,
                    Color = Color.white
                };

                if (version >= Version20)
                {
                    layer.Color = new Color32(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
                    var scaleX = reader.ReadSingle();
                    var scaleY = version > Version23 ? reader.ReadSingle() : scaleX;
                    layer.Scale = new Vector2(scaleX, scaleY);
                    layer.Angle = reader.ReadInt32();
                    layer.Type = reader.ReadInt32();
                    if (version >= Version25)
                    {
                        layer.Width = reader.ReadInt32();
                        layer.Height = reader.ReadInt32();
                    }
                }
                layers[i] = layer;
            }

            var frame = new RoFrame { Layers = layers, Sound = version >= Version20 ? reader.ReadInt32() : -1 };
            if (version >= Version23)
            {
                var positionCount = ReadCount(reader, MaxLayersPerFrame, "position");
                frame.Pos = new RoPos[positionCount];
                for (var i = 0; i < positionCount; i++)
                    frame.Pos[i] = new RoPos
                    {
                        Unknown1 = reader.ReadInt32(),
                        Position = new Vector2(reader.ReadInt32(), reader.ReadInt32()),
                        Unknown2 = reader.ReadInt32()
                    };
            }
            return frame;
        }

        private static int ReadCount(BinaryReader reader, int max, string name)
        {
            var count = reader.ReadUInt32();
            if (count > max)
                throw new InvalidDataException($"ACT {name} count exceeds {max}.");
            return (int)count;
        }
    }
}
