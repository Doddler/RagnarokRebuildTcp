using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Assets.Scripts.Effects;
using UnityEditor;

namespace Assets.Editor
{
    public static class EffectDataGenerator
    {
        [MenuItem("Ragnarok/CodeGen/Update 3D Effect + Primitive handlers")]
        public static void Generate3DEffectDataClass()
        {
            var text = new List<string>();

            var effectTypes = new List<string>();
            var handlers = new List<string>();
            var primitiveType = new List<string>();

            foreach (var type in Assembly.GetAssembly(typeof(RagnarokEffectData))!.GetTypes()
                         .Where(t => t.IsClass && t.GetCustomAttribute<RoEffectAttribute>() != null))
            {
                var attr = type.GetCustomAttribute<RoEffectAttribute>();
                if (effectTypes.Contains(attr.TypeName))
                    throw new Exception($"Cannot have two RoEffect attributes with the same type '{attr.TypeName}'!");
                effectTypes.Add(attr.TypeName);

                handlers.Add($"effectHandlers.Add(EffectType.{attr.TypeName}, new {type.FullName}());");
            }


            foreach (var type in Assembly.GetAssembly(typeof(RagnarokEffectData))!.GetTypes()
                         .Where(t => t.IsClass && t.GetCustomAttribute<RoPrimitiveAttribute>() != null))
            {
                var attr = type.GetCustomAttribute<RoPrimitiveAttribute>();

                if (primitiveType.Contains(attr.TypeName))
                    throw new Exception(
                        $"Cannot have two RoPrimitive attributes with the same type '{attr.TypeName}'!");

                primitiveType.Add(attr.TypeName);

                if (typeof(IPrimitiveHandler).IsAssignableFrom(type))
                    handlers.Add($"primitiveHandlers.Add(PrimitiveType.{attr.TypeName}, new {type.FullName}());");

                if (attr.DataType != null)
                    handlers.Add(
                        $"primitiveDataFactory.Add(PrimitiveType.{attr.TypeName}, () => new {attr.DataType.FullName}());");

            }

            text.Add("using System;");
            text.Add("using Assets.Scripts.Effects.EffectHandlers;");
            text.Add("using Assets.Scripts.Effects.EffectHandlers.Environment;");
            text.Add("using Assets.Scripts.Effects.EffectHandlers.General;");
            text.Add("using Assets.Scripts.Effects.EffectHandlers.Skills;");
            text.Add("using Assets.Scripts.Effects.EffectHandlers.StatusEffects;");
            text.Add("");
            text.Add("namespace Assets.Scripts.Effects");
            text.Add("{");
            text.Add(Indent(1) + "public enum EffectType");
            text.Add(Indent(1) + "{");

            foreach (var effect in effectTypes)
                text.Add(Indent(2) + effect + ",");

            text.Add(Indent(1) + "}");
            text.Add("");
            text.Add(Indent(1) + "public enum PrimitiveType");
            text.Add(Indent(1) + "{");

            foreach (var primitive in primitiveType)
                text.Add(Indent(2) + primitive + ",");

            text.Add(Indent(1) + "}");
            text.Add("");
            text.Add(Indent(1) + "public partial class RagnarokEffectData");
            text.Add(Indent(1) + "{");
            text.Add(Indent(2) + "static RagnarokEffectData()");
            text.Add(Indent(2) + "{");
            
            foreach(var handler in handlers)
                text.Add(Indent(3) + handler);
            
            text.Add(Indent(2) + "}");
            text.Add(Indent(1) + "}");
            text.Add("}");
            
            File.WriteAllLines($"Assets\\Scripts\\Effects\\RagnarokEffectData.Generated.cs", text);
    }

        private static string Indent(int indentCount)
        {
            var str = "";
            for (var i = 0; i < indentCount; i++)
                str += "\t";
            return str;
        }
    }
}