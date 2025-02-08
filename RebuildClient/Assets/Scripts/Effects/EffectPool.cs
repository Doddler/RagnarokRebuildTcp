using System.Collections.Generic;
using Assets.Scripts.Utility;
using UnityEngine;

namespace Assets.Scripts.Effects
{
    public static class EffectPool
    {
        private static Stack<Mesh> meshes = new Stack<Mesh>(20);
        private static Stack<EffectPart> partPool = new Stack<EffectPart>(80);
        private static Stack<EffectSegment> segmentPool = new Stack<EffectSegment>(140);
        private static Stack<MeshBuilder> builderPool = new Stack<MeshBuilder>(20);

        public static Mesh BorrowMesh()
        {
            if (meshes.Count > 0)
                return meshes.Pop();

            return new Mesh();
        }

        public static void ReturnMesh(Mesh mesh)
        {
            mesh.Clear(false);
            meshes.Push(mesh);
        }

        public static MeshBuilder BorrowMeshBuilder()
        {
            if (builderPool.Count > 0)
                return builderPool.Pop();

            return new MeshBuilder();
        }

        public static void ReturnMeshBuilder(MeshBuilder builder)
        {
            builder.Clear();
            builderPool.Push(builder);
        }

        public static EffectPart BorrowPart()
        {
            if (partPool.Count > 0)
                return partPool.Pop();
            return new EffectPart();
        }

        public static void ReturnPart(EffectPart part)
        {
            part.Clear();
            partPool.Push(part);
        }

        public static EffectPart[] BorrowParts(int count)
        {
            var p = new EffectPart[count];
            for (var i = 0; i < count; i++)
                p[i] = BorrowPart();
            return p;
        }

        public static void ReturnParts(EffectPart[] parts)
        {
            for (var i = 0; i < parts.Length; i++)
            {
                ReturnPart(parts[i]);
                parts[i] = null;
            }
        }
        
        public static EffectSegment BorrowSegment()
        {
            if (segmentPool.Count > 0)
                return segmentPool.Pop();
            return new EffectSegment();
        }

        public static void ReturnSegment(EffectSegment part)
        {
            part.Clear();
            segmentPool.Push(part);
        }

        public static EffectSegment[] BorrowSegments(int count)
        {
            var p = new EffectSegment[count];
            for (var i = 0; i < count; i++)
                p[i] = BorrowSegment();
            return p;
        }

        public static void ReturnSegments(EffectSegment[] parts)
        {
            for (var i = 0; i < parts.Length; i++)
            {
                ReturnSegment(parts[i]);
                parts[i] = null;
            }
        }
    }
}
