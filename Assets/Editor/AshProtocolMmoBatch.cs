using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

[InitializeOnLoad]
public static class AshProtocolMmoBatch
{
    const string BuildProfilePath = "Assets/AnyRPG/Addons/anymmo-fishnet/Build Profiles/AnyMMO Demo Game.asset";
    const string OutputPath = "Builds/AnyMMO/AnyMMO.exe";

    static bool started;

    static AshProtocolMmoBatch()
    {
        if (!HasArg("-ashMmo"))
        {
            return;
        }

        EditorApplication.delayCall += Run;
    }

    public static void SetupAndBuild()
    {
        Run();
    }

    static void Run()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += Run;
            return;
        }

        if (started)
        {
            return;
        }

        started = true;

        if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone) != ScriptingImplementation.Mono2x)
        {
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            started = false;
            EditorApplication.delayCall += Run;
            return;
        }

        Build();
    }

    static void Build()
    {
        string projectRoot = Directory.GetCurrentDirectory();
        string output = Path.Combine(projectRoot, OutputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(output));

        string[] scenes = ReadEnabledScenes(BuildProfilePath);
        if (scenes.Length == 0)
        {
            Debug.LogError("No scenes found in " + BuildProfilePath);
            EditorApplication.Exit(1);
            return;
        }

        Debug.Log("Building AnyMMO with " + scenes.Length + " scenes to " + output);
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = output,
            target = BuildTarget.StandaloneWindows64,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.None
        });

        if (report.summary.result != BuildResult.Succeeded)
        {
            Debug.LogError("Build failed: " + report.summary.result + " errors " + report.summary.totalErrors);
            EditorApplication.Exit(1);
            return;
        }

        Debug.Log("Build succeeded: " + output);
        EditorApplication.Exit(0);
    }

    static string[] ReadEnabledScenes(string profilePath)
    {
        string full = Path.Combine(Directory.GetCurrentDirectory(), profilePath);
        if (!File.Exists(full))
        {
            Debug.LogError("Missing build profile " + full);
            return Array.Empty<string>();
        }

        return File.ReadAllLines(full)
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("m_path: ", StringComparison.Ordinal))
            .Select(line => line.Substring("m_path: ".Length).Trim())
            .Where(path => path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    static bool HasArg(string arg)
    {
        return Environment.GetCommandLineArgs().Any(value => string.Equals(value, arg, StringComparison.OrdinalIgnoreCase));
    }
}
