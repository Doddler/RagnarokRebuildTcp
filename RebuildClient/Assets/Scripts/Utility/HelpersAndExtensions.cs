using System;
using System.Collections.Generic;
using System.IO;
using Assets.Scripts.MapEditor;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using UnityEngine;

namespace Assets.Scripts
{
    public static class DirectionHelper
    {
        public static Direction FlipDirection(this Direction dir)
        {
            switch (dir)
            {
                case Direction.SouthWest: return Direction.NorthEast;
                case Direction.West: return Direction.East;
                case Direction.NorthWest: return Direction.SouthEast;
                case Direction.North: return Direction.South;
                case Direction.NorthEast: return Direction.SouthWest;
                case Direction.East: return Direction.West;
                case Direction.SouthEast: return Direction.NorthWest;
                case Direction.South: return Direction.North;
            }

            return Direction.None;
        }
    }

    public static class AssetHelper
    {
        public static string GetAssetPath(string path, string filename)
        {
            path = path.Replace("\\", "/");

            //Debug.Log(path);
            //Debug.Log(Directory.Exists(path));

            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            return Path.Combine(path, filename).Replace("\\", "/");
        }
    }

    public static class ListExtensions
    {
        public static void ArrayAdd<T>(this List<T> list, T[] array)
        {
            for (var i = 0; i < array.Length; i++)
                list.Add(array[i]);
        }
    }

    public static class VectorHelper
    {
        public static Vector2[] DefaultQuadUVs()
        {
            return new[] { new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 0), new Vector2(1, 0) };
        }

        public static Vector3 CalcQuadNormal(Vector3 v1, Vector3 v2, Vector3 v3, Vector3 v4)
        {
            var c1 = Vector3.Cross((v2 - v1), (v3 - v1));
            var c2 = Vector3.Cross((v4 - v2), (v1 - v2));
            var c3 = Vector3.Cross((v3 - v4), (v2 - v4));
            var c4 = Vector3.Cross((v1 - v3), (v4 - v3));

            return Vector3.Normalize((c1 + c2 + c3 + c4) / 4);
        }

        public static Vector3 CalcNormal(Vector3 v1, Vector3 v2, Vector3 v3)
        {
            var side1 = v2 - v1;
            var side2 = v3 - v1;

            return Vector3.Cross(side1, side2).normalized;
        }

        public static Vector2 RemapUV(Vector2 pos, Rect space)
        {
            var x = pos.x.Remap(0, 1, space.xMin, space.xMax);
            var y = pos.y.Remap(0, 1, space.yMin, space.yMax);
            return new Vector2(x, y);
        }

        public static Vector3 FlipY(this Vector3 v)
        {
            v.y = -v.y;
            return v;
        }

        public static Quaternion FlipY(this Quaternion q)
        {
            var euler = q.eulerAngles;
            return Quaternion.Euler(-euler.FlipY());
        }

        public static Vector2 Rotate(Vector2 v, float delta)
        {
            return new Vector2(
                v.x * Mathf.Cos(delta) - v.y * Mathf.Sin(delta),
                v.x * Mathf.Sin(delta) + v.y * Mathf.Cos(delta)
            );
        }

        public static Vector2 Clamp(Vector2 v, Vector2 min, Vector2 max)
        {
            return new Vector2(
                Mathf.Clamp(v.x, min.x, max.x),
                Mathf.Clamp(v.y, min.y, max.y)
            );
        }

        //Position = Tile Position
        //Vector2Int = Also Tile Position
        //Vector2 = Map Position
        //Vector3 = World Position
        
        public static Vector2 ToMapPosition(this Vector3 v) => new Vector2(v.x, v.z);
        public static Vector2 ToMapPosition(this Position p) => new Vector2(p.X + 0.5f, p.Y + 0.5f);
        public static Vector2 ToMapPosition(this Vector2Int p) => new Vector2(p.x + 0.5f, p.y + 0.5f);
        public static Vector2Int ToTilePosition(this Vector3 v) => new(Mathf.FloorToInt(v.x), Mathf.FloorToInt(v.z));
        public static Vector2Int ToTilePosition(this Vector2 v) => new(Mathf.FloorToInt(v.x), Mathf.FloorToInt(v.y));
        public static Vector2Int ToTilePosition(this Vector2Int v) => new(v.x, v.y);

