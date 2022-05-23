using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.MapEditor.Editor
{
    [Serializable]
    public class RoWater
    {
        public float Level;
        public int Type;
        public float WaveHeight;
        public float WaveSpeed;
        public float WavePitch;
        public int AnimSpeed;
        public string[] Images;

    }

    [Serializable]
    public class RoWorldModel
    {
        public int Index;
        public string Name;
        public int AnimType;
        public float AnimSpeed;
        public int BlockType;
        public string FileName;
        public string NodeName;
        public Vector3 Position;
        public Vector3 Rotation;
        public Vector3 Scale;
    }

    [Serializable]
    public class RoWorldLight
    {
        public int Index;
        public string Name;
        public Vector3 Position;
        public Color Color;
        public float Range;
    }

    [Serializable]
    public class RoWorldSound
    {
        public int Index;
        public string Name;
        public string File;
        public Vector3 Position;
        public float Volume;
        public int Width;
        public int Height;
        public float Range;
        public float Cycle;
    }

    [Serializable]
    public class RoWorldEffect
    {
        public int Index;
        public string Name;
        public Vector3 Position;
        public int Id;
        public float Delay;
        public Vector4 Param;
    }

    [Serializable]
    public class RoLightSetup
    {
        public int Latitude;
        public int Longitude;
        public Color Diffuse;
        public Color Ambient;
        public float Opacity;

        public bool UseMapAmbient;
        //public Vector3 Direction;
    }

    [Serializable]
    public class RoFogSetup
    {
	    public Color FogColor;
	    public float NearPlane;
	    public float FarPlane;
    }

    public class RagnarokWorld : ScriptableObject
    {
        public string MapName;

        public string IniFileName;
        public string GndFileName;
        public string GatFileName;
        public string SrcFileName;

        public int Version;
        public RoWater Water;
        public RoLightSetup LightSetup;
        public RoFogSetup FogSetup;

        public List<RoWorldSound> Sounds = new List<RoWorldSound>();
        public List<RoWorldModel> Models = new List<RoWorldModel>();
        public List<RoWorldLight> Lights = new List<RoWorldLight>();
        public List<RoWorldEffect> Effects = new List<RoWorldEffect>();
    }
}