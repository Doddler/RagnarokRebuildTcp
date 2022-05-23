using System.Reflection;
using Antlr4.Runtime;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RoRebuildServer.Data;
using RoRebuildServer.Data.Config;
using RoRebuildServer.Logging;
using RoServerScript;

namespace RoRebuildServer.ScriptSystem;

internal class ScriptCompiler
{
    public Dictionary<string, string> scriptFiles = new();

    /// <summary>
    /// Compiles a script on a given path and stores the output.
    /// </summary>
    /// <param name="inputPath">Path to script file</param>
    /// <returns>True if the script needed to be compiled, false if the script was found in cache.</returns>
    public bool Compile(string inputPath)
    {
        var config = ServerConfig.DataConfig;

        var cachePath = Path.Combine(config.CachePath, "scripts");
        var dataPath = config.DataPath;

        var name = Path.GetRelativePath(dataPath, inputPath).Replace(".", "_").Replace("\\", "_").Replace("/", "_");
        var cacheFileName = Path.Combine(cachePath, name) + ".txt";

        if (config.CacheScripts)
        {
            if (File.Exists(cacheFileName))
            {
                var oldModified = File.GetLastWriteTime(inputPath);
                var newModified = File.GetLastWriteTime(cacheFileName);

                if (newModified > oldModified)
                {
                    ServerLogger.Debug($"File {cacheFileName} is already built, using it instead.");
                    var script = File.ReadAllText(cacheFileName);
                    scriptFiles.Add(Path.GetRelativePath(dataPath, inputPath), script);
                    return false;
                }
            }
        }

        var fs = new StreamReader(inputPath);
        var input = new AntlrInputStream(fs);

        var lexer = new RoScriptLexer(input);
        var tokenStream = new CommonTokenStream(lexer);
        var parser = new RoScriptParser(tokenStream);

        var walker = new ScriptTreeWalker();
        
        var str = walker.BuildClass(name, parser);
        
        scriptFiles.Add(Path.GetRelativePath(dataPath, inputPath), str);

        if (config.CacheScripts)
        {
            if (!Directory.Exists(cachePath))
                Directory.CreateDirectory(cachePath);
            
            File.WriteAllText(cacheFileName, str);
        }

        return true;
    }

    private bool TryLoadFromCache(out Assembly assembly)
    {
        assembly = null!;

        var cachePath = Path.Combine(ServerConfig.DataConfig.CachePath, "Script.dll");
        if (!File.Exists(cachePath))
            return false;

        try
        {
            var bytes = File.ReadAllBytes(cachePath);

            assembly = Assembly.Load(bytes);

            return true;
        }
        catch (Exception ex)
        {
            ServerLogger.LogWarning($"Could not load script assembly from cache due to an exception: {ex}");

            return false;
        }
    }
    
    public Assembly Load(bool loadFromCache)
    {
        var useCache = ServerConfig.DataConfig.CacheScripts;
        if (useCache && loadFromCache)
        {
            if (TryLoadFromCache(out var a))
            {
                ServerLogger.Log("Scripts have not changed, script assembly loaded from cache.");
                scriptFiles.Clear(); //no need to keep the scripts in memory anymore.
                scriptFiles = null;
                return a;
            }
        }

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

        var bytes = memoryStream.ToArray();
        if(useCache)
            File.WriteAllBytes(Path.Combine(ServerConfig.DataConfig.CachePath, "Script.dll"), bytes);

        scriptFiles.Clear(); //no need to keep the scripts in memory anymore.
        scriptFiles = null;

        return Assembly.Load(bytes);

    }
}