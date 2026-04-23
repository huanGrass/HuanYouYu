using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class GameViewPresetTools
{
    private const int WeChatWidth = 750;
    private const int WeChatHeight = 1334;
    private const string WeChatPresetName = "幻游域 微信 750x1334";

    [MenuItem("Tools/游戏视图/设置 微信 750x1334")]
    public static void SetWeChatPortraitGameView()
    {
        try
        {
            var gameView = GetGameViewWindow();
            var groupType = GetCurrentGroupType(gameView);
            var sizeIndex = FindOrCreateSize(groupType, WeChatWidth, WeChatHeight, WeChatPresetName);
            SetSelectedSize(gameView, sizeIndex);
            SaveGameViewSizesToDisk();
            gameView.Repaint();

            Debug.Log(string.Format("GameView 已切换到 {0}x{1}。", WeChatWidth, WeChatHeight));
        }
        catch (Exception exception)
        {
            Debug.LogError("设置 GameView 分辨率失败: " + exception);
        }
    }

    [MenuItem("Tools/游戏视图/记录当前分组")]
    public static void LogCurrentGroup()
    {
        try
        {
            var gameView = GetGameViewWindow();
            var groupType = GetCurrentGroupType(gameView);
            Debug.Log("当前 GameView 分组: " + groupType);
        }
        catch (Exception exception)
        {
            Debug.LogError("读取 GameView 分组失败: " + exception);
        }
    }

    private static object GetGameViewSizesInstance()
    {
        var sizesType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizes");
        if (sizesType == null)
        {
            throw new MissingMemberException("UnityEditor.GameViewSizes");
        }

        var singleType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
        var instanceProperty = singleType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static);
        if (instanceProperty == null)
        {
            throw new MissingMemberException(singleType.FullName, "instance");
        }

        return instanceProperty.GetValue(null, null);
    }

    private static object GetCurrentGroupType(EditorWindow gameView)
    {
        var gameViewType = gameView.GetType();

        foreach (var propertyName in new[] { "currentSizeGroupType", "currentGameViewSizeGroupType", "sizeGroupType" })
        {
            var property = gameViewType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null)
            {
                var value = property.GetValue(gameView, null);
                if (value != null)
                {
                    return value;
                }
            }
        }

        foreach (var methodName in new[] { "GetCurrentGroupType", "GetCurrentSizeGroupType" })
        {
            var method = gameViewType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method != null)
            {
                var value = method.Invoke(gameView, null);
                if (value != null)
                {
                    return value;
                }
            }
        }

        var sizesType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizes");
        if (sizesType != null)
        {
            foreach (var staticMethodName in new[] { "GetCurrentGroupType", "CurrentGroupType" })
            {
                var method = sizesType.GetMethod(staticMethodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (method != null)
                {
                    var value = method.Invoke(null, null);
                    if (value != null)
                    {
                        return value;
                    }
                }
            }

            var instance = GetGameViewSizesInstance();
            foreach (var propertyName in new[] { "currentGroupType", "currentSizeGroupType" })
            {
                var property = sizesType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null)
                {
                    var value = property.GetValue(instance, null);
                    if (value != null)
                    {
                        return value;
                    }
                }
            }
        }

        return GameViewSizeGroupType.Standalone;
    }

    private static int FindOrCreateSize(object groupType, int width, int height, string label)
    {
        var sizesInstance = GetGameViewSizesInstance();
        var sizesType = sizesInstance.GetType();
        var getGroup = sizesType.GetMethod("GetGroup", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (getGroup == null)
        {
            throw new MissingMemberException(sizesType.FullName, "GetGroup");
        }

        var group = getGroup.Invoke(sizesInstance, new[] { groupType });
        var groupTypeInfo = group.GetType();

        var getBuiltinCount = groupTypeInfo.GetMethod("GetBuiltinCount", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var getCustomCount = groupTypeInfo.GetMethod("GetCustomCount", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var getGameViewSize = groupTypeInfo.GetMethod("GetGameViewSize", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var addCustomSize = groupTypeInfo.GetMethod("AddCustomSize", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (getBuiltinCount == null || getCustomCount == null || getGameViewSize == null || addCustomSize == null)
        {
            throw new MissingMethodException(groupTypeInfo.FullName, "GameView group API");
        }

        var builtinCount = (int)getBuiltinCount.Invoke(group, null);
        var customCount = (int)getCustomCount.Invoke(group, null);
        var totalCount = builtinCount + customCount;

        for (var i = 0; i < totalCount; i++)
        {
            var size = getGameViewSize.Invoke(group, new object[] { i });
            if (MatchesSize(size, width, height, label))
            {
                return i;
            }
        }

        var newSize = CreateSize(width, height, label);
        addCustomSize.Invoke(group, new[] { newSize });
        return totalCount;
    }

    private static object CreateSize(int width, int height, string label)
    {
        var sizeType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSize");
        var sizeEnumType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizeType");
        if (sizeType == null || sizeEnumType == null)
        {
            throw new MissingMemberException("GameViewSize reflection types");
        }

        var ctor = sizeType.GetConstructor(new[] { sizeEnumType, typeof(int), typeof(int), typeof(string) });
        if (ctor == null)
        {
            throw new MissingMethodException(sizeType.FullName, ".ctor");
        }

        var fixedResolutionEnum = Enum.ToObject(sizeEnumType, 1);
        return ctor.Invoke(new object[] { fixedResolutionEnum, width, height, label });
    }

    private static bool MatchesSize(object size, int width, int height, string label)
    {
        var sizeType = size.GetType();
        var widthProperty = sizeType.GetProperty("width", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var heightProperty = sizeType.GetProperty("height", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var baseTextProperty = sizeType.GetProperty("baseText", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (widthProperty == null || heightProperty == null)
        {
            return false;
        }

        var sizeWidth = (int)widthProperty.GetValue(size, null);
        var sizeHeight = (int)heightProperty.GetValue(size, null);
        var baseText = baseTextProperty != null ? baseTextProperty.GetValue(size, null) as string : string.Empty;

        return (sizeWidth == width && sizeHeight == height) || string.Equals(baseText, label, StringComparison.Ordinal);
    }

    private static EditorWindow GetGameViewWindow()
    {
        var gameViewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
        if (gameViewType == null)
        {
            throw new MissingMemberException("UnityEditor.GameView");
        }

        return EditorWindow.GetWindow(gameViewType);
    }

    private static void SetSelectedSize(EditorWindow gameView, int sizeIndex)
    {
        var selectedSizeProperty = gameView.GetType().GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (selectedSizeProperty == null)
        {
            throw new MissingMemberException(gameView.GetType().FullName, "selectedSizeIndex");
        }

        selectedSizeProperty.SetValue(gameView, sizeIndex, null);
    }

    private static void SaveGameViewSizesToDisk()
    {
        var sizesInstance = GetGameViewSizesInstance();
        var saveMethod = sizesInstance.GetType().GetMethod("SaveToHDD", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (saveMethod != null)
        {
            saveMethod.Invoke(sizesInstance, null);
            return;
        }

        var scriptableSingletonType = typeof(ScriptableSingleton<>).MakeGenericType(sizesInstance.GetType());
        var singletonSaveMethod = scriptableSingletonType.GetMethod("Save", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(bool) }, null);
        if (singletonSaveMethod != null)
        {
            singletonSaveMethod.Invoke(sizesInstance, new object[] { true });
        }
    }
}
