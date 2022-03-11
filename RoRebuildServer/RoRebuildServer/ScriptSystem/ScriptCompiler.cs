using System.Reflection;
using Antlr4.Runtime;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RoRebuildServer.Logging;
using RoServerScript;

namespace RoRebuildServer.ScriptSystem;

internal class ScriptCompiler
{
    public Dictionary<string, string> scriptFiles = new();

    public void Compile(string inputPath)
    {
        var fs = new StreamReader(inputPath);
        var input = new AntlrInputStream(fs);

        var lexer = new RoScriptLexer(input);
        var tokenStream = new CommonTokenStream(lexer);
        var parser = new RoScriptParser(tokenStream);

        var walker = new ScriptTreeWalker();

        var name = Path.GetRelativePath(AppDomain.CurrentDomain.BaseDirectory, inputPath).Replace(".", "_").Replace("\\", "_").Replace("/", "_");

        var str = walker.BuildClass(name, parser);

        

        scriptFiles.Add(Path.GetRelativePath(AppDomain.CurrentDomain.BaseDirectory, inputPath), str);

        if (!Directory.Exists("temp"))
            Directory.CreateDirectory("temp");

        File.WriteAllText(Path.Combine("temp", name) + ".txt", str);

        //Console.WriteLine(str);
    }

    public Assembly Load()
    {
        var trees = new List<SyntaxTree>();
        foreach (var script in scriptFiles)
        {
            var tree = CSharpSyntaxTree.ParseText(script.Value).WithFilePath(script.Key);
            trees.Add(tree);
        }
        
        string assemblyName = Path.GetRandomFileName();
        var references2 = new List<MetadataReference>()
        {
            MetadataReference.CreateFromFile(Assembly.Load("netstandard, Version=2.0.0.0").Location),
            MetadataReference.CreateFromFile(typeof(Object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ScriptCompiler).Assembly.Location)
        };

        Assembly.GetEntryAssembly().GetReferencedAssemblies().ToList().ForEach(a => references2.Add(MetadataReference.CreateFromFile(Assembly.Load(a).Location)));

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName,
            syntaxTrees: trees.ToArray(),
            references: references2,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        using var memoryStream = new MemoryStream();

        var result = compilation.Emit(memoryStream);
        
        if (!result.Success)
        {
            foreach (var r in result.Diagnostics)
            {
                if (r.Severity == DiagnosticSeverity.Error)
                    ServerLogger.LogError(r.Location + " : " + r);
            }
        }

        return Assembly.Load(memoryStream.ToArray());

    }
}