using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEngine;

public static class CIBuild
{
    public static void BuildWeixinMiniGame()
    {
        string outputPath = GetCommandLineArg("-customBuildPath");
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            outputPath = WeixinMiniGameBuildProfileUtility.GetDefaultBuildRootPath();
        }

        string appId = GetCommandLineArg("-weixinAppId");
        if (string.IsNullOrWhiteSpace(appId))
        {
            appId = Environment.GetEnvironmentVariable("WEIXIN_APP_ID") ?? string.Empty;
        }

        string projectName = GetCommandLineArg("-weixinProjectName");

        BuildWeixinMiniGameProject(outputPath, appId, projectName);
    }

    private static string GetCommandLineArg(string key)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return string.Empty;
    }

    private static void BuildWeixinMiniGameProject(string outputPath, string appId, string projectName)
    {
        string fullOutputPath = Path.GetFullPath(outputPath);
        if (string.IsNullOrWhiteSpace(fullOutputPath))
        {
            throw new InvalidOperationException("Build output path is empty.");
        }

        Directory.CreateDirectory(fullOutputPath);

        BuildProfile buildProfile = WeixinMiniGameBuildProfileUtility.GetOrCreateBuildProfile(
            fullOutputPath,
            appId,
            projectName);

        ColorSpace originalColorSpace = PlayerSettings.colorSpace;
        BuildMiniGameError result = BuildMiniGameError.Unknown;
        try
        {
            PlayerSettings.colorSpace = ColorSpace.Gamma;
            result = BuildPipeline.BuildMiniGame(buildProfile, BuildOptions.None);
        }
        finally
        {
            PlayerSettings.colorSpace = originalColorSpace;
        }

        if (result != BuildMiniGameError.Succeeded)
        {
            throw new Exception("Weixin Mini Game build failed: " + result);
        }

        string importableProjectPath = Path.Combine(fullOutputPath, "minigame");
        if (!File.Exists(Path.Combine(importableProjectPath, "game.json")) ||
            !File.Exists(Path.Combine(importableProjectPath, "project.config.json")))
        {
            throw new Exception("Weixin Mini Game build completed, but importable DevTools project was not generated.");
        }

        Debug.Log("Build succeeded: " + importableProjectPath);
    }
}
