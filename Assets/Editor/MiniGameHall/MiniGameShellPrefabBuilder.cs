using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall.Editor
{
    public static class MiniGameShellPrefabBuilder
    {
        private const string PrefabDirectory = "Assets/Common/Resources";
        private const string PrefabPath = PrefabDirectory + "/MiniGameShell.prefab";
        private const string BackgroundSpritePath = "Assets/Common/Resources/HallTheme/hall_bg.png";
        private const string PauseButtonSpritePath = "Assets/Common/Resources/HallTheme/pause_button.png";
        [MenuItem("Tools/小游戏/构建统一壳层预制体")]
        public static void BuildShellPrefab()
        {
            EnsureFolder("Assets/Common/Resources");
            EnsureFolder(PrefabDirectory);

            var backgroundSprite = LoadRequiredSprite(BackgroundSpritePath);
            var pauseButtonSprite = LoadRequiredSprite(PauseButtonSpritePath);

            var root = new GameObject("MiniGameShell", typeof(RectTransform));
            Stretch(root.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            try
            {
                var background = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                background.transform.SetParent(root.transform, false);
                Stretch(background.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var backgroundImage = background.GetComponent<Image>();
                backgroundImage.sprite = backgroundSprite;
                backgroundImage.color = Color.white;
                backgroundImage.raycastTarget = false;

                var topHost = CreateHost("TopHost", root.transform);
                Stretch(topHost, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -MiniGameShellLayout.DefaultTopInset), Vector2.zero);

                var contentHost = CreateHost("ContentHost", root.transform);
                Stretch(contentHost, Vector2.zero, Vector2.one, new Vector2(0f, MiniGameShellLayout.DefaultBottomInset), new Vector2(0f, -MiniGameShellLayout.DefaultTopInset));

                var bottomHost = CreateHost("BottomHost", root.transform);
                Stretch(bottomHost, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, MiniGameShellLayout.DefaultBottomInset));

                var pauseButton = new GameObject(
                    "PauseButton",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Button));
                pauseButton.transform.SetParent(root.transform, false);

                var pauseRect = pauseButton.GetComponent<RectTransform>();
                pauseRect.anchorMin = new Vector2(0f, 1f);
                pauseRect.anchorMax = new Vector2(0f, 1f);
                pauseRect.pivot = new Vector2(0f, 1f);
                pauseRect.anchoredPosition = new Vector2(18f, -18f);
                pauseRect.sizeDelta = new Vector2(64f, 64f);

                var pauseImage = pauseButton.GetComponent<Image>();
                pauseImage.sprite = pauseButtonSprite;
                pauseImage.color = new Color(1f, 1f, 1f, 0.98f);
                pauseImage.type = Image.Type.Simple;
                pauseImage.preserveAspect = true;

                var pauseControl = pauseButton.GetComponent<Button>();
                pauseControl.targetGraphic = pauseImage;
                var colors = pauseControl.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(0.96f, 0.96f, 0.96f, 1f);
                colors.pressedColor = new Color(0.84f, 0.84f, 0.84f, 1f);
                colors.selectedColor = colors.highlightedColor;
                colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.7f);
                pauseControl.colors = colors;

                var popupHost = CreateHost("PopupHost", root.transform);
                Stretch(popupHost, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                popupHost.SetAsLastSibling();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("MiniGame shell prefab generated at " + PrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        public static void BuildShellPrefabFromCommandLine()
        {
            BuildShellPrefab();
        }

        private static RectTransform CreateHost(string name, Transform parent)
        {
            var host = new GameObject(name, typeof(RectTransform));
            var rect = host.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static Sprite LoadRequiredSprite(string assetPath)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null)
            {
                throw new InvalidOperationException("Sprite not found: " + assetPath);
            }

            return sprite;
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
            {
                return;
            }

            var parent = System.IO.Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
            var folderName = System.IO.Path.GetFileName(assetPath);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(folderName))
            {
                throw new InvalidOperationException("Invalid folder path: " + assetPath);
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }

        private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
