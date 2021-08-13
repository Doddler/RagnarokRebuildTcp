using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Assets.Scripts
{
    public static class BinarySerializationExtensions
    {

        public static void Write(this BinaryWriter bw, Vector2 vec)
        {
            bw.Write(vec.x);
            bw.Write(vec.y);
        }

        public static void Write(this BinaryWriter bw, Vector3 vec)
        {
            bw.Write(vec.x);
            bw.Write(vec.y);
            bw.Write(vec.z);
        }

        public static void Write(this BinaryWriter bw, Vector4 vec)
        {
            bw.Write(vec.x);
            bw.Write(vec.y);
            bw.Write(vec.z);
            bw.Write(vec.w);
        }

        public static void Write(this BinaryWriter bw, Color c)
        {
            bw.Write(c.r);
            bw.Write(c.g);
            bw.Write(c.b);
            bw.Write(c.a);
        }

        public static void WriteVector2Array(this BinaryWriter bw, Vector2[] arr)
        {
            bw.Write(arr.Length);
            for (var i = 0; i < arr.Length; i++)
                bw.Write(i);
        }

        public static void WriteNullableString(this BinaryWriter bw, string str)
        {
            if (str == null)
                bw.Write(false);
        }

        public static void WriteObjectCallback<T>(this BinaryWriter bw, T obj, Action<T> action)
        {
            bw.Write(obj == null);
            if (obj == null)
                return;
            action(obj);
        }

        public static void WriteArrayCallback<T>(this BinaryWriter bw, T[] array, Action<T> action)
        {
            bw.Write(array == null);
            if (array == null)
                return;
            bw.Write(array.Length);

            foreach (var a in array)
                action(a);
        }

        public static T ReadObjectCallback<T>(this BinaryReader br, Action<T> action) where T : new()
        {
            var isNull = br.ReadBoolean();
            if (isNull)
                return default(T);
            var obj = new T();
            action(obj);

            return obj;
        }

        public static T[] ReadArrayCallback<T>(this BinaryReader br, Action<T> action) where T : new()
        {
            var isNull = br.ReadBoolean();
            if (isNull)
                return null;
            var len = br.ReadInt32();
            var array = new T[len];
            for (var i = 0; i < len; i++)
            {
                array[i] = new T();
                action(array[i]);
            }

            return array;
        }

        public static Vector2 ReadVector2(this BinaryReader br)
        {
            return new Vector2(br.ReadSingle(), br.ReadSingle());
        }
        public static Vector3 ReadVector3(this BinaryReader br)
        {
            return new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
        }


        public static Vector3 ReadVector3FlipY(this BinaryReader br)
        {
            return new Vector3(br.ReadSingle(), -br.ReadSingle(), br.ReadSingle());
        }

        public static Vector3 ReadRoPosition(this BinaryReader br)
        {
            return new Vector3(br.ReadSingle() / 5, -br.ReadSingle() / 5, br.ReadSingle() / 5);
        }

        public static Vector4 ReadVector4(this BinaryReader br)
        {
            return new Vector4(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
        }

        public static Quaternion ReadQuaternion(this BinaryReader br)
        {
            return new Quaternion(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
        }

        public static Color ReadColor(this BinaryReader br)
        {
            return new Color(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
        }


        public static Color ReadColor2(this BinaryReader br)
        {
            return new Color(br.ReadSingle() / 255f, br.ReadSingle() / 255f, br.ReadSingle() / 255f, br.ReadSingle() / 255f);
        }


        public static Color ReadColorNoAlpha(this BinaryReader br)
        {
            return new Color(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), 1f);
        }

        public static Vector2[] ReadVector2Array(this BinaryReader br)
        {
            var count = br.ReadInt32();
            var arr = new Vector2[count];
            for (var i = 0; i < count; i++)
                arr[i] = br.ReadVector2();
            return arr;
        }


        public static Vector2[] ReadVector2Array(this BinaryReader br, int count)
        {
            var arr = new Vector2[count];
            for (var i = 0; i < count; i++)
                arr[i] = br.ReadVector2();
            return arr;
        }


        public static float[] ReadFloatArray(this BinaryReader br, int count)
        {
            var arr = new float[count];
            for (var i = 0; i < count; i++)
                arr[i] = br.ReadSingle();
            return arr;
        }
        
        public static Vector2[] ReadVector2StaggeredArray(this BinaryReader br, int count)
        {
            var val = new float[count*2];
            for (var i = 0; i < count * 2; i++)
                val[i] = br.ReadSingle();

            var arr = new Vector2[count];
            for (var i = 0; i < count; i++)
                arr[i] = new Vector2(val[i], val[count + i]);
            return arr;
        }

        public static string ReadNullableString(this BinaryReader br)
        {
            var isNull = br.ReadBoolean();
            if (isNull)
                return null;

            return br.ReadString();
        }

        public static string ReadKoreanString(this BinaryReader br, int len)
        {
            var str = Encoding.GetEncoding(949).GetString(br.ReadBytes(len));
            if (str.Contains("\0"))
                str = str.Split('\0')[0];
            return str;
        }

        public static Color ReadByteColor(this BinaryReader br)
        {
            var b = br.ReadByte() / 255f;
            var g = br.ReadByte() / 255f;
            var r = br.ReadByte() / 255f;
            var a = br.ReadByte() / 255f;
            return new Color(r, g, b, a);
        }
    }
}