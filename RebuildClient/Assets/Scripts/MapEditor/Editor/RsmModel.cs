using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.MapEditor.Editor
{
    public enum RsmShadingType
    {
        None,
        Flat,
        Smooth
    }

    public class RsmTriangle
    {
        public Vector3[] Vertices = new Vector3[3];
        public Vector3[] Normals = new Vector3[3];
        public Vector2[] UVs = new Vector2[3];
        public Color[] Colors = new Color[3];
        public bool TwoSided;

        public Vector3[] FlippedNormals
        {
            get
            {
                var normals = new Vector3[3];
                normals[0] = -Normals[0];
                normals[1] = -Normals[1];
                normals[2] = -Normals[2];
                return normals;
            }
        }

        public void CalcNormals()
        {
            var normal = VectorHelper.CalcNormal(Vertices[0], Vertices[1], Vertices[2]);
            Normals[0] = normal;
            Normals[1] = normal;
            Normals[2] = normal;
        }
    }

    public class RsmVolumeBox
    {
        public Vector3 Scale;
        public Vector3 Position;
        public Vector3 Rotation;
        public int Flag;
    }

    public class RsmFace
    {
        public int[] VertexIds = new int[3];
        public int[] UVIds = new int[3];
        public int TextureId;
        public int Padding;
        public bool TwoSided;
        public int SmoothGroup;
    }

    public class RsmPosKeyframe
    {
        public int Frame;
        public Vector3 Position;
    }

    public class RsmRotKeyFrame
    {
        public int Frame;
        public Quaternion Rotation;
    }

    public class RsmNode
    {
        public string Name;
        public string ParentName;
        public bool OnlyObject;
        public List<int> TextureIds = new List<int>();
        public Matrix4x4 OffsetMatrix;
        public Matrix4x4 Matrix;
        public Vector3 Offset;
        public Vector3 Position;
        public Vector3 RotationAxis;
        public Vector3 Scale;
        public float RotationAngle;
        public Bounds Bounds;
        public List<Vector3> Vertices = new List<Vector3>();
        public List<Vector2> UVs = new List<Vector2>();
        public List<Color> Colors = new List<Color>();
        public List<RsmFace> Faces = new List<RsmFace>();
        public List<RsmPosKeyframe> PosKeyFrames = new List<RsmPosKeyframe>();
        public List<RsmRotKeyFrame> RotationKeyFrames = new List<RsmRotKeyFrame>();
        public List<RsmNode> Children = new List<RsmNode>();
        public RsmNode Parent;
    }

    public class RsmModel
    {
        public int Version;
        public string Name;
        public RsmShadingType ShadingType;
        public List<RsmNode> RsmNodes;
        public List<string> Textures;
        public float Alpha;
        public RsmNode RootNode;
        public List<RsmVolumeBox> VolumeBoxes = new List<RsmVolumeBox>();
        public List<RsmPosKeyframe> PosKeyFrames = new List<RsmPosKeyframe>();
    }
}