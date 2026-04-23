using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEngine;

internal static class WeixinMiniGameBuildProfileUtility
{
    internal const string BuildProfileAssetPath = "Assets/Setting/Build Profiles/WeixinMiniGame.asset";
    private const string DefaultProjectIconPath = "Assets/Common/Resources/GameIcons/game_logo.png";

    private const string PackageConfigAssetPath = "Packages/com.qq.weixin.minigame/Editor/MiniGameConfig.asset";
    private const string AssetsDefaultBackgroundPath = "Assets/Setting/WeixinLoading/background.jpg";
    private const string PackageDefaultBackgroundPath = "Packages/com.qq.weixin.minigame/Runtime/wechat-default/images/background.jpg";
    private const string WeixinSubplatformName = "WeChat:微信小游戏";
    private const string WeChatBuildProfileTypeName = "UnityEditor.Build.Profile.WeChatBuildProfile";
    private const string SharedPlatformSettingsTypeName = "UnityEditor.Build.Profile.SharedPlatformSettings";
    private const string WeixinMiniGameSettingsTypeName = "WeChatWASM.WeixinMiniGameSettings";
    private const string WeixinMiniGameSettingsEditorTypeName = "WeChatWASM.WeixinMiniGameSettingsEditor";

    internal static string GetDefaultBuildRootPath()
    {
        return Path.Combine("Build", "WeixinMiniGame", "wechat");
    }

    [MenuItem("Tools/微信/应用默认项目图标")]
    public static void ApplyDefaultProjectIcon()
    {
        Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(DefaultProjectIconPath);
        if (icon == null)
        {
            throw new InvalidOperationException("Project icon not found: " + DefaultProjectIconPath);
        }

        ApplyIconForTargetGroup("Unknown", icon);
        ApplyIconForTargetGroup("WeixinMiniGame", icon);
        ApplyBuildProfileEditorIcon(icon);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Applied project icon: " + DefaultProjectIconPath);
    }

    internal static BuildProfile GetOrCreateBuildProfile(string buildRootPath, string appId, string projectName)
    {
        EnsureWeixinPackageImported();

        string assetDirectory = Path.GetDirectoryName(BuildProfileAssetPath);
        if (string.IsNullOrWhiteSpace(assetDirectory))
        {
            throw new InvalidOperationException("Invalid Weixin build profile asset path.");
        }

        Directory.CreateDirectory(assetDirectory);

        BuildProfile buildProfile = LoadOrCreateBuildProfile();
        EnsureBuildProfileState(buildProfile);
        ApplyBuildProfileConfig(buildProfile, buildRootPath, appId, projectName);

        EditorUtility.SetDirty(buildProfile);
        AssetDatabase.SaveAssets();

        return buildProfile;
    }

    private static void EnsureWeixinPackageImported()
    {
        if (UnityEditor.PackageManager.PackageInfo.FindForAssetPath(PackageConfigAssetPath) == null)
        {
            throw new InvalidOperationException(
                "微信小游戏转换 SDK 尚未完成导入，请等待 Package Manager 完成 com.qq.weixin.minigame 的安装后重试。");
        }
    }

    private static BuildProfile LoadOrCreateBuildProfile()
    {
        BuildProfile buildProfile = AssetDatabase.LoadAssetAtPath<BuildProfile>(BuildProfileAssetPath);
        if (IsUsableBuildProfile(buildProfile))
        {
            return buildProfile;
        }

        if (buildProfile != null)
        {
            AssetDatabase.DeleteAsset(BuildProfileAssetPath);
        }

        MiniGameSettings miniGameSettings = CreateMiniGameSettings();
        string createdAssetPath = CreateBuildProfileAsset(miniGameSettings);
        buildProfile = AssetDatabase.LoadAssetAtPath<BuildProfile>(createdAssetPath);
        if (!IsUsableBuildProfile(buildProfile))
        {
            throw new InvalidOperationException("无法创建可用的微信小游戏 Build Profile。");
        }

        return buildProfile;
    }