        public static Vector3 ToWorldPosition(this Vector2 v)
        {
            var walkProvider = RoWalkDataProvider.Instance;
            var pos = new Vector3(v.x, 0, v.y);
            if (walkProvider != null)
                pos.y = walkProvider.GetHeightForPosition(pos);
            return pos;
        }

        public static Vector3 ToWorldPosition(this Position p) => ToWorldPosition(p.ToMapPosition());
        public static Vector3 ToWorldPosition(this Vector2Int p) => ToWorldPosition(p.ToMapPosition());

    }

    public static class RectHelper
    {
        //public static Vector2 RealCenter(this RectInt rect)
        //{
        //    var x = rect.xMin + (rect.xMax - 1 - rect.xMin) / 2f;
        //    var y = rect.yMin + (rect.yMax - 1 - rect.yMin) / 2f;
        //}

        public static RectInt AreaRect(int minX, int minY, int maxX, int maxY)
        {
            return new RectInt(minX, minY, maxX - minX, maxY - minY);
        }

        public static RectInt ExpandRect(this RectInt src, int dist)
        {
            return new RectInt(src.xMin - dist, src.yMin - dist, src.width + dist * 2, src.height + dist * 2);
        }

        public static RectInt ExpandRectToIncludePoint(RectInt src, int x, int y)
        {
            var minX = src.xMin;
            var minY = src.yMin;
            var maxX = src.xMax;
            var maxY = src.yMax;
            if (x < minX)
                minX = x;
            if (x + 1 > maxX)
                maxX = x + 1;
            if (y < minY)
                minY = y;
            if (y + 1 > maxY)
                maxY = y + 1;
            return AreaRect(minX, minY, maxX, maxY);
        }
    }

    public static class MathHelper
    {
        public static float SnapValue(float value, float interval)
        {
            return Mathf.Round(value / interval) * interval;
        }

        public static float Remap(this float value, float from1, float to1, float from2, float to2)
        {
            return (value - from1) / (to1 - from1) * (to2 - from2) + from2;
        }


        public static bool CloseEnough(float value, float target)
        {
            return Mathf.Abs(value - target) < 0.001f;
        }
    }

    public static class DirectoryHelper
    {
        public static string GetRelativeDirectory(string root, string directory)
        {
            if (!root.EndsWith("/") && !root.EndsWith("\\"))
                root += "/";

            if (!directory.EndsWith("/") && !directory.EndsWith("\\"))
                directory += "/";

            //Debug.Log($"{root} {directory}");

            var uri1 = new Uri(root);
            var uri2 = new Uri(directory);

            return uri1.MakeRelativeUri(uri2).ToString();
        }
    }

    public static class GameObjectExtensions
    {
        public static T GetOrAddComponent<T>(this GameObject go) where T : Component
        {
            var obj = go.GetComponent<T>();
            if (obj != null)
                return obj;
            return go.AddComponent<T>();
        }

        public static void ChangeStaticRecursive(this GameObject go, bool isStatic)
        {
            go.layer = isStatic ? LayerMask.NameToLayer("Object") : LayerMask.NameToLayer("DynamicObject");
            go.isStatic = isStatic;
            foreach (Transform t in go.transform)
            {
                ChangeStaticRecursive(t.gameObject, isStatic);
            }
        }

        public static string GetGameObjectPath(this GameObject obj)
        {
            string path = "/" + obj.name;
            while (obj.transform.parent != null)
            {
                obj = obj.transform.parent.gameObject;
                path = "/" + obj.name + path;
            }

            return path;
        }
    }


    public static class PathHelper
    {
        public static void CreateDirectoryIfNotExists(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }
    }

    public static class ColorHelper
    {
        public static Color32 Black => new Color32(0, 0, 0, 255);
        public static Color32 White => new Color32(255, 255, 255, 255);
    }
}