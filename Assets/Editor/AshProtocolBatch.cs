using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

[InitializeOnLoad]
public static class AshProtocolBatch
{
    const string BuildProfilePath = "Assets/AnyRPG/Addons/a-lost-soul-demo-games/Build Profiles/A Lost Soul Story Demo.asset";
    const string OutputPath = "Builds/LostSoul/LostSoul.exe";
    const string Cc0PackagePath = "_setup/anyrpg-cc0-fantasy-content-pack.unitypackage";
    const string StatePath = "_setup/batch-state.txt";

    static bool started;

    static AshProtocolBatch()
    {
        if (!HasArg("-ashSetup"))
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
        if (started)
        {
            return;
        }

        started = true;
        if (ReadState() == "imported" || PackagesToImport().Count == 0)
        {
            Build();
            return;
        }

        WriteState("importing");
        ImportNext();
    }

    static void ImportNext()
    {
        var packages = PackagesToImport();
        if (packages.Count == 0)
        {
            WriteState("imported");
            EditorApplication.delayCall += Build;
            return;
        }

        string packagePath = packages[0];
        AssetDatabase.importPackageCompleted += OnPackageImported;
        AssetDatabase.importPackageFailed += OnPackageFailed;
        Debug.Log("Importing " + packagePath);
        AssetDatabase.ImportPackage(packagePath, false);
    }

    static void OnPackageImported(string packageName)
    {
        AssetDatabase.importPackageCompleted -= OnPackageImported;
        AssetDatabase.importPackageFailed -= OnPackageFailed;
        Debug.Log("Imported " + packageName);
        EditorApplication.delayCall += ImportNext;
    }

    static void OnPackageFailed(string packageName, string error)
    {
        Debug.LogError("Package import failed: " + packageName + " " + error);
        EditorApplication.Exit(1);
    }

    static System.Collections.Generic.List<string> PackagesToImport()
    {
        var packages = new System.Collections.Generic.List<string>();
        string tmp = FindTmpEssentials();
        if (!string.IsNullOrEmpty(tmp) && !Directory.Exists("Assets/TextMesh Pro"))
        {
            packages.Add(tmp);
        }

        string cc0 = Path.GetFullPath(Cc0PackagePath);
        if (File.Exists(cc0) && !Directory.Exists("Assets/AnyRPG/Addons/anyrpg-cc0-fantasy-content-pack"))
        {
            packages.Add(cc0);
        }

        return packages;
    }

    static string FindTmpEssentials()
    {
        string cache = Path.Combine(Directory.GetCurrentDirectory(), "Library", "PackageCache");
        if (!Directory.Exists(cache))
        {
            return null;
        }

        foreach (string dir in Directory.GetDirectories(cache, "com.unity.ugui@*"))
        {
            string candidate = Path.Combine(dir, "Package Resources", "TMP Essential Resources.unitypackage");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    static void Build()
    {
        WriteState("building");
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

        if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone) != ScriptingImplementation.Mono2x)
        {
            WriteState("set-mono");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        }

        WriteState("building");
        Debug.Log("Building Lost Soul with " + scenes.Length + " scenes to " + output);
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

    static string ReadState()
    {
        string path = Path.GetFullPath(StatePath);
        return File.Exists(path) ? File.ReadAllText(path).Trim() : string.Empty;
    }

    static void WriteState(string value)
    {
        string path = Path.GetFullPath(StatePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, value);
    }
}