    private static bool IsUsableBuildProfile(BuildProfile buildProfile)
    {
        if (buildProfile == null)
        {
            return false;
        }

        return string.Equals(buildProfile.GetType().FullName, WeChatBuildProfileTypeName, StringComparison.Ordinal) &&
               buildProfile.platformSettings != null;
    }

    private static void EnsureBuildProfileState(BuildProfile buildProfile)
    {
        if (buildProfile == null)
        {
            throw new ArgumentNullException("buildProfile");
        }

        if (!string.Equals(buildProfile.GetType().FullName, WeChatBuildProfileTypeName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("现有微信构建配置资产类型不正确，请删除后重新生成。");
        }

        buildProfile.buildTarget = BuildTarget.WeixinMiniGame;
        buildProfile.moduleName = BuildTarget.WeixinMiniGame.ToString();

        InvokeBuildProfileMethod(buildProfile, "LoadPlayerSettings");
        if (buildProfile.playerSettings == null)
        {
            InvokeBuildProfileMethod(buildProfile, "CreatePlayerSettingsFromGlobal");
        }

        if (buildProfile.playerSettings != null)
        {
            SetPlayerSettingsColorSpaceOverride(buildProfile.playerSettings, 0);
        }

        if (buildProfile.platformSettings == null)
        {
            buildProfile.platformSettings = CreatePlatformSettings();
        }

        EnsureMiniGameSettings(buildProfile);
        if (buildProfile.miniGameSettings != null)
        {
            buildProfile.miniGameSettings.hostName = WeixinSubplatformName;
        }
    }

    private static void EnsureMiniGameSettings(BuildProfile buildProfile)
    {
        Type settingsType = FindRequiredType(WeixinMiniGameSettingsTypeName);
        if (buildProfile.miniGameSettings == null ||
            !settingsType.IsAssignableFrom(buildProfile.miniGameSettings.GetType()))
        {
            buildProfile.miniGameSettings = CreateMiniGameSettings();
        }
    }

    private static string CreateBuildProfileAsset(MiniGameSettings miniGameSettings)
    {
        Type buildProfileType = FindRequiredType(WeChatBuildProfileTypeName);
        BuildProfile buildProfile = ScriptableObject.CreateInstance(buildProfileType) as BuildProfile;
        if (buildProfile == null)
        {
            throw new InvalidOperationException("无法创建微信小游戏 Build Profile 对象。");
        }

        buildProfile.name = "WeixinMiniGame";
        buildProfile.buildTarget = BuildTarget.WeixinMiniGame;
        buildProfile.moduleName = BuildTarget.WeixinMiniGame.ToString();
        buildProfile.platformSettings = CreatePlatformSettings();
        buildProfile.miniGameSettings = miniGameSettings;

        AssetDatabase.CreateAsset(buildProfile, BuildProfileAssetPath);
        return BuildProfileAssetPath;
    }

    private static MiniGameSettings CreateMiniGameSettings()
    {
        Type settingsType = FindRequiredType(WeixinMiniGameSettingsTypeName);
        Type editorType = FindRequiredType(WeixinMiniGameSettingsEditorTypeName);
        object settingsEditor = Activator.CreateInstance(editorType);
        object settings = Activator.CreateInstance(settingsType, settingsEditor);
        if (settings == null)
        {
            throw new InvalidOperationException("无法创建微信小游戏设置对象。");
        }

        UnityEngine.Object defaultConfig = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(PackageConfigAssetPath);
        if (defaultConfig != null)
        {
            string json = EditorJsonUtility.ToJson(defaultConfig);
            JsonUtility.FromJsonOverwrite(json, settings);
        }
        else
        {
            InitializeMissingNestedFields(settings);
        }

        MiniGameSettings miniGameSettings = settings as MiniGameSettings;
        if (miniGameSettings == null)
        {
            throw new InvalidOperationException("微信小游戏设置对象不是合法的 MiniGameSettings。");
        }

        miniGameSettings.hostName = WeixinSubplatformName;
        return miniGameSettings;
    }

    private static BuildProfilePlatformSettingsBase CreatePlatformSettings()
    {
        Type settingsType = FindRequiredType(SharedPlatformSettingsTypeName);
        object platformSettings = Activator.CreateInstance(settingsType);
        BuildProfilePlatformSettingsBase typedPlatformSettings = platformSettings as BuildProfilePlatformSettingsBase;
        if (typedPlatformSettings == null)
        {
            throw new InvalidOperationException("无法创建 Build Profile 平台设置对象。");
        }

        return typedPlatformSettings;
    }

    private static void InitializeMissingNestedFields(object settings)
    {
        foreach (FieldInfo field in settings.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (field.GetValue(settings) != null)
            {
                continue;
            }

            if (field.FieldType == typeof(string))
            {
                field.SetValue(settings, string.Empty);
                continue;
            }

            if (field.FieldType.IsValueType)
            {
                field.SetValue(settings, Activator.CreateInstance(field.FieldType));
                continue;
            }

            if (field.FieldType == typeof(System.Collections.Generic.List<string>))
            {
                field.SetValue(settings, new System.Collections.Generic.List<string>());
                continue;
            }

            object instance = Activator.CreateInstance(field.FieldType);
            field.SetValue(settings, instance);
        }
    }

    private static void ApplyBuildProfileConfig(BuildProfile buildProfile, string buildRootPath, string appId, string projectName)
    {
        string fullBuildRootPath = Path.GetFullPath(buildRootPath);
        buildProfile.buildPath = fullBuildRootPath;

        BuildProfilePlatformSettingsBase platformSettings = buildProfile.platformSettings;
        platformSettings.development = false;
        platformSettings.connectProfiler = false;
        platformSettings.allowDebugging = false;
        platformSettings.waitForManagedDebugger = false;
        platformSettings.buildOptions = BuildOptions.None;

        object miniGameSettings = buildProfile.miniGameSettings;
        object projectConf = GetRequiredFieldValue(miniGameSettings, "ProjectConf");
        object compileOptions = GetRequiredFieldValue(miniGameSettings, "CompileOptions");

        string resolvedProjectName = string.IsNullOrWhiteSpace(projectName)
            ? PlayerSettings.productName
            : projectName.Trim();
        if (string.IsNullOrWhiteSpace(resolvedProjectName))
        {
            resolvedProjectName = Directory.GetParent(Application.dataPath).Name;
        }

        SetFieldValue(projectConf, "projectName", resolvedProjectName);
        if (!string.IsNullOrWhiteSpace(appId))
        {
            SetFieldValue(projectConf, "Appid", appId.Trim());
        }

        // 默认导出为可直接在开发者工具中运行的本地分包资源模式，而不是 CDN 模式。
        SetFieldValue(projectConf, "assetLoadType", 1);
        SetFieldValue(projectConf, "relativeDST", ToProjectRelativePath(fullBuildRootPath));
        SetFieldValue(projectConf, "DST", fullBuildRootPath);
        SetFieldValue(projectConf, "bgImageSrc", ResolveBackgroundPath());

        SetFieldValue(compileOptions, "DevelopBuild", false);
        SetFieldValue(compileOptions, "AutoProfile", false);
        SetFieldValue(compileOptions, "ScriptOnly", false);
    }

    private static string ResolveBackgroundPath()
    {
        if (File.Exists(Path.GetFullPath(AssetsDefaultBackgroundPath)))
        {
            return AssetsDefaultBackgroundPath;
        }

        return PackageDefaultBackgroundPath;
    }

    private static string ToProjectRelativePath(string fullPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        if (fullPath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
        {
            string relative = fullPath.Substring(projectRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.IsNullOrWhiteSpace(relative) ? "." : relative;
        }

        return fullPath;
    }

    private static object GetRequiredFieldValue(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
        if (field == null)
        {
            throw new InvalidOperationException("缺少字段: " + target.GetType().FullName + "." + fieldName);
        }

        object value = field.GetValue(target);
        if (value == null)
        {
            value = Activator.CreateInstance(field.FieldType);
            field.SetValue(target, value);
        }

        return value;
    }

    private static void SetFieldValue(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
        if (field == null)
        {
            throw new InvalidOperationException("缺少字段: " + target.GetType().FullName + "." + fieldName);
        }

        field.SetValue(target, value);
    }

    private static void ApplyIconForTargetGroup(string buildTargetGroupName, Texture2D icon)
    {
        BuildTargetGroup buildTargetGroup;
        if (!Enum.TryParse(buildTargetGroupName, out buildTargetGroup))
        {
            Debug.LogWarning("BuildTargetGroup not found: " + buildTargetGroupName);
            return;
        }

        int[] iconSizes = PlayerSettings.GetIconSizesForTargetGroup(buildTargetGroup);
        if (iconSizes == null || iconSizes.Length == 0)
        {
            Debug.LogWarning("No icon slots for BuildTargetGroup: " + buildTargetGroupName);
            return;
        }

        Texture2D[] icons = new Texture2D[iconSizes.Length];
        for (int i = 0; i < icons.Length; i++)
        {
            icons[i] = icon;
        }

        PlayerSettings.SetIconsForTargetGroup(buildTargetGroup, icons);
    }

    private static void ApplyBuildProfileEditorIcon(Texture2D icon)
    {
        BuildProfile buildProfile = AssetDatabase.LoadAssetAtPath<BuildProfile>(BuildProfileAssetPath);
        if (buildProfile == null || buildProfile.miniGameSettings == null)
        {
            return;
        }

        SerializedObject serializedObject = new SerializedObject(buildProfile);
        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;
        bool assigned = false;
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = true;
            if (iterator.propertyType != SerializedPropertyType.ObjectReference)
            {
                continue;
            }

            if (!string.Equals(iterator.name, "tex", StringComparison.Ordinal))
            {
                continue;
            }

            iterator.objectReferenceValue = icon;
            assigned = true;
        }

        if (!assigned)
        {
            Debug.LogWarning("Unable to locate serialized icon field 'tex' in Weixin build profile.");
            return;
        }

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(buildProfile);
    }

    private static void SetPlayerSettingsColorSpaceOverride(PlayerSettings playerSettings, int colorSpaceValue)
    {
        SerializedObject serializedObject = new SerializedObject(playerSettings);
        SerializedProperty property = serializedObject.FindProperty("m_ActiveColorSpace");
        if (property == null)
        {
            property = serializedObject.FindProperty("m_ColorSpace");
        }

        if (property == null)
        {
            throw new InvalidOperationException("缺少 PlayerSettings 颜色空间序列化字段。");
        }

        property.intValue = colorSpaceValue;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void InvokeBuildProfileMethod(BuildProfile buildProfile, string methodName)
    {
        MethodInfo method = FindMethodInHierarchy(buildProfile.GetType(), methodName);
        if (method == null)
        {
            throw new InvalidOperationException("缺少 BuildProfile 方法: " + methodName);
        }

        method.Invoke(buildProfile, null);
    }

    private static MethodInfo FindMethodInHierarchy(Type type, string methodName)
    {
        Type current = type;
        while (current != null)
        {
            MethodInfo method = current.GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (method != null)
            {
                return method;
            }

            current = current.BaseType;
        }

        return null;
    }

    private static Type FindRequiredType(string fullName)
    {
        Type type = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(GetTypesSafe)
            .FirstOrDefault(candidate => string.Equals(candidate.FullName, fullName, StringComparison.Ordinal));

        if (type == null)
        {
            throw new InvalidOperationException("缺少类型: " + fullName);
        }

        return type;
    }

    private static Type[] GetTypesSafe(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(type => type != null).ToArray();
        }
    }
}

