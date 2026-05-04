using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    internal sealed class MiniGameShell : IDisposable
    {
        private const string ShellPrefabResourcePath = "MiniGameShell";
        private const string PopupPrefabResourcePath = "MiniGamePopup";
        private const string PausePopupPrefabResourcePath = "MiniGamePausePopup";
        private const float PauseButtonBaseX = 18f;
        private const float PauseButtonBaseY = -18f;
        private const float PauseButtonTopPadding = 12f;

        private readonly Action pauseAction;
        private readonly Func<string> pauseHelpProvider;
        private readonly GameObject backgroundObject;
        private readonly Button pauseButton;
        private readonly RectTransform contentHost;
        private readonly RectTransform bottomHost;
        private IDisposable activePopup;

        public MiniGameShell(Transform parent, string rootName, Action onPause, Func<string> getPauseHelpText)
        {
            pauseAction = onPause;
            pauseHelpProvider = getPauseHelpText;

            var prefab = Resources.Load<GameObject>(ShellPrefabResourcePath);
            if (prefab == null)
            {
                throw new InvalidOperationException("MiniGameShell prefab not found at Resources/" + ShellPrefabResourcePath);
            }

            Root = UnityEngine.Object.Instantiate(prefab, parent, false);
            Root.name = rootName;
            RootTransform = Root.GetComponent<RectTransform>();
            TopHost = Root.transform.Find("TopHost") as RectTransform;
            backgroundObject = Root.transform.Find("Background")?.gameObject;
            contentHost = Root.transform.Find("ContentHost") as RectTransform;
            bottomHost = Root.transform.Find("BottomHost") as RectTransform;
            PopupHost = Root.transform.Find("PopupHost") as RectTransform;
            pauseButton = Root.transform.Find("PauseButton")?.GetComponent<Button>();

            if (RootTransform == null || TopHost == null || backgroundObject == null || contentHost == null || bottomHost == null || PopupHost == null || pauseButton == null)
            {
                UnityEngine.Object.Destroy(Root);
                throw new InvalidOperationException("MiniGameShell prefab structure is incomplete.");
            }

            pauseButton.onClick.RemoveAllListeners();
            pauseButton.onClick.AddListener(OnPauseClicked);
            MiniGameSfxPlayer.Attach(pauseButton, MiniGameSfxType.UiTap, 0.92f);
            ApplyLayout(MiniGameShellLayout.Default);
            BringPauseButtonToFront();
        }

        public GameObject Root { get; }

        public RectTransform RootTransform { get; }

        public RectTransform TopHost { get; }

        public RectTransform ContentHost
        {
            get { return contentHost; }
        }

        public RectTransform BottomHost
        {
            get { return bottomHost; }
        }

        public RectTransform PopupHost { get; }

        public void ApplyLayout(MiniGameShellLayout layout)
        {
            var useBottomSlot = layout.BottomMode == MiniGameShellBottomMode.DefaultSlot;
            bottomHost.gameObject.SetActive(useBottomSlot);
            Stretch(
                TopHost,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, -layout.TopInset),
                Vector2.zero);
            Stretch(
                bottomHost,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                Vector2.zero,
                new Vector2(0f, layout.BottomInset));
            Stretch(
                contentHost,
                Vector2.zero,
                Vector2.one,
                new Vector2(0f, useBottomSlot ? layout.BottomInset : MiniGameShellLayout.ContentOwnedBottomInset),
                new Vector2(0f, -layout.TopInset));
            ApplyPauseButtonLayout();
            if (activePopup == null)
            {
                BringPauseButtonToFront();
            }
        }

        public void ConfigureBottomMode(MiniGameShellBottomMode mode, float bottomInset = MiniGameShellLayout.DefaultBottomInset)
        {
            ApplyLayout(new MiniGameShellLayout(MiniGameShellLayout.DefaultTopInset, bottomInset, mode));
        }

        public void AttachTop(Transform target)
        {
            AttachToHost(target, TopHost);
        }

        public void AttachContent(Transform target)
        {
            AttachToHost(target, contentHost);
        }

        public void AttachBottom(Transform target)
        {
            AttachToHost(target, bottomHost);
        }

        public void SetBackgroundVisible(bool visible)
        {
            backgroundObject.SetActive(visible);
        }

        public void ShowPausePopup(Action onResume, Action onConfirmExit)
        {
            ClosePopup();
            BringPopupHostToFront();

            var pausePopup = MiniGamePausePopupView.Create(PopupHost);
            pausePopup.Bind(
                UiTextCatalog.GetOrFallback("popup.pause.title", "Pause"),
                ResolvePauseHelpText(),
                onResume,
                onConfirmExit);
            activePopup = pausePopup;
        }

        public void ShowInfoPopup(string message, Action onConfirm = null)
        {
            ShowPopup(
                UiTextCatalog.GetOrFallback("common.action.hint", "Tip"),
                message,
                UiTextCatalog.GetOrFallback("common.action.got_it", "Got it"),
                string.Empty,
                false,
                false,
                false,
                delegate
                {
                    ClosePopup();
                    onConfirm?.Invoke();
                },
                null,
                null);
        }

        public void ShowExitConfirmPopup(Action onCancel, Action onConfirm)
        {
            ShowPopup(
                UiTextCatalog.GetOrFallback("popup.exit.title", "Exit"),
                UiTextCatalog.GetOrFallback("popup.exit.message", "Leaving now will settle the current run."),
                UiTextCatalog.GetOrFallback("common.action.confirm_exit", "Exit"),
                UiTextCatalog.GetOrFallback("common.action.continue", "Continue"),
                true,
                true,
                true,
                onConfirm,
                onCancel,
                onCancel);
        }

        public void ShowConfirmPopup(
            string title,
            string message,
            string confirmLabel,
            string cancelLabel,
            Action onCancel,
            Action onConfirm)
        {
            ShowPopup(
                title,
                message,
                confirmLabel,
                cancelLabel,
                true,
                true,
                true,
                onConfirm,
                onCancel,
                onCancel);
        }

        public void ShowSettlementPopup(string message, Action onConfirm, string confirmLabel = null)
        {
            ShowPopup(
                UiTextCatalog.GetOrFallback("popup.settlement.title", "Settlement"),
                message,
                string.IsNullOrWhiteSpace(confirmLabel)
                    ? UiTextCatalog.GetOrFallback("common.action.back_hall", "Back")
                    : confirmLabel,
                string.Empty,
                false,
                false,
                false,
                onConfirm,
                onConfirm,
                null);
        }

        public void ShowSettlementChoicePopup(
            string message,
            string primaryLabel,
            string secondaryLabel,
            Action onPrimary,
            Action onSecondary)
        {
            ShowPopup(
                UiTextCatalog.GetOrFallback("popup.settlement.title", "Settlement"),
                message,
                primaryLabel,
                secondaryLabel,
                true,
                false,
                false,
                onPrimary,
                onSecondary,
                null);
        }

        public void ShowRetryOrExitPopup(string message, Action onRetry, Action onExit)
        {
            ShowPopup(
                UiTextCatalog.GetOrFallback("popup.settlement.title", "Settlement"),
                message,
                UiTextCatalog.Get("common.action.back_hall"),
                UiTextCatalog.Get("common.action.retry"),
                true,
                false,
                false,
                onExit,
                onRetry,
                null);
        }

        public void ClosePopup()
        {
            if (activePopup != null)
            {
                activePopup.Dispose();
                activePopup = null;
            }

            BringPauseButtonToFront();
        }

        public void Dispose()
        {
            ClosePopup();
            if (Root != null)
            {
                UnityEngine.Object.Destroy(Root);
            }
        }

        private void ShowPopup(
            string title,
            string message,
            string confirmLabel,
            string cancelLabel,
            bool showCancel,
            bool showCloseButton,
            bool dismissOnBackdrop,
            Action onConfirm,
            Action onCancel,
            Action onClose)
        {
            ClosePopup();
            BringPopupHostToFront();
            var popupView = MiniGamePopupView.Create(PopupHost);
            popupView.Bind(title, message, confirmLabel, cancelLabel, showCancel, showCloseButton, dismissOnBackdrop, onConfirm, onCancel, onClose);
            activePopup = popupView;
        }

        private void OnPauseClicked()
        {
            pauseAction?.Invoke();
        }

        private string ResolvePauseHelpText()
        {
            var helpMessage = pauseHelpProvider != null ? pauseHelpProvider() : null;
            if (!string.IsNullOrWhiteSpace(helpMessage) && !string.Equals(helpMessage, "?", StringComparison.Ordinal))
            {
                return helpMessage;
            }

            return UiTextCatalog.GetOrFallback("popup.help.fallback", "Help is not available yet.");
        }

        private static void AttachToHost(Transform target, RectTransform host)
        {
            if (target == null || host == null)
            {
                return;
            }

            target.SetParent(host, false);
            target.SetAsLastSibling();
        }

        private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private void ApplyPauseButtonLayout()
        {
            if (pauseButton == null)
            {
                return;
            }

            var buttonRect = pauseButton.transform as RectTransform;
            if (buttonRect == null)
            {
                return;
            }

            var topInset = MiniGameSafeAreaUtility.GetTopInset(RootTransform);
            buttonRect.anchorMin = new Vector2(0f, 1f);
            buttonRect.anchorMax = new Vector2(0f, 1f);
            buttonRect.pivot = new Vector2(0f, 1f);
            buttonRect.anchoredPosition = new Vector2(PauseButtonBaseX, PauseButtonBaseY - topInset - PauseButtonTopPadding);
        }

        private void BringPauseButtonToFront()
        {
            if (pauseButton != null && pauseButton.transform != null)
            {
                pauseButton.transform.SetAsLastSibling();
            }
        }

        private void BringPopupHostToFront()
        {
            if (PopupHost != null)
            {
                PopupHost.SetAsLastSibling();
            }
        }

        private static void BindButton(Button button, Action action, MiniGameSfxType sfxType, float volumeScale)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(delegate
            {
                MiniGameSfxPlayer.Play(sfxType, volumeScale);
                action?.Invoke();
            });
        }

        private sealed class MiniGamePopupView : IDisposable
        {
            private readonly GameObject root;
            private readonly Button blockerButton;
            private readonly Button closeButton;
            private readonly Button cancelButton;
            private readonly Button confirmButton;
            private readonly TextMeshProUGUI titleText;
            private readonly TextMeshProUGUI messageText;
            private readonly TextMeshProUGUI cancelLabelText;
            private readonly TextMeshProUGUI confirmLabelText;

            private MiniGamePopupView(
                GameObject instance,
                Button blocker,
                Button close,
                Button cancel,
                Button confirm,
                TextMeshProUGUI title,
                TextMeshProUGUI message,
                TextMeshProUGUI cancelLabel,
                TextMeshProUGUI confirmLabel)
            {
                root = instance;
                blockerButton = blocker;
                closeButton = close;
                cancelButton = cancel;
                confirmButton = confirm;
                titleText = title;
                messageText = message;
                cancelLabelText = cancelLabel;
                confirmLabelText = confirmLabel;
            }

            public static MiniGamePopupView Create(Transform parent)
            {
                var prefab = Resources.Load<GameObject>(PopupPrefabResourcePath);
                if (prefab == null)
                {
                    throw new InvalidOperationException("MiniGamePopup prefab not found at Resources/" + PopupPrefabResourcePath);
                }

                var instance = UnityEngine.Object.Instantiate(prefab, parent, false);
                instance.name = "MiniGamePopup";

                var blocker = instance.transform.Find("Blocker")?.GetComponent<Button>();
                var close = instance.transform.Find("Dialog/CloseButton")?.GetComponent<Button>();
                var cancel = instance.transform.Find("Dialog/Buttons/CancelButton")?.GetComponent<Button>();
                var confirm = instance.transform.Find("Dialog/Buttons/ConfirmButton")?.GetComponent<Button>();
                var title = instance.transform.Find("Dialog/Title")?.GetComponent<TextMeshProUGUI>();
                var message = instance.transform.Find("Dialog/MessagePanel/Message")?.GetComponent<TextMeshProUGUI>();
                var cancelLabel = instance.transform.Find("Dialog/Buttons/CancelButton/Label")?.GetComponent<TextMeshProUGUI>();
                var confirmLabel = instance.transform.Find("Dialog/Buttons/ConfirmButton/Label")?.GetComponent<TextMeshProUGUI>();
                if (blocker == null || close == null || cancel == null || confirm == null || title == null || message == null || cancelLabel == null || confirmLabel == null)
                {
                    UnityEngine.Object.Destroy(instance);
                    throw new InvalidOperationException("MiniGamePopup prefab structure is incomplete.");
                }

                return new MiniGamePopupView(instance, blocker, close, cancel, confirm, title, message, cancelLabel, confirmLabel);
            }

            public void Bind(
                string title,
                string message,
                string confirmLabel,
                string cancelLabel,
                bool showCancel,
                bool showCloseButton,
                bool dismissOnBackdrop,
                Action onConfirm,
                Action onCancel,
                Action onClose)
            {
                titleText.text = title;
                messageText.text = message;
                confirmLabelText.text = confirmLabel;
                cancelLabelText.text = cancelLabel;

                cancelButton.gameObject.SetActive(showCancel);
                closeButton.gameObject.SetActive(showCloseButton);

                BindButton(confirmButton, onConfirm, MiniGameSfxType.UiTap, 0.95f);

                if (showCancel)
                {
                    if (dismissOnBackdrop)
                    {
                        BindButton(blockerButton, onClose, MiniGameSfxType.UiBack, 0.9f);
                    }
                    else
                    {
                        blockerButton.onClick.RemoveAllListeners();
                    }

                    if (showCloseButton)
                    {
                        BindButton(closeButton, onClose, MiniGameSfxType.UiBack, 0.9f);
                    }
                    else
                    {
                        closeButton.onClick.RemoveAllListeners();
                    }

                    BindButton(cancelButton, onCancel, MiniGameSfxType.UiBack, 0.9f);
                }
                else
                {
                    blockerButton.onClick.RemoveAllListeners();
                    closeButton.onClick.RemoveAllListeners();
                    cancelButton.onClick.RemoveAllListeners();
                }
            }

            public void Dispose()
            {
                if (root != null)
                {
                    UnityEngine.Object.Destroy(root);
                }
            }
        }

        private sealed class MiniGamePausePopupView : IDisposable
        {
            private const float HelpDialogWidth = 660f;
            private const float HelpDialogMinHeight = 380f;
            private const float HelpDialogMaxHeightRatio = 0.68f;
            private const float HelpDialogChromeHeight = 242f;
            private const float HelpMessageHorizontalPadding = 120f;

            private readonly GameObject root;
            private readonly Button blockerButton;
            private readonly Button closeButton;
            private readonly Button helpButton;
            private readonly Button continueButton;
            private readonly Button exitButton;
            private readonly GameObject helpOverlay;
            private readonly Button helpOverlayBlockerButton;
            private readonly Button helpConfirmButton;
            private readonly TextMeshProUGUI titleText;
            private readonly TextMeshProUGUI helpMessageText;
            private readonly RectTransform rootRect;
            private readonly RectTransform helpDialogRect;
            private readonly MiniGamePauseToggleView musicToggle;
            private readonly MiniGamePauseToggleView sfxToggle;
            private readonly MiniGamePauseToggleView vibrationToggle;

            private MiniGamePausePopupView(
                GameObject instance,
                Button blocker,
                Button close,
                Button help,
                Button continueAction,
                Button exitAction,
                GameObject overlay,
                Button overlayBlocker,
                Button overlayConfirm,
                TextMeshProUGUI title,
                TextMeshProUGUI helpMessage,
                RectTransform popupRoot,
                RectTransform helpDialog,
                MiniGamePauseToggleView music,
                MiniGamePauseToggleView sfx,
                MiniGamePauseToggleView vibration)
            {
                root = instance;
                blockerButton = blocker;
                closeButton = close;
                helpButton = help;
                continueButton = continueAction;
                exitButton = exitAction;
                helpOverlay = overlay;
                helpOverlayBlockerButton = overlayBlocker;
                helpConfirmButton = overlayConfirm;
                titleText = title;
                helpMessageText = helpMessage;
                rootRect = popupRoot;
                helpDialogRect = helpDialog;
                musicToggle = music;
                sfxToggle = sfx;
                vibrationToggle = vibration;
            }

            public static MiniGamePausePopupView Create(Transform parent)
            {
                var prefab = Resources.Load<GameObject>(PausePopupPrefabResourcePath);
                if (prefab == null)
                {
                    throw new InvalidOperationException("MiniGamePausePopup prefab not found at Resources/" + PausePopupPrefabResourcePath);
                }

                var instance = UnityEngine.Object.Instantiate(prefab, parent, false);
                instance.name = "MiniGamePausePopup";

                var blocker = instance.transform.Find("Blocker")?.GetComponent<Button>();
                var close = instance.transform.Find("Dialog/CloseButton")?.GetComponent<Button>();
                var help = instance.transform.Find("Dialog/HelpButton")?.GetComponent<Button>();
                var continueAction = instance.transform.Find("Dialog/MainButtons/ContinueButton")?.GetComponent<Button>();
                var exitAction = instance.transform.Find("Dialog/MainButtons/ExitButton")?.GetComponent<Button>();
                var overlay = instance.transform.Find("HelpOverlay")?.gameObject;
                var overlayBlocker = instance.transform.Find("HelpOverlay/Blocker")?.GetComponent<Button>();
                var overlayConfirm = instance.transform.Find("HelpOverlay/Dialog/ConfirmButton")?.GetComponent<Button>();
                var title = instance.transform.Find("Dialog/Title")?.GetComponent<TextMeshProUGUI>();
                var helpMessage = instance.transform.Find("HelpOverlay/Dialog/Message")?.GetComponent<TextMeshProUGUI>();
                var helpDialog = instance.transform.Find("HelpOverlay/Dialog") as RectTransform;
                var instanceRect = instance.GetComponent<RectTransform>();
                var tweenRunner = instance.GetComponent<UiTweenRunner>();
                if (tweenRunner == null)
                {
                    tweenRunner = instance.AddComponent<UiTweenRunner>();
                }

                var music = MiniGamePauseToggleView.Create(instance.transform.Find("Dialog/Settings/MusicRow"), tweenRunner);
                var sfx = MiniGamePauseToggleView.Create(instance.transform.Find("Dialog/Settings/SfxRow"), tweenRunner);
                var vibration = MiniGamePauseToggleView.Create(instance.transform.Find("Dialog/Settings/VibrationRow"), tweenRunner);

                if (blocker == null ||
                    close == null ||
                    help == null ||
                    continueAction == null ||
                    exitAction == null ||
                    overlay == null ||
                    overlayBlocker == null ||
                    overlayConfirm == null ||
                    title == null ||
                    helpMessage == null ||
                    helpDialog == null ||
                    instanceRect == null ||
                    music == null ||
                    sfx == null ||
                    vibration == null)
                {
                    UnityEngine.Object.Destroy(instance);
                    throw new InvalidOperationException("MiniGamePausePopup prefab structure is incomplete.");
                }

                return new MiniGamePausePopupView(
                    instance,
                    blocker,
                    close,
                    help,
                    continueAction,
                    exitAction,
                    overlay,
                    overlayBlocker,
                    overlayConfirm,
                    title,
                    helpMessage,
                    instanceRect,
                    helpDialog,
                    music,
                    sfx,
                    vibration);
            }

            public void Bind(
                string title,
                string helpMessage,
                Action onResume,
                Action onExit)
            {
                titleText.text = title;
                helpMessageText.text = string.IsNullOrWhiteSpace(helpMessage)
                    ? UiTextCatalog.GetOrFallback("popup.help.fallback", "Help is not available yet.")
                    : helpMessage;
                UpdateHelpOverlayLayout();

                helpOverlay.SetActive(false);

                musicToggle.Bind(MiniGameRuntimeSettings.MusicEnabled, false);
                sfxToggle.Bind(MiniGameRuntimeSettings.SfxEnabled, false);
                vibrationToggle.Bind(MiniGameRuntimeSettings.VibrationEnabled, false);

                BindButton(blockerButton, onResume, MiniGameSfxType.UiBack, 0.9f);
                BindButton(closeButton, onResume, MiniGameSfxType.UiBack, 0.9f);
                BindButton(continueButton, onResume, MiniGameSfxType.UiBack, 0.9f);
                BindButton(exitButton, onExit, MiniGameSfxType.UiTap, 0.95f);
                BindButton(helpButton, ShowHelpOverlay, MiniGameSfxType.UiTap, 0.88f);
                BindButton(helpOverlayBlockerButton, HideHelpOverlay, MiniGameSfxType.UiBack, 0.82f);
                BindButton(helpConfirmButton, HideHelpOverlay, MiniGameSfxType.UiTap, 0.86f);

                musicToggle.BindToggle(delegate
                {
                    Toggle(musicToggle);
                    MiniGameRuntimeSettings.SetMusicEnabled(musicToggle.IsOn);
                }, MiniGameSfxType.UiTap, 0.82f);
                sfxToggle.BindToggle(delegate
                {
                    Toggle(sfxToggle);
                    MiniGameRuntimeSettings.SetSfxEnabled(sfxToggle.IsOn);
                }, MiniGameSfxType.UiTap, 0.82f);
                vibrationToggle.BindToggle(delegate
                {
                    Toggle(vibrationToggle);
                    MiniGameRuntimeSettings.SetVibrationEnabled(vibrationToggle.IsOn);
                }, MiniGameSfxType.UiTap, 0.82f);
            }

            public void Dispose()
            {
                if (root != null)
                {
                    UnityEngine.Object.Destroy(root);
                }
            }

            private void ShowHelpOverlay()
            {
                UpdateHelpOverlayLayout();
                helpOverlay.SetActive(true);
            }

            private void HideHelpOverlay()
            {
                helpOverlay.SetActive(false);
            }

            private static void Toggle(MiniGamePauseToggleView toggle)
            {
                if (toggle == null)
                {
                    return;
                }

                toggle.Bind(!toggle.IsOn, true);
            }

            private void UpdateHelpOverlayLayout()
            {
                Canvas.ForceUpdateCanvases();
                EnsureTextMaterial(helpMessageText);
                helpMessageText.ForceMeshUpdate();

                var availableHeight = rootRect.rect.height;
                if (availableHeight <= 0f)
                {
                    var parentRect = root.transform.parent as RectTransform;
                    if (parentRect != null)
                    {
                        availableHeight = parentRect.rect.height;
                    }
                }

                var maxHeight = availableHeight > 0f
                    ? Mathf.Max(HelpDialogMinHeight, availableHeight * HelpDialogMaxHeightRatio)
                    : 760f;
                var contentWidth = Mathf.Max(0f, HelpDialogWidth - HelpMessageHorizontalPadding);
                var preferredHeight = helpMessageText.GetPreferredValues(helpMessageText.text, contentWidth, 0f).y;
                var dialogHeight = Mathf.Clamp(
                    Mathf.Ceil(preferredHeight) + HelpDialogChromeHeight,
                    HelpDialogMinHeight,
                    maxHeight);

                helpDialogRect.sizeDelta = new Vector2(HelpDialogWidth, dialogHeight);
            }

            private static void EnsureTextMaterial(TextMeshProUGUI text)
            {
                if (text == null || text.fontSharedMaterial != null || text.font == null)
                {
                    return;
                }

                var defaultMaterial = text.font.material;
                if (defaultMaterial != null)
                {
                    text.fontSharedMaterial = defaultMaterial;
                }
            }

        }
    }
}
