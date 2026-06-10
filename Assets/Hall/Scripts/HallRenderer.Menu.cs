using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    internal sealed partial class HallRenderer
    {
        private const string MenuButtonSpritePath = "HallTheme/menu_button";
        private const string MenuAboutGameKey = "hall.menu.about_game";
        private const string MenuSettingsKey = "hall.menu.settings";
        private const string MenuGameClubKey = "hall.menu.game_club";
        private const string MenuShareKey = "hall.menu.share";
        private const string ShareTitleKey = "hall.share.title";
        private const string AboutPopupTitleKey = "hall.about.title";
        private const string AboutPopupMessageKey = "hall.about.message";
        private const string SettingsPopupTitleKey = "hall.settings.title";
        private const string SettingsMusicKey = "hall.settings.music";
        private const string SettingsSfxKey = "hall.settings.sfx";
        private const string SettingsVibrationKey = "hall.settings.vibration";
        private const string AboutPopupResourcePath = "MiniGamePausePopup";
        private const string SettingsPopupResourcePath = "MiniGamePausePopup";
        private const float HeaderMenuButtonBaseX = 52f;
        private const float HeaderMenuButtonBaseY = -56f;
        private const float HeaderMenuPanelBaseX = 16f;
        private const float HeaderMenuPanelBaseY = -104f;
        private const float HeaderMenuTopPadding = 12f;
        private const float HeaderMenuPanelWidth = 262f;
        private const float HeaderMenuPanelHeight = 324f;

        private RectTransform headerMenuRoot;
        private RectTransform headerMenuPanelRoot;
        private Button headerMenuButton;
        private Button headerMenuBackdropButton;
        private WeChatWASM.WXGameClubButton wechatGameClubButton;
        private GameObject activeModalRoot;
        private UiTweenRunner settingsPopupTweenRunner;
        private TextMeshProUGUI settingsMusicValueText;
        private TextMeshProUGUI settingsSfxValueText;
        private TextMeshProUGUI settingsVibrationValueText;

        private void EnsureHeaderMenu(Transform shell)
        {
            if (shell == null)
            {
                return;
            }

            CleanupLegacyHeaderMenu(shell);

            if (headerMenuRoot == null)
            {
                headerMenuRoot = shell != null ? shell.Find("HeaderMenu") as RectTransform : null;
            }

            if (headerMenuRoot == null)
            {
                headerMenuRoot = CreateHeaderMenu(shell);
            }

            if (headerMenuRoot == null)
            {
                return;
            }

            Stretch(headerMenuRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            headerMenuButton = headerMenuRoot.Find("MenuButton")?.GetComponent<Button>();
            var headerMenuButtonImage = headerMenuRoot.Find("MenuButton")?.GetComponent<Image>();
            var headerMenuButtonRect = headerMenuRoot.Find("MenuButton") as RectTransform;
            var headerMenuButtonLayout = headerMenuRoot.Find("MenuButton")?.GetComponent<LayoutElement>();
            headerMenuPanelRoot = headerMenuRoot.Find("MenuPanel") as RectTransform;
            headerMenuBackdropButton = headerMenuRoot.Find("Backdrop")?.GetComponent<Button>();
            var headerMenuBackdropImage = headerMenuRoot.Find("Backdrop")?.GetComponent<Image>();

            if (headerMenuButtonImage != null)
            {
                headerMenuButtonImage.raycastTarget = true;
            }

            if (headerMenuBackdropImage != null)
            {
                headerMenuBackdropImage.color = new Color(0f, 0f, 0f, 0.56f);
                headerMenuBackdropImage.raycastTarget = true;
            }

            if (headerMenuButtonRect != null)
            {
                headerMenuButtonRect.anchorMin = new Vector2(0f, 1f);
                headerMenuButtonRect.anchorMax = new Vector2(0f, 1f);
                headerMenuButtonRect.pivot = new Vector2(0.5f, 0.5f);
                headerMenuButtonRect.anchoredPosition = new Vector2(HeaderMenuButtonBaseX, HeaderMenuButtonBaseY);
                headerMenuButtonRect.sizeDelta = new Vector2(80f, 80f);
            }

            if (headerMenuButtonLayout != null)
            {
                headerMenuButtonLayout.preferredWidth = 80f;
                headerMenuButtonLayout.preferredHeight = 80f;
            }

            ApplyHeaderMenuLayout();
            EnsureMenuPanelBackground(headerMenuPanelRoot);
            EnsureMenuPanelEntries(headerMenuPanelRoot);

            if (headerMenuButton != null)
            {
                headerMenuButton.onClick.RemoveAllListeners();
                headerMenuButton.onClick.AddListener(ToggleHeaderMenuPanel);
                MiniGameSfxPlayer.Attach(headerMenuButton, MiniGameSfxType.UiTap, 0.68f);
                EnsureMenuButtonPressEffect(headerMenuButton.transform);
            }

            if (headerMenuBackdropButton != null)
            {
                headerMenuBackdropButton.onClick.RemoveAllListeners();
                headerMenuBackdropButton.onClick.AddListener(HideHeaderMenuPanel);
            }

            var aboutButton = headerMenuPanelRoot != null ? headerMenuPanelRoot.Find("AboutGameButton")?.GetComponent<Button>() : null;
            var settingsButton = headerMenuPanelRoot != null ? headerMenuPanelRoot.Find("SettingsButton")?.GetComponent<Button>() : null;
            var gameClubButton = headerMenuPanelRoot != null ? headerMenuPanelRoot.Find("GameClubButton")?.GetComponent<Button>() : null;
            var shareButton = headerMenuPanelRoot != null ? headerMenuPanelRoot.Find("ShareButton")?.GetComponent<Button>() : null;
            BindMenuEntryButton(aboutButton, MenuAboutGameKey, ShowAboutGamePopup);
            BindMenuEntryButton(settingsButton, MenuSettingsKey, ShowSettingsPopup);
            BindMenuEntryButton(gameClubButton, MenuGameClubKey, ShowWechatGameClub);
            BindMenuEntryButton(shareButton, MenuShareKey, ShareGameToWechatFriend);

            HideHeaderMenuPanel();
        }

        private void CloseHeaderMenu()
        {
            HideHeaderMenuPanel();
        }

        private void HideHeaderMenuPanel()
        {
            if (headerMenuPanelRoot != null)
            {
                headerMenuPanelRoot.gameObject.SetActive(false);
            }

            if (headerMenuBackdropButton != null)
            {
                headerMenuBackdropButton.gameObject.SetActive(false);
            }

            HideWechatGameClubButton();
        }

        private void ToggleHeaderMenuPanel()
        {
            if (headerMenuPanelRoot == null)
            {
                return;
            }

            ApplyHeaderMenuLayout();

            if (headerMenuPanelRoot.gameObject.activeSelf)
            {
                HideHeaderMenuPanel();
                return;
            }

            if (activeModalRoot != null)
            {
                CloseActiveModal();
            }

            headerMenuPanelRoot.gameObject.SetActive(true);
            if (headerMenuBackdropButton != null)
            {
                headerMenuBackdropButton.gameObject.SetActive(true);
            }

            headerMenuRoot?.SetAsLastSibling();
            ShowWechatGameClubButton();
        }

        private void CloseActiveModal()
        {
            if (activeModalRoot != null)
            {
                UnityEngine.Object.Destroy(activeModalRoot);
                activeModalRoot = null;
            }

            settingsPopupTweenRunner = null;
            settingsMusicValueText = null;
            settingsSfxValueText = null;
            settingsVibrationValueText = null;
        }

        private void BindMenuEntryButton(Button button, string textKey, Action onClick)
        {
            if (button == null)
            {
                return;
            }

            var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                label.text = UiTextCatalog.Get(textKey);
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(delegate
            {
                HideHeaderMenuPanel();
                onClick?.Invoke();
            });
            MiniGameSfxPlayer.Attach(button, MiniGameSfxType.UiTap, 0.68f);
        }

        private void ShowAboutGamePopup()
        {
            CloseActiveModal();
            activeModalRoot = CreateAnnouncementPopup();
        }

        private void ShowSettingsPopup()
        {
            CloseActiveModal();
            activeModalRoot = CreateSettingsPopupFromPrefab();
        }

        private void EnsureMenuPanelEntries(RectTransform panelRoot)
        {
            if (panelRoot == null)
            {
                return;
            }

            panelRoot.sizeDelta = new Vector2(HeaderMenuPanelWidth, HeaderMenuPanelHeight);
            var panelLayout = panelRoot.GetComponent<LayoutElement>();
            if (panelLayout != null)
            {
                panelLayout.preferredWidth = HeaderMenuPanelWidth;
                panelLayout.preferredHeight = HeaderMenuPanelHeight;
            }

            if (panelRoot.Find("ShareButton") == null)
            {
                CreateMenuEntryButton(panelRoot, "ShareButton", MenuShareKey);
            }

            if (panelRoot.Find("GameClubButton") == null)
            {
                CreateMenuEntryButton(panelRoot, "GameClubButton", MenuGameClubKey);
            }

            var gameClubButton = panelRoot.Find("GameClubButton");
            var shareButton = panelRoot.Find("ShareButton");
            if (gameClubButton != null && shareButton != null)
            {
                gameClubButton.SetSiblingIndex(Mathf.Max(0, shareButton.GetSiblingIndex()));
            }
        }

        private void ShowWechatGameClub()
        {
            if (Application.isEditor)
            {
                Debug.Log("微信游戏圈仅在微信小游戏环境下可用。");
                return;
            }

            ShowWechatGameClubButton();
        }

        private void ShowWechatGameClubButton()
        {
            if (Application.isEditor)
            {
                return;
            }

            var gameClubButtonRect = headerMenuPanelRoot != null ? headerMenuPanelRoot.Find("GameClubButton") as RectTransform : null;
            if (gameClubButtonRect == null)
            {
                return;
            }

            try
            {
                Canvas.ForceUpdateCanvases();
                var style = CreateWechatGameClubButtonStyle(gameClubButtonRect);
                DestroyWechatGameClubButton();
                wechatGameClubButton = WeChatWASM.WXSDKManagerHandler.Instance.CreateGameClubButton(new WeChatWASM.WXCreateGameClubButtonParam
                {
                    type = WeChatWASM.GameClubButtonType.text,
                    text = string.Empty,
                    style = style,
                    styleRaw = JsonUtility.ToJson(style),
                    icon = WeChatWASM.GameClubButtonIcon.green
                });
                wechatGameClubButton?.Show();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("微信游戏圈按钮创建失败: " + exception.Message);
            }
        }

        private void HideWechatGameClubButton()
        {
            if (wechatGameClubButton == null)
            {
                return;
            }

            try
            {
                wechatGameClubButton.Hide();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("微信游戏圈按钮隐藏失败: " + exception.Message);
            }
        }

        private void DestroyWechatGameClubButton()
        {
            if (wechatGameClubButton == null)
            {
                return;
            }

            try
            {
                wechatGameClubButton.Destroy();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("微信游戏圈按钮销毁失败: " + exception.Message);
            }
            finally
            {
                wechatGameClubButton = null;
            }
        }

        private static WeChatWASM.GameClubButtonStyle CreateWechatGameClubButtonStyle(RectTransform buttonRect)
        {
            var corners = new Vector3[4];
            buttonRect.GetWorldCorners(corners);
            var bottomLeft = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            var topRight = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
            var width = Mathf.Max(1f, topRight.x - bottomLeft.x);
            var height = Mathf.Max(1f, topRight.y - bottomLeft.y);
            var screenWidth = Mathf.Max(1f, Screen.width);
            var screenHeight = Mathf.Max(1f, Screen.height);

            try
            {
                var windowInfo = WeChatWASM.WXSDKManagerHandler.GetWindowInfo();
                if (windowInfo != null && windowInfo.windowWidth > 0 && windowInfo.windowHeight > 0)
                {
                    var scaleX = (float)(windowInfo.windowWidth / screenWidth);
                    var scaleY = (float)(windowInfo.windowHeight / screenHeight);
                    bottomLeft = new Vector2(bottomLeft.x * scaleX, bottomLeft.y * scaleY);
                    topRight = new Vector2(topRight.x * scaleX, topRight.y * scaleY);
                    width = Mathf.Max(1f, topRight.x - bottomLeft.x);
                    height = Mathf.Max(1f, topRight.y - bottomLeft.y);
                    screenHeight = (float)windowInfo.windowHeight;
                }
            }
            catch (Exception)
            {
            }

            return new WeChatWASM.GameClubButtonStyle
            {
                left = Mathf.RoundToInt(bottomLeft.x),
                top = Mathf.RoundToInt(screenHeight - topRight.y),
                width = Mathf.RoundToInt(width),
                height = Mathf.RoundToInt(height),
                backgroundColor = "rgba(255,255,255,0)",
                borderColor = "rgba(255,255,255,0)",
                borderWidth = 0,
                borderRadius = 18,
                color = "rgba(255,255,255,0)",
                textAlign = WeChatWASM.GameClubButtonTextAlign.left,
                fontSize = 1,
                lineHeight = Mathf.RoundToInt(height)
            };
        }

        private void ShareGameToWechatFriend()
        {
            if (Application.isEditor)
            {
                Debug.Log("微信好友分享仅在微信小游戏环境下可用。");
                return;
            }

            try
            {
                WeChatWASM.WXSDKManagerHandler.Instance.ShareAppMessage(new WeChatWASM.ShareAppMessageOption
                {
                    title = UiTextCatalog.Get(ShareTitleKey)
                });
            }
            catch (Exception exception)
            {
                Debug.LogWarning("微信好友分享调用失败: " + exception.Message);
            }
        }

        private GameObject CreateAboutPopupFromPrefab()
        {
            var root = CreatePopupInstance(AboutPopupResourcePath, "AboutPopup");
            if (root == null)
            {
                return null;
            }

            SetPopupText(root.transform, "Dialog/Title", UiTextCatalog.Get(AboutPopupTitleKey));
            SetPopupActive(root.transform, "Dialog", false);
            SetPopupActive(root.transform, "HelpOverlay", true);
            SetPopupText(root.transform, "HelpOverlay/Dialog/Title", UiTextCatalog.Get(AboutPopupTitleKey));
            SetPopupText(
                root.transform,
                "HelpOverlay/Dialog/Message",
                UiTextCatalog.Get(AboutPopupMessageKey));
            SetPopupText(root.transform, "HelpOverlay/Dialog/ConfirmButton/Label", UiTextCatalog.Get("common.action.got_it"));
            ApplyAboutPopupLayout(root.transform);
            ConfigurePopupButton(root.transform, "Blocker", CloseActiveModal);
            ConfigurePopupButton(root.transform, "HelpOverlay/Blocker", CloseActiveModal);
            ConfigurePopupButton(root.transform, "HelpOverlay/Dialog/ConfirmButton", CloseActiveModal);

            return root;
        }

        private GameObject CreateSettingsPopupFromPrefab()
        {
            var root = CreatePopupInstance(SettingsPopupResourcePath, "SettingsPopup");
            if (root == null)
            {
                return null;
            }

            SetPopupText(root.transform, "Dialog/Title", UiTextCatalog.Get(SettingsPopupTitleKey));
            SetPopupActive(root.transform, "Dialog/MainButtons", false);
            SetPopupActive(root.transform, "Dialog/HelpButton", false);
            SetPopupActive(root.transform, "HelpOverlay", false);
            SetPopupActive(root.transform, "Dialog/TopDivider", false);
            SetPopupActive(root.transform, "Dialog/BottomDivider", false);
            ConfigurePopupButton(root.transform, "Blocker", CloseActiveModal);
            ConfigurePopupButton(root.transform, "Dialog/CloseButton", CloseActiveModal);
            settingsPopupTweenRunner = root.GetComponent<UiTweenRunner>();
            if (settingsPopupTweenRunner == null)
            {
                settingsPopupTweenRunner = root.AddComponent<UiTweenRunner>();
            }

            ConfigureSettingsRow(
                root.transform,
                "Dialog/Settings/MusicRow",
                SettingsMusicKey,
                MiniGameRuntimeSettings.MusicEnabled,
                delegate(bool enabled) { MiniGameRuntimeSettings.SetMusicEnabled(enabled); });
            ConfigureSettingsRow(
                root.transform,
                "Dialog/Settings/SfxRow",
                SettingsSfxKey,
                MiniGameRuntimeSettings.SfxEnabled,
                delegate(bool enabled) { MiniGameRuntimeSettings.SetSfxEnabled(enabled); });
            ConfigureSettingsRow(
                root.transform,
                "Dialog/Settings/VibrationRow",
                SettingsVibrationKey,
                MiniGameRuntimeSettings.VibrationEnabled,
                delegate(bool enabled) { MiniGameRuntimeSettings.SetVibrationEnabled(enabled); });

            return root;
        }

        private GameObject CreatePopupInstance(string resourcePath, string rootName)
        {
            if (overlayRoot == null)
            {
                overlayRoot = EnsureOverlayRoot();
            }

            if (overlayRoot == null)
            {
                return null;
            }

            var prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab == null)
            {
                return null;
            }

            var instance = UnityEngine.Object.Instantiate(prefab, overlayRoot, false);
            instance.name = rootName;
            instance.transform.SetAsLastSibling();
            return instance;
        }

        private void ConfigurePopupButton(Transform root, string path, Action onClick)
        {
            var button = FindButton(root, path);
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(delegate
            {
                onClick?.Invoke();
            });
            MiniGameSfxPlayer.Attach(button, MiniGameSfxType.UiTap, 0.66f);
        }

        private void ConfigureSettingsRow(Transform root, string rowPath, string textKey, bool enabled, Action<bool> onChanged)
        {
            var title = FindText(root, rowPath + "/Title");
            if (title != null)
            {
                title.text = UiTextCatalog.Get(textKey);
            }

            var toggleView = MiniGamePauseToggleView.Create(root.Find(rowPath), settingsPopupTweenRunner);
            if (toggleView == null)
            {
                return;
            }

            toggleView.Bind(enabled, false);
            toggleView.BindToggle(delegate
            {
                var currentEnabled = !toggleView.IsOn;
                onChanged?.Invoke(currentEnabled);
                toggleView.Bind(currentEnabled, true);
            }, MiniGameSfxType.UiTap, 0.66f);
        }

        private static void SetPopupActive(Transform root, string path, bool active)
        {
            var target = root != null ? root.Find(path) : null;
            if (target != null)
            {
                target.gameObject.SetActive(active);
            }
        }

        private static void SetPopupText(Transform root, string path, string content)
        {
            var text = FindText(root, path);
            if (text != null)
            {
                text.text = content;
            }
        }

        private static TextMeshProUGUI FindText(Transform root, string path)
        {
            var target = root != null ? root.Find(path) : null;
            return target != null ? target.GetComponent<TextMeshProUGUI>() : null;
        }

        private static Button FindButton(Transform root, string path)
        {
            var target = root != null ? root.Find(path) : null;
            return target != null ? target.GetComponent<Button>() : null;
        }

        private static RectTransform FindRectTransform(Transform root, string path)
        {
            var target = root != null ? root.Find(path) : null;
            return target as RectTransform;
        }

        private static RoundedRectGraphic FindRoundedRectGraphic(Transform root, string path)
        {
            var target = root != null ? root.Find(path) : null;
            return target != null ? target.GetComponent<RoundedRectGraphic>() : null;
        }

        private RectTransform CreateHeaderMenu(Transform parent)
        {
            var root = new GameObject("HeaderMenu", typeof(RectTransform));
            root.transform.SetParent(parent, false);

            var rootRect = root.GetComponent<RectTransform>();
            Stretch(rootRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var backdrop = new GameObject(
                "Backdrop",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            backdrop.transform.SetParent(root.transform, false);
            Stretch(backdrop.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var backdropImage = backdrop.GetComponent<Image>();
            backdropImage.color = new Color(0f, 0f, 0f, 0.56f);
            backdropImage.raycastTarget = true;
            backdrop.GetComponent<Button>().transition = Selectable.Transition.None;
            backdrop.GetComponent<Button>().targetGraphic = backdropImage;

            var button = CreateImage("MenuButton", LoadSprite(MenuButtonSpritePath), true);
            button.transform.SetParent(root.transform, false);
            var buttonRect = button.rectTransform;
            buttonRect.anchorMin = new Vector2(0f, 1f);
            buttonRect.anchorMax = new Vector2(0f, 1f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.anchoredPosition = new Vector2(HeaderMenuButtonBaseX, HeaderMenuButtonBaseY);
            buttonRect.sizeDelta = new Vector2(80f, 80f);
            var buttonControl = button.gameObject.AddComponent<Button>();
            buttonControl.transition = Selectable.Transition.None;
            buttonControl.targetGraphic = button;
            var buttonLayout = button.gameObject.AddComponent<LayoutElement>();
            buttonLayout.preferredWidth = 80f;
            buttonLayout.preferredHeight = 80f;

            var panel = new GameObject(
                "MenuPanel",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RoundedRectGraphic),
                typeof(Shadow),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter),
                typeof(LayoutElement));
            panel.transform.SetParent(root.transform, false);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(HeaderMenuPanelBaseX, HeaderMenuPanelBaseY);
            panelRect.sizeDelta = new Vector2(HeaderMenuPanelWidth, HeaderMenuPanelHeight);
            panel.SetActive(false);

            var panelGraphic = panel.GetComponent<RoundedRectGraphic>();
            panelGraphic.color = Color.white;
            panelGraphic.CornerRadius = 26f;
            panelGraphic.raycastTarget = true;

            var panelShadow = panel.GetComponent<Shadow>();
            panelShadow.effectColor = new Color(0.31f, 0.29f, 0.21f, 0.16f);
            panelShadow.effectDistance = new Vector2(0f, -5f);

            var panelLayout = panel.GetComponent<VerticalLayoutGroup>();
            panelLayout.padding = new RectOffset(18, 18, 18, 18);
            panelLayout.spacing = 12f;
            panelLayout.childAlignment = TextAnchor.UpperCenter;
            panelLayout.childControlWidth = true;
            panelLayout.childControlHeight = false;
            panelLayout.childForceExpandWidth = true;
            panelLayout.childForceExpandHeight = false;

            var fitter = panel.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            panel.GetComponent<LayoutElement>().preferredWidth = HeaderMenuPanelWidth;
            panel.GetComponent<LayoutElement>().preferredHeight = HeaderMenuPanelHeight;

            CreateMenuEntryButton(panel.transform, "AboutGameButton", MenuAboutGameKey);
            CreateMenuEntryButton(panel.transform, "SettingsButton", MenuSettingsKey);
            CreateMenuEntryButton(panel.transform, "GameClubButton", MenuGameClubKey);
            CreateMenuEntryButton(panel.transform, "ShareButton", MenuShareKey);

            return rootRect;
        }

        private void ApplyHeaderMenuLayout()
        {
            if (headerMenuRoot == null)
            {
                return;
            }

            var shellRect = headerMenuRoot.parent as RectTransform;
            var topInset = MiniGameSafeAreaUtility.GetTopInset(shellRect) + HeaderMenuTopPadding;

            var buttonRect = headerMenuRoot.Find("MenuButton") as RectTransform;
            if (buttonRect != null)
            {
                buttonRect.anchoredPosition = new Vector2(HeaderMenuButtonBaseX, HeaderMenuButtonBaseY - topInset);
            }

            if (headerMenuPanelRoot != null)
            {
                headerMenuPanelRoot.anchoredPosition = new Vector2(HeaderMenuPanelBaseX, HeaderMenuPanelBaseY - topInset);
            }
        }

        private void CreateMenuEntryButton(Transform parent, string name, string textKey)
        {
            var button = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RoundedRectGraphic),
                typeof(Button),
                typeof(LayoutElement));
            button.transform.SetParent(parent, false);

            var buttonRect = button.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0f, 0.5f);
            buttonRect.anchorMax = new Vector2(1f, 0.5f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.sizeDelta = new Vector2(0f, 60f);

            var background = button.GetComponent<RoundedRectGraphic>();
            background.color = new Color(1f, 0.99f, 0.96f, 1f);
            background.CornerRadius = 18f;
            background.raycastTarget = true;

            var buttonControl = button.GetComponent<Button>();
            buttonControl.transition = Selectable.Transition.None;
            buttonControl.targetGraphic = background;

            var label = CreateButtonLabel("Label", UiTextCatalog.Get(textKey), 24f);
            label.transform.SetParent(button.transform, false);
            var labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.offsetMin = new Vector2(18f, 0f);
            labelRect.offsetMax = new Vector2(-18f, 0f);
            label.alignment = TextAlignmentOptions.MidlineLeft;

            button.GetComponent<LayoutElement>().preferredHeight = 60f;
        }

        private static Image CreateImage(string name, Sprite sprite, bool preserveAspect)
        {
            var imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var image = imageObject.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = preserveAspect;
            image.raycastTarget = true;
            return image;
        }

        private static Sprite LoadSprite(string resourcePath)
        {
            var sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite == null)
            {
                throw new InvalidOperationException("Sprite not found at Resources/" + resourcePath);
            }

            return sprite;
        }

        private void EnsureMenuPanelBackground(RectTransform panelRoot)
        {
            if (panelRoot == null)
            {
                return;
            }

            var image = panelRoot.GetComponent<Image>();
            if (image != null)
            {
                image.raycastTarget = false;
                UnityEngine.Object.DestroyImmediate(image);
            }

            var graphic = panelRoot.GetComponent<RoundedRectGraphic>();
            if (graphic == null)
            {
                graphic = panelRoot.gameObject.AddComponent<RoundedRectGraphic>();
            }

            graphic.color = Color.white;
            graphic.CornerRadius = 26f;
            graphic.raycastTarget = true;

            var button = panelRoot.GetComponent<Button>();
            if (button == null)
            {
                button = panelRoot.gameObject.AddComponent<Button>();
            }

            button.transition = Selectable.Transition.None;
            button.targetGraphic = graphic;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(HideHeaderMenuPanel);

            var shadow = panelRoot.GetComponent<Shadow>();
            if (shadow == null)
            {
                shadow = panelRoot.gameObject.AddComponent<Shadow>();
            }

            shadow.effectColor = new Color(0.31f, 0.29f, 0.21f, 0.16f);
            shadow.effectDistance = new Vector2(0f, -5f);
        }

        private static void EnsureMenuButtonPressEffect(Transform buttonTransform)
        {
            if (buttonTransform == null)
            {
                return;
            }

            var effect = buttonTransform.GetComponent<MenuButtonPressEffect>();
            if (effect == null)
            {
                effect = buttonTransform.gameObject.AddComponent<MenuButtonPressEffect>();
            }

            effect.Configure(0.92f);
        }

        private void ApplyAboutPopupLayout(Transform root)
        {
            var title = FindText(root, "HelpOverlay/Dialog/Title");
            if (title != null)
            {
                title.fontSize = 28f;
                title.enableAutoSizing = true;
                title.fontSizeMin = 24f;
                title.fontSizeMax = 30f;
                title.alignment = TextAlignmentOptions.Center;
                title.enableWordWrapping = false;
            }

            var titleRect = FindRectTransform(root, "HelpOverlay/Dialog/Title");
            if (titleRect != null)
            {
                titleRect.anchorMin = new Vector2(0.5f, 1f);
                titleRect.anchorMax = new Vector2(0.5f, 1f);
                titleRect.pivot = new Vector2(0.5f, 0.5f);
                titleRect.anchoredPosition = new Vector2(0f, -35f);
                titleRect.sizeDelta = new Vector2(410f, 58f);
            }

            var message = FindText(root, "HelpOverlay/Dialog/Message");
            if (message != null)
            {
                message.fontSize = 24f;
                message.enableAutoSizing = true;
                message.fontSizeMin = 22f;
                message.fontSizeMax = 27f;
                message.alignment = TextAlignmentOptions.TopLeft;
                message.enableWordWrapping = true;
                message.overflowMode = TextOverflowModes.Overflow;
            }

            var messageRect = FindRectTransform(root, "HelpOverlay/Dialog/Message");
            if (messageRect != null)
            {
                messageRect.anchorMin = new Vector2(0f, 0f);
                messageRect.anchorMax = new Vector2(1f, 1f);
                messageRect.pivot = new Vector2(0.5f, 0.5f);
                messageRect.anchoredPosition = new Vector2(0f, 1f);
                messageRect.sizeDelta = new Vector2(-104f, -242f);
            }

            var helpDialog = FindRectTransform(root, "HelpOverlay/Dialog");
            if (helpDialog != null && message != null)
            {
                Canvas.ForceUpdateCanvases();
                message.ForceMeshUpdate();

                var availableHeight = root.GetComponent<RectTransform>() != null ? root.GetComponent<RectTransform>().rect.height : 0f;
                if (availableHeight <= 0f)
                {
                    var parentRect = root.parent as RectTransform;
                    if (parentRect != null)
                    {
                        availableHeight = parentRect.rect.height;
                    }
                }

                const float helpDialogWidth = 660f;
                const float helpDialogMinHeight = 380f;
                const float helpDialogMaxHeightRatio = 0.68f;
                const float helpDialogChromeHeight = 242f;
                const float helpMessageHorizontalPadding = 120f;

                var maxHeight = availableHeight > 0f
                    ? Mathf.Max(helpDialogMinHeight, availableHeight * helpDialogMaxHeightRatio)
                    : 760f;
                var contentWidth = Mathf.Max(0f, helpDialogWidth - helpMessageHorizontalPadding);
                var preferredHeight = message.GetPreferredValues(message.text, contentWidth, 0f).y;
                var dialogHeight = Mathf.Clamp(Mathf.Ceil(preferredHeight) + helpDialogChromeHeight, helpDialogMinHeight, maxHeight);

                helpDialog.sizeDelta = new Vector2(helpDialogWidth, dialogHeight);
            }
        }

        private GameObject CreateSettingsPopup()
        {
            var root = CreateModalHost("SettingsPopup");
            if (root == null)
            {
                return null;
            }

            var dialog = CreatePopupPanel(root.transform, "Dialog", new Vector2(620f, 468f));
            var title = CreatePopupText("Title", UiTextCatalog.Get(SettingsPopupTitleKey), 34f, FontStyles.Bold, TextAlignmentOptions.Center);
            title.transform.SetParent(dialog.transform, false);
            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 0.5f);
            titleRect.anchoredPosition = new Vector2(0f, -40f);
            titleRect.sizeDelta = new Vector2(300f, 56f);

            var container = new GameObject(
                "Container",
                typeof(RectTransform),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            container.transform.SetParent(dialog.transform, false);
            var containerRect = container.GetComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0f, 0f);
            containerRect.anchorMax = new Vector2(1f, 1f);
            containerRect.offsetMin = new Vector2(36f, 118f);
            containerRect.offsetMax = new Vector2(-36f, -108f);

            var layout = container.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = container.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            settingsMusicValueText = CreateSettingsRow(container.transform, "MusicRow", SettingsMusicKey, MiniGameRuntimeSettings.MusicEnabled);
            settingsSfxValueText = CreateSettingsRow(container.transform, "SfxRow", SettingsSfxKey, MiniGameRuntimeSettings.SfxEnabled);
            settingsVibrationValueText = CreateSettingsRow(container.transform, "VibrationRow", SettingsVibrationKey, MiniGameRuntimeSettings.VibrationEnabled);

            var closeButton = CreateDialogButton(
                "CloseButton",
                dialog.transform,
                UiTextCatalog.Get("common.action.got_it"),
                new Vector2(240f, 58f),
                new Color(0.98f, 0.90f, 0.68f, 1f),
                CloseActiveModal);
            var closeRect = closeButton.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.5f, 0f);
            closeRect.anchorMax = new Vector2(0.5f, 0f);
            closeRect.pivot = new Vector2(0.5f, 0.5f);
            closeRect.anchoredPosition = new Vector2(0f, 34f);

            return root;
        }

        private TextMeshProUGUI CreateSettingsRow(Transform parent, string name, string textKey, bool enabled)
        {
            var row = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RoundedRectGraphic),
                typeof(Button),
                typeof(LayoutElement));
            row.transform.SetParent(parent, false);

            var rowRect = row.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0f, 0.5f);
            rowRect.anchorMax = new Vector2(1f, 0.5f);
            rowRect.pivot = new Vector2(0.5f, 0.5f);
            rowRect.sizeDelta = new Vector2(0f, 64f);

            var background = row.GetComponent<RoundedRectGraphic>();
            background.color = new Color(1f, 0.99f, 0.96f, 1f);
            background.CornerRadius = 18f;
            background.raycastTarget = true;

            var button = row.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = background;

            var label = CreateButtonLabel("Label", UiTextCatalog.Get(textKey), 24f);
            label.transform.SetParent(row.transform, false);
            var labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(0.5f, 1f);
            labelRect.offsetMin = new Vector2(18f, 0f);
            labelRect.offsetMax = new Vector2(-12f, 0f);
            label.alignment = TextAlignmentOptions.MidlineLeft;

            var value = CreateButtonLabel("Value", GetToggleText(enabled), 22f);
            value.transform.SetParent(row.transform, false);
            var valueRect = value.rectTransform;
            valueRect.anchorMin = new Vector2(0.5f, 0f);
            valueRect.anchorMax = new Vector2(1f, 1f);
            valueRect.offsetMin = new Vector2(12f, 0f);
            valueRect.offsetMax = new Vector2(-18f, 0f);
            value.alignment = TextAlignmentOptions.MidlineRight;
            value.color = new Color(0.48f, 0.39f, 0.24f, 1f);

            var layout = row.GetComponent<LayoutElement>();
            layout.preferredHeight = 64f;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(delegate { ToggleSettingsValue(textKey); });
            MiniGameSfxPlayer.Attach(button, MiniGameSfxType.UiTap, 0.66f);

            return value;
        }

        private void ToggleSettingsValue(string textKey)
        {
            if (textKey == SettingsMusicKey)
            {
                MiniGameRuntimeSettings.SetMusicEnabled(!MiniGameRuntimeSettings.MusicEnabled);
                if (settingsMusicValueText != null)
                {
                    settingsMusicValueText.text = GetToggleText(MiniGameRuntimeSettings.MusicEnabled);
                }
            }
            else if (textKey == SettingsSfxKey)
            {
                MiniGameRuntimeSettings.SetSfxEnabled(!MiniGameRuntimeSettings.SfxEnabled);
                if (settingsSfxValueText != null)
                {
                    settingsSfxValueText.text = GetToggleText(MiniGameRuntimeSettings.SfxEnabled);
                }
            }
            else if (textKey == SettingsVibrationKey)
            {
                MiniGameRuntimeSettings.SetVibrationEnabled(!MiniGameRuntimeSettings.VibrationEnabled);
                if (settingsVibrationValueText != null)
                {
                    settingsVibrationValueText.text = GetToggleText(MiniGameRuntimeSettings.VibrationEnabled);
                }
            }
        }

        private static string GetToggleText(bool enabled)
        {
            return UiTextCatalog.Get(enabled ? "popup.pause.toggle_on" : "popup.pause.toggle_off");
        }

        private GameObject CreateModalHost(string name)
        {
            if (overlayRoot == null)
            {
                overlayRoot = EnsureOverlayRoot();
            }

            if (overlayRoot == null)
            {
                return null;
            }

            var host = new GameObject(name, typeof(RectTransform));
            host.transform.SetParent(overlayRoot, false);
            Stretch(host.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            host.transform.SetAsLastSibling();

            var blocker = new GameObject(
                "Backdrop",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            blocker.transform.SetParent(host.transform, false);
            Stretch(blocker.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var blockerImage = blocker.GetComponent<Image>();
            blockerImage.color = new Color(0.15f, 0.14f, 0.11f, 0.36f);
            blockerImage.raycastTarget = true;
            var blockerButton = blocker.GetComponent<Button>();
            blockerButton.transition = Selectable.Transition.None;
            blockerButton.targetGraphic = blockerImage;
            blockerButton.onClick.AddListener(CloseActiveModal);

            return host;
        }

        private RectTransform CreatePopupPanel(Transform parent, string name, Vector2 size)
        {
            var panel = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RoundedRectGraphic),
                typeof(Shadow));
            panel.transform.SetParent(parent, false);

            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;

            var graphic = panel.GetComponent<RoundedRectGraphic>();
            graphic.color = new Color(1f, 0.99f, 0.96f, 1f);
            graphic.CornerRadius = 28f;
            graphic.raycastTarget = true;

            var shadow = panel.GetComponent<Shadow>();
            shadow.effectColor = new Color(0.31f, 0.29f, 0.21f, 0.16f);
            shadow.effectDistance = new Vector2(0f, -5f);

            return rect;
        }

        private TextMeshProUGUI CreatePopupText(string name, string content, float fontSize, FontStyles style, TextAlignmentOptions alignment)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.enableWordWrapping = false;
            text.color = new Color(0.35f, 0.28f, 0.18f, 1f);
            text.raycastTarget = false;
            return text;
        }

        private TextMeshProUGUI CreateButtonLabel(string name, string content, float fontSize)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.color = new Color(0.35f, 0.28f, 0.18f, 1f);
            text.raycastTarget = false;
            return text;
        }

        private Button CreateDialogButton(string name, Transform parent, string content, Vector2 size, Color backgroundColor, Action onClick)
        {
            var buttonRoot = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RoundedRectGraphic),
                typeof(Button));
            buttonRoot.transform.SetParent(parent, false);

            var rect = buttonRoot.GetComponent<RectTransform>();
            rect.sizeDelta = size;

            var graphic = buttonRoot.GetComponent<RoundedRectGraphic>();
            graphic.color = backgroundColor;
            graphic.CornerRadius = 18f;
            graphic.raycastTarget = true;

            var button = buttonRoot.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = graphic;
            button.onClick.AddListener(delegate
            {
                onClick?.Invoke();
            });
            MiniGameSfxPlayer.Attach(button, MiniGameSfxType.UiTap, 0.66f);

            var label = CreateButtonLabel("Label", content, 22f);
            label.transform.SetParent(buttonRoot.transform, false);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            return button;
        }

        private void CleanupLegacyHeaderMenu(Transform shell)
        {
            if (shell == null)
            {
                return;
            }

            var legacyMenuButton = shell.Find("HeaderTitleBar/MenuButton");
            if (legacyMenuButton != null)
            {
                UnityEngine.Object.Destroy(legacyMenuButton.gameObject);
            }
        }

        private sealed class MenuButtonPressEffect : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
        {
            private Vector3 originalScale = Vector3.one;
            private Vector3 pressedScale = Vector3.one;
            private bool configured;

            public void Configure(float scaleFactor)
            {
                if (!configured)
                {
                    originalScale = transform.localScale;
                    configured = true;
                }

                pressedScale = originalScale * Mathf.Clamp(scaleFactor, 0.5f, 1f);
            }

            public void OnPointerDown(PointerEventData eventData)
            {
                transform.localScale = pressedScale;
            }

            public void OnPointerUp(PointerEventData eventData)
            {
                transform.localScale = originalScale;
            }

            public void OnPointerExit(PointerEventData eventData)
            {
                transform.localScale = originalScale;
            }

            private void OnDisable()
            {
                transform.localScale = originalScale;
            }
        }
    }
}
