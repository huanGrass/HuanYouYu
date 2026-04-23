using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Tests
{
    public sealed class MiniGameShellTests
    {
        [Test]
        public void BackgroundVisibilityToggleOnlyAffectsBackgroundNode()
        {
            var rootObject = new GameObject("MiniGameCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            object shell = null;

            try
            {
                shell = CreateShell(rootObject.transform);
                var shellRoot = GetShellRoot(shell);
                Assert.IsNotNull(shellRoot, "MiniGameShell root should exist.");

                var background = shellRoot.transform.Find("Background")?.gameObject;
                var topHost = shellRoot.transform.Find("TopHost")?.gameObject;
                var contentHost = shellRoot.transform.Find("ContentHost")?.gameObject;
                var bottomHost = shellRoot.transform.Find("BottomHost")?.gameObject;
                var popupHost = shellRoot.transform.Find("PopupHost")?.gameObject;
                var pauseButton = shellRoot.transform.Find("PauseButton")?.gameObject;

                Assert.IsNotNull(background, "Background should exist.");
                Assert.IsNotNull(topHost, "TopHost should exist.");
                Assert.IsNotNull(contentHost, "ContentHost should exist.");
                Assert.IsNotNull(bottomHost, "BottomHost should exist.");
                Assert.IsNotNull(popupHost, "PopupHost should exist.");
                Assert.IsNotNull(pauseButton, "PauseButton should exist.");
                Assert.IsTrue(background.activeSelf, "Background should be visible by default.");

                InvokeShellMethod(shell, "SetBackgroundVisible", false);

                Assert.IsFalse(background.activeSelf, "Background should be hidden after calling SetBackgroundVisible(false).");
                Assert.IsTrue(topHost.activeSelf, "TopHost should remain active.");
                Assert.IsTrue(contentHost.activeSelf, "ContentHost should remain active.");
                Assert.IsTrue(bottomHost.activeSelf, "BottomHost should remain active.");
                Assert.IsTrue(popupHost.activeSelf, "PopupHost should remain active.");
                Assert.IsTrue(pauseButton.activeSelf, "PauseButton should remain active.");

                InvokeShellMethod(shell, "SetBackgroundVisible", true);
                Assert.IsTrue(background.activeSelf, "Background should be visible again after calling SetBackgroundVisible(true).");
            }
            finally
            {
                DisposeShell(shell);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void PauseButtonSitsLowerThanTheVeryTopEdge()
        {
            var rootObject = new GameObject("MiniGameCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            object shell = null;

            try
            {
                shell = CreateShell(rootObject.transform);
                var shellRoot = GetShellRoot(shell);
                Assert.IsNotNull(shellRoot, "MiniGameShell root should exist.");

                var pauseButton = shellRoot.transform.Find("PauseButton")?.GetComponent<RectTransform>();
                Assert.IsNotNull(pauseButton, "PauseButton should exist.");
                Assert.LessOrEqual(pauseButton.anchoredPosition.y, -28f, "PauseButton should be shifted down to avoid the top edge.");
            }
            finally
            {
                DisposeShell(shell);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void LevelSelectWithManyLevelsUsesMaskedScrollViewport()
        {
            var rootObject = new GameObject("MiniGameCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            object levelSelect = null;

            try
            {
                levelSelect = CreateLevelSelect(rootObject.transform, 40);
                var panel = rootObject.transform.Find("LevelSelectTestRoot");
                Assert.IsNotNull(panel, "Level select root should exist.");

                var viewport = panel.Find("Dialog/LevelViewport");
                Assert.IsNotNull(viewport, "Level select should create a viewport.");
                Assert.IsNotNull(viewport.GetComponent<RectMask2D>(), "Level viewport should mask overflowing level buttons.");

                var scrollRect = viewport.GetComponent<ScrollRect>();
                Assert.IsNotNull(scrollRect, "Level viewport should be scrollable.");
                Assert.IsFalse(scrollRect.horizontal, "Level select should not scroll horizontally.");
                Assert.IsTrue(scrollRect.vertical, "Level select should scroll vertically.");

                var content = viewport.Find("LevelGrid") as RectTransform;
                Assert.IsNotNull(content, "Level grid content should exist.");
                Assert.AreSame(content, scrollRect.content, "ScrollRect should use LevelGrid as content.");
                Assert.Greater(content.rect.height, ((RectTransform)viewport).rect.height, "Many levels should make the grid taller than the visible viewport.");

                var grid = content.GetComponent<GridLayoutGroup>();
                Assert.IsNotNull(grid, "Level grid should use a GridLayoutGroup.");
                Assert.AreEqual(GridLayoutGroup.Constraint.FixedColumnCount, grid.constraint, "Level grid should use a fixed column count.");
                Assert.AreEqual(5, grid.constraintCount, "Level grid should show five levels per row.");
            }
            finally
            {
                DisposeDisposable(levelSelect);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        private static object CreateShell(Transform parent)
        {
            var shellType = GetShellType();
            var constructor = shellType.GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                null,
                new[] { typeof(Transform), typeof(string), typeof(Action), typeof(Func<string>) },
                null);

            Assert.IsNotNull(constructor, "MiniGameShell constructor was not found.");
            return constructor.Invoke(new object[] { parent, "MiniGameShellTestRoot", null, null });
        }

        private static object CreateLevelSelect(Transform parent, int levelCount)
        {
            var levelSelectType = GetLevelSelectType();
            var createMethod = levelSelectType.GetMethod("Create", BindingFlags.Static | BindingFlags.Public);
            Assert.IsNotNull(createMethod, "MiniGameLevelSelectView.Create method was not found.");

            return createMethod.Invoke(
                null,
                new object[]
                {
                    parent,
                    null,
                    levelCount,
                    0,
                    levelCount,
                    "LevelSelectTestRoot",
                    "LevelButton_",
                    null,
                    null
                });
        }

        private static GameObject GetShellRoot(object shell)
        {
            if (shell == null)
            {
                return null;
            }

            var property = GetShellType().GetProperty("Root", BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(property, "MiniGameShell.Root property was not found.");
            return property.GetValue(shell) as GameObject;
        }

        private static void InvokeShellMethod(object shell, string methodName, bool visible)
        {
            var method = GetShellType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(method, "MiniGameShell method was not found: " + methodName);
            method.Invoke(shell, new object[] { visible });
        }

        private static void DisposeShell(object shell)
        {
            if (shell == null)
            {
                return;
            }

            var disposeMethod = GetShellType().GetMethod("Dispose", BindingFlags.Instance | BindingFlags.Public);
            if (disposeMethod != null)
            {
                disposeMethod.Invoke(shell, null);
            }
        }

        private static void DisposeDisposable(object disposable)
        {
            if (disposable == null)
            {
                return;
            }

            var disposeMethod = disposable.GetType().GetMethod("Dispose", BindingFlags.Instance | BindingFlags.Public);
            if (disposeMethod != null)
            {
                disposeMethod.Invoke(disposable, null);
            }
        }

        private static Type GetShellType()
        {
            var runtimeAssembly = typeof(HuanYouYu.MiniGameHall.MiniGameShellLayout).Assembly;
            var shellType = runtimeAssembly.GetType("HuanYouYu.MiniGameHall.MiniGameShell", true);
            Assert.IsNotNull(shellType, "MiniGameShell type was not found.");
            return shellType;
        }

        private static Type GetLevelSelectType()
        {
            var runtimeAssembly = typeof(HuanYouYu.MiniGameHall.MiniGameShellLayout).Assembly;
            var levelSelectType = runtimeAssembly.GetType("HuanYouYu.MiniGameHall.MiniGameLevelSelectView", true);
            Assert.IsNotNull(levelSelectType, "MiniGameLevelSelectView type was not found.");
            return levelSelectType;
        }
    }
}
