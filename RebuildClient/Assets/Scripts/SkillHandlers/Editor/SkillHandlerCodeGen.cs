using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using RebuildSharedData.Enum;
using UnityEditor;
using UnityEditor.Compilation;

namespace Assets.Scripts.SkillHandlers.Editor
{
    public static class SkillHandlerCodeGen
    {
        [MenuItem("Ragnarok/CodeGen/Update Skill Handlers", false, 121)]
        public static void UpdateSkillHandlers()
        {
            var count = System.Enum.GetNames(typeof(CharacterSkill)).Length;

            var code = new StringBuilder();

            code.Append("using Assets.Scripts.SkillHandlers.Handlers;\n\n");
            code.Append("namespace Assets.Scripts.SkillHandlers\n{\n\tpublic static partial class ClientSkillHandler\n\t{\n\t\tstatic ClientSkillHandler()\n\t\t{\n");
            code.Append($"\t\t\thandlers = new SkillHandlerBase[{count}];\n");

            var handlers = new List<string>();
            for (var i = 0; i < count; i++)
                handlers.Add($"\t\t\thandlers[{i}] = new DefaultSkillHandler();");
            
            //var handlers = new SkillHandlerBase[count];

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes().Where(t => t.IsClass && t.GetCustomAttribute<SkillHandlerAttribute>() != null))
                {
                    //var handler = (SkillHandlerBase)Activator.CreateInstance(type)!;
                    var attr = type.GetCustomAttribute<SkillHandlerAttribute>();
                    var skill = attr.SkillType;

                    handlers[(int)skill] = $"\t\t\thandlers[{(int)skill}] = new {type.Name}();";
                    if (attr.RunHandlerWithoutSource)
                        handlers[(int)skill] += $"\n\t\t\thandlers[{(int)skill}].ExecuteWithoutSource = true;";
                }
            }

            foreach (var l in handlers)
                code.Append(l).Append("\n");

            code.Append("\t\t}\n\t}\n}\n");
            
            File.WriteAllText(@"Assets\Scripts\SkillHandlers\ClientSkillHandlerGenerated.cs", code.ToString());
            
            CompilationPipeline.RequestScriptCompilation();
        }
    }
}