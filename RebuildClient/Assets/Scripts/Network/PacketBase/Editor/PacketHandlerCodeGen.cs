using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.SkillHandlers;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using UnityEditor;
using UnityEditor.Compilation;

namespace Assets.Scripts.Network
{
    public static class PacketHandlerCodeGen
    {
        [MenuItem("Ragnarok/CodeGen/Update Packet Handlers", false, 120)]
        public static void UpdatePacketHandlers()
        {
            var count = System.Enum.GetNames(typeof(PacketType)).Length;

            var code = new StringBuilder();

            code.Append("using Assets.Scripts.Network;\nusing Assets.Scripts.Network.PacketBase;\nusing Assets.Scripts.Network.IncomingPacketHandlers;\n");
            code.Append("using Assets.Scripts.Network.IncomingPacketHandlers.Character;\n");
            code.Append("using Assets.Scripts.Network.IncomingPacketHandlers.Combat;\n");
            code.Append("using Assets.Scripts.Network.IncomingPacketHandlers.Environment;\n");
            code.Append("using Assets.Scripts.Network.IncomingPacketHandlers.Network;\n");
            code.Append("using Assets.Scripts.Network.IncomingPacketHandlers.System;\n");
            code.Append("using Assets.Scripts.Network.HandlerBase;\n\n");
            code.Append("namespace Assets.Scripts.Network.PacketBase\n{\n\tpublic static partial class ClientPacketHandler\n\t{\n\t\tstatic ClientPacketHandler()\n\t\t{\n");
            code.Append($"\t\t\thandlers = new ClientPacketHandlerBase[{count}];\n");

            var handlers = new List<string>();
            for (var i = 0; i < count; i++)
                handlers.Add($"\t\t\thandlers[{i}] = new InvalidPacket(); //{(PacketType)i}");
            
            //var handlers = new SkillHandlerBase[count];

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes().Where(t => t.IsClass && t.GetCustomAttribute<ClientPacketHandlerAttribute>() != null))
                {
                    //var handler = (SkillHandlerBase)Activator.CreateInstance(type)!;
                    var attr = type.GetCustomAttribute<ClientPacketHandlerAttribute>();
                    var packetType = attr.PacketType;

                    handlers[(int)packetType] = $"\t\t\thandlers[{(int)packetType}] = new {type.Name}(); //{packetType}";
                }
            }

            foreach (var l in handlers)
                code.Append(l).Append("\n");

            code.Append("\t\t}\n\t}\n}\n");
            
            File.WriteAllText(@"Assets\Scripts\Network\PacketBase\ClientPacketHandlerGenerated.cs", code.ToString());
            
            CompilationPipeline.RequestScriptCompilation();
        }
    }
}