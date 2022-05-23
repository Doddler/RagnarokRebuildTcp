using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using DateTime = System.DateTime;

public static class WebGLUnfucker
{
    [PostProcessBuild]
    public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.WebGL)
            return;

        var buildFolder = "Build_" + DateTime.Now.ToString("yyyy-MM-dd-hh-mm");
        Directory.Move(Path.Combine(pathToBuiltProject, "Build"), Path.Combine(pathToBuiltProject, buildFolder));

        var indexPath = Path.Combine(pathToBuiltProject, "index.html");
        var indexFile = File.ReadAllText(indexPath);
        indexFile = indexFile.Replace("buildUrl = \"Build\";", $"buildUrl = \"{buildFolder}\";");
        File.WriteAllText(indexPath, indexFile);


    }
}
