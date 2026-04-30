using System;
using FarmPrototype;
using TMPro;
using UnityEngine;

namespace HuanYouYu.MiniGameHall
{
    /// <summary>
    /// StardewAI 游戏视图：纯游戏内容，只保留暂停/返回按钮。
    /// </summary>
    public sealed class GameStardewAIView : MiniGameBase
    {
        public const string GameIdConstant = "stardewai";
        private const float PortraitTopInset = 176f;
        private const float PortraitBottomInset = 116f;
        private const float LandscapeTopInset = 172f;
        private const float LandscapeBottomInset = 156f;
        private const string PreviewNoticeTextKey = "stardewai.preview.notice";

        private GameObject farmRoot;
        private GameObject contentRoot;
        private RectTransform overlayRoot;
        private FarmHudView hudView;
        private FarmPrototypeController farmController;

        public GameStardewAIView(
            MonoBehaviour hostBehaviour,
            Transform parent,
            Action<MiniGameSettlement> onComplete,
            Action onExit)
            : base(GameIdConstant, "GameStardewAIView", hostBehaviour, parent, onComplete, onExit)
        {
        }

        protected override MiniGameShellLayout CreateShellLayout()
        {
            if (Screen.height > Screen.width)
            {
                return new MiniGameShellLayout(PortraitTopInset, PortraitBottomInset, MiniGameShellBottomMode.DefaultSlot);
            }

            return new MiniGameShellLayout(LandscapeTopInset, LandscapeBottomInset, MiniGameShellBottomMode.DefaultSlot);
        }

        protected override void BuildOrBindSections()
        {
            Shell.SetBackgroundVisible(false);
        }

        protected override void ResetGame()
        {
            Shell.ClosePopup();
            DisposeRuntime();

            contentRoot = new GameObject("StardewAIContentRoot", typeof(RectTransform));
            Shell.AttachContent(contentRoot.transform);

            var contentRect = contentRoot.GetComponent<RectTransform>();
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            overlayRoot = new GameObject("FarmOverlayRoot", typeof(RectTransform)).GetComponent<RectTransform>();
            overlayRoot.SetParent(contentRect, false);
            overlayRoot.anchorMin = Vector2.zero;
            overlayRoot.anchorMax = Vector2.one;
            overlayRoot.offsetMin = Vector2.zero;
            overlayRoot.offsetMax = Vector2.zero;

            hudView = FarmHudView.Create(Shell.TopHost, Shell.BottomHost, overlayRoot, MiniGameFontProvider.DefaultFont);
            hudView.ApplyLayout(Screen.safeArea, new Vector2Int(Screen.width, Screen.height));

            farmRoot = new GameObject("FarmPrototype");
            var worldRoot = new GameObject("FarmWorldRoot");
            worldRoot.transform.SetParent(farmRoot.transform, false);

            farmController = farmRoot.AddComponent<FarmPrototypeController>();
            farmController.Initialize(hudView, worldRoot.transform, overlayRoot);

            Shell.ShowInfoPopup(UiTextCatalog.Get(PreviewNoticeTextKey));
        }

        public override void Tick(float deltaTime)
        {
        }

        protected override void OnPauseRequested()
        {
            Shell.ShowPausePopup(ResumeFromPause, ConfirmExitToHall);
        }

        protected override void OnBeforeDispose()
        {
            Shell.ClosePopup();
            DisposeRuntime();
        }

        private void ResumeFromPause()
        {
            Shell.ClosePopup();
        }

        private void ConfirmExitToHall()
        {
            Shell.ClosePopup();
            ExitToHall?.Invoke();
        }

        private void DisposeRuntime()
        {
            if (hudView != null)
            {
                hudView.Dispose();
                hudView = null;
            }

            if (contentRoot != null)
            {
                UnityEngine.Object.Destroy(contentRoot);
                contentRoot = null;
                overlayRoot = null;
            }

            if (farmRoot != null)
            {
                UnityEngine.Object.Destroy(farmRoot);
                farmRoot = null;
                farmController = null;
            }
        }

        protected override (string helpKey, string creditsKey)? GetPauseHelpKeys()
        {
            return null;
        }
    }
}
