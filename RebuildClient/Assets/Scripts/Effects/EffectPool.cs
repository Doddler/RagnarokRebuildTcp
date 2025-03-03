using System;
using System.Collections.Generic;
using Assets.Scripts.Utility;
using UnityEngine;

namespace Assets.Scripts.Effects
{
    public static class EffectPool
    {
        private static Stack<Mesh> meshes = new(20);
        private static Stack<EffectPart> partPool = new(80);
        private static Stack<EffectSegment> segmentPool = new(140);
        private static Stack<MeshBuilder> builderPool = new(20);
        private static Dictionary<PrimitiveType, Stack<object>> dataPool = new();

        private static int meshCount = 0;
        
        public static Mesh BorrowMesh()
        {
            if (meshes.Count > 0)
                return meshes.Pop();

            var m = new Mesh();
            #if DEBUG
            m.name = $"Mesh {meshCount++}";
            #endif
            return m;
        }

        public static void ReturnMesh(Mesh mesh)
        {
            mesh.Clear(false);
#if DEBUG
            if (meshes.Contains(mesh))
                throw new Exception($"Attempting to return mesh a second time!");
#endif
            meshes.Push(mesh);
        }

        public static MeshBuilder BorrowMeshBuilder()
        {
            if (builderPool.Count > 0)
                return builderPool.Pop();

            return new MeshBuilder();
        }

        public static object BorrowData(PrimitiveType type)
        {
            if (dataPool.TryGetValue(type, out var stack) && stack.Count > 0)
                return stack.Pop();

            return RagnarokEffectData.NewPrimitiveData(type);
        }

        public static void ReturnData(object obj, PrimitiveType type)
        {
            if (obj is IResettable resettable)
                resettable.Reset();
            else
                return;
            
            if(dataPool.TryGetValue(type, out var stack))
                stack.Push(obj);
            else
            {
                stack = new Stack<object>();
                stack.Push(obj);
                dataPool.Add(type, stack);
            }
        }

        public static void ReturnMeshBuilder(MeshBuilder builder)
        {
            builder.Clear();
            #if DEBUG
            if (builderPool.Contains(builder))
                throw new Exception($"Attempting to return MeshBuilder a second time!");
            #endif
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
