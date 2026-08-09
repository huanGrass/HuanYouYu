using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    internal sealed partial class HallRenderer
    {
        private const string SupportTitleKey = "hall.support.title";
        private const string SupportUnconfiguredKey = "hall.support.status.unconfigured";
        private const string SupportEditorOnlyKey = "hall.support.status.wechat_only";
        private const string SupportReadyKey = "hall.support.status.ready";
        private const string SupportLoadingKey = "hall.support.status.loading";
        private const string SupportShowingKey = "hall.support.status.showing";
        private const string SupportNativeTemplateVisibleKey = "hall.support.status.native_template_visible";
        private const string SupportRewardSuccessKey = "hall.support.result.success";
        private const string SupportRewardIncompleteKey = "hall.support.result.incomplete";
        private const string SupportRewardErrorKey = "hall.support.result.error";
        private const string SupportResultPopupResourcePath = "MiniGamePopup";

        private enum SupportAdTab
        {
            RewardedVideo,
            NativeTemplate,
            Interstitial
        }

        private sealed class SupportTabBinding
        {
            public SupportAdTab Tab;
            public Button Button;
            public RoundedRectGraphic Graphic;
            public TextMeshProUGUI Label;
            public LayoutElement LayoutElement;
            public RectTransform RectTransform;
        }

        private readonly List<SupportTabBinding> supportTabBindings = new List<SupportTabBinding>();
        private HallAdConfig hallAdConfig;
        private SupportAdTab activeSupportTab = SupportAdTab.RewardedVideo;
        private TextMeshProUGUI supportDescriptionText;
        private TextMeshProUGUI supportStatusText;
        private TextMeshProUGUI supportActionLabel;
        private Button supportActionButton;
        private RectTransform supportNativeTemplateSlot;
        private WeChatWASM.WXCustomAd supportNativeTemplateAd;
        private WeChatWASM.WXInterstitialAd supportInterstitialAd;
        private WeChatWASM.WXRewardedVideoAd supportRewardedVideoAd;
        private GameObject supportResultPopupRoot;
        private bool supportAdOperationPending;
        private bool supportRewardHandled;

        private void ShowSupportAuthorPopup()
        {
            CloseActiveModal();
            hallAdConfig = HallAdConfig.Load();
            activeModalRoot = CreateSupportAuthorPopup();
        }

        private GameObject CreateSupportAuthorPopup()
        {
            var modal = CreateModalHost("SupportAuthorPopup");
            if (modal == null)
            {
                return null;
            }

            var dialog = CreateSupportDialogPanel(modal.transform);
            CreateSupportTitleDecor(dialog);
            var title = CreatePopupText("Title", UiTextCatalog.Get(SupportTitleKey), 34f, FontStyles.Bold, TextAlignmentOptions.Center);
            title.transform.SetParent(dialog, false);
            title.color = new Color(0.32f, 0.42f, 0.19f, 1f);
            ConfigureRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -48f), new Vector2(420f, 58f));

            CreateSupportTabBar(dialog);
            CreateSupportContent(dialog);

            var closeButton = CreateDialogButton(
                "CloseButton",
                dialog,
                UiTextCatalog.Get("hall.support.action.close"),
                new Vector2(210f, 58f),
                new Color(0.82f, 0.89f, 0.46f, 1f),
                CloseActiveModal);
            ConfigureRect(closeButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(210f, 58f));
            closeButton.transform.Find("Label").GetComponent<TextMeshProUGUI>().color = new Color(0.25f, 0.34f, 0.12f, 1f);

            SelectSupportTab(SupportAdTab.RewardedVideo);
            return modal;
        }

        private RectTransform CreateSupportDialogPanel(Transform parent)
        {
            var outer = CreateRoundedRect(
                "Dialog",
                parent,
                new Color32(191, 218, 104, 255),
                34f,
                true,
                typeof(Shadow));
            var outerRect = outer.GetComponent<RectTransform>();
            ConfigureRect(outerRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(660f, 720f));

            var shadow = outer.GetComponent<Shadow>();
            shadow.effectColor = new Color(0.18f, 0.28f, 0.10f, 0.34f);
            shadow.effectDistance = new Vector2(0f, -8f);

            var inner = CreateRoundedRect(
                "InnerPanel",
                outer.transform,
                new Color32(255, 251, 238, 255),
                28f,
                true);
            Stretch(
                inner.GetComponent<RectTransform>(),
                Vector2.zero,
                Vector2.one,
                new Vector2(12f, 12f),
                new Vector2(-12f, -12f));
            return outerRect;
        }

        private void CreateSupportTitleDecor(Transform dialog)
        {
            var titleGlow = CreateRoundedRect(
                "TitleGlow",
                dialog,
                new Color(1f, 1f, 0.96f, 0.78f),
                22f,
                false);
            ConfigureRect(
                titleGlow.GetComponent<RectTransform>(),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -48f),
                new Vector2(300f, 52f));

            CreateSupportTitleDot(dialog, "TitleDotLeft", new Vector2(-174f, -48f));
            CreateSupportTitleDot(dialog, "TitleDotRight", new Vector2(174f, -48f));
        }

        private void CreateSupportTitleDot(Transform dialog, string name, Vector2 position)
        {
            var dot = CreateRoundedRect(name, dialog, new Color(1f, 0.62f, 0.14f, 1f), 8f, false);
            ConfigureRect(
                dot.GetComponent<RectTransform>(),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                position,
                new Vector2(14f, 14f));
        }

        private void CreateSupportTabBar(Transform dialog)
        {
            supportTabBindings.Clear();
            var tabBar = new GameObject(
                "TabBar",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RoundedRectGraphic),
                typeof(HorizontalLayoutGroup));
            tabBar.transform.SetParent(dialog, false);
            var tabBarRect = tabBar.GetComponent<RectTransform>();
            tabBarRect.anchorMin = new Vector2(0.5f, 1f);
            tabBarRect.anchorMax = new Vector2(0.5f, 1f);
            tabBarRect.pivot = new Vector2(0.5f, 1f);
            tabBarRect.anchoredPosition = new Vector2(0f, -104f);
            tabBarRect.sizeDelta = new Vector2(588f, 54f);

            var background = tabBar.GetComponent<RoundedRectGraphic>();
            background.color = new Color(1f, 0.98f, 0.88f, 0.88f);
            background.CornerRadius = 22f;
            background.raycastTarget = false;

            var layout = tabBar.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 7, 7);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            CreateSupportTab(tabBar.transform, "Tab_RewardedVideo", SupportAdTab.RewardedVideo, "hall.support.tab.rewarded");
            CreateSupportTab(tabBar.transform, "Tab_NativeTemplate", SupportAdTab.NativeTemplate, "hall.support.tab.native_template");
            CreateSupportTab(tabBar.transform, "Tab_Interstitial", SupportAdTab.Interstitial, "hall.support.tab.interstitial");
        }

        private void CreateSupportTab(Transform parent, string name, SupportAdTab tab, string textKey)
        {
            var tabObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RoundedRectGraphic),
                typeof(Button),
                typeof(LayoutElement));
            tabObject.transform.SetParent(parent, false);
            var tabRect = tabObject.GetComponent<RectTransform>();
            tabRect.sizeDelta = new Vector2(168f, 40f);

            var layoutElement = tabObject.GetComponent<LayoutElement>();
            layoutElement.preferredWidth = 168f;
            layoutElement.preferredHeight = 40f;

            var graphic = tabObject.GetComponent<RoundedRectGraphic>();
            graphic.CornerRadius = 18f;
            graphic.raycastTarget = true;

            var button = tabObject.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = graphic;
            button.onClick.AddListener(delegate { SelectSupportTab(tab); });
            MiniGameSfxPlayer.Attach(button, MiniGameSfxType.UiTap, 0.66f);

            var label = CreateButtonLabel("Label", UiTextCatalog.Get(textKey), 22f);
            label.transform.SetParent(tabObject.transform, false);
            label.enableAutoSizing = true;
            label.fontSizeMin = 17f;
            label.fontSizeMax = 22f;
            Stretch(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, 0f));

            supportTabBindings.Add(new SupportTabBinding
            {
                Tab = tab,
                Button = button,
                Graphic = graphic,
                Label = label,
                LayoutElement = layoutElement,
                RectTransform = tabRect
            });
        }

        private void CreateSupportContent(Transform dialog)
        {
            var content = CreatePopupPanel(dialog, "Content", new Vector2(588f, 420f));
            content.anchorMin = new Vector2(0.5f, 1f);
            content.anchorMax = new Vector2(0.5f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = new Vector2(0f, -178f);
            content.GetComponent<RoundedRectGraphic>().color = new Color(1f, 0.97f, 0.88f, 1f);
            content.GetComponent<Shadow>().effectColor = new Color(0.31f, 0.27f, 0.17f, 0.10f);

            var descriptionBackdrop = CreateRoundedRect(
                "DescriptionBackdrop",
                content,
                new Color(1f, 1f, 0.97f, 0.78f),
                18f,
                false);
            ConfigureRect(
                descriptionBackdrop.GetComponent<RectTransform>(),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -74f),
                new Vector2(526f, 132f));

            supportDescriptionText = CreatePopupText("Description", string.Empty, 25f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            supportDescriptionText.transform.SetParent(content, false);
            supportDescriptionText.enableWordWrapping = true;
            supportDescriptionText.overflowMode = TextOverflowModes.Overflow;
            supportDescriptionText.color = new Color(0.35f, 0.31f, 0.20f, 1f);
            ConfigureRect(supportDescriptionText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -74f), new Vector2(482f, 108f));

            supportNativeTemplateSlot = CreatePopupPanel(content, "NativeTemplateSlot", new Vector2(510f, 126f));
            supportNativeTemplateSlot.anchoredPosition = new Vector2(0f, -22f);
            supportNativeTemplateSlot.GetComponent<RoundedRectGraphic>().color = new Color(0.91f, 0.86f, 0.72f, 1f);
            var nativeTemplateHint = CreatePopupText("Hint", UiTextCatalog.Get("hall.support.native_template.placeholder"), 21f, FontStyles.Normal, TextAlignmentOptions.Center);
            nativeTemplateHint.transform.SetParent(supportNativeTemplateSlot, false);
            Stretch(nativeTemplateHint.rectTransform, Vector2.zero, Vector2.one, new Vector2(12f, 8f), new Vector2(-12f, -8f));

            var statusBackdrop = CreateRoundedRect(
                "StatusBackdrop",
                content,
                new Color(0.82f, 0.89f, 0.46f, 0.42f),
                16f,
                false);
            ConfigureRect(
                statusBackdrop.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 118f),
                new Vector2(500f, 52f));

            supportStatusText = CreatePopupText("Status", string.Empty, 21f, FontStyles.Bold, TextAlignmentOptions.Center);
            supportStatusText.transform.SetParent(content, false);
            supportStatusText.enableWordWrapping = true;
            supportStatusText.color = new Color(0.32f, 0.42f, 0.19f, 1f);
            ConfigureRect(supportStatusText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 118f), new Vector2(476f, 48f));

            supportActionButton = CreateDialogButton(
                "ActionButton",
                content,
                string.Empty,
                new Vector2(300f, 64f),
                new Color(0.98f, 0.78f, 0.30f, 1f),
                HandleSupportActionClicked);
            ConfigureRect(supportActionButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 58f), new Vector2(300f, 64f));
            supportActionLabel = supportActionButton.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            if (supportActionLabel != null)
            {
                supportActionLabel.color = Color.white;
            }
        }

        private void SelectSupportTab(SupportAdTab tab)
        {
            if (supportAdOperationPending)
            {
                return;
            }

            if (activeSupportTab == SupportAdTab.NativeTemplate && tab != SupportAdTab.NativeTemplate)
            {
                HideAndDestroyNativeTemplateAd();
            }

            activeSupportTab = tab;
            for (var i = 0; i < supportTabBindings.Count; i++)
            {
                var binding = supportTabBindings[i];
                var selected = binding.Tab == tab;
                binding.Graphic.color = selected
                    ? new Color(1f, 0.62f, 0.14f, 1f)
                    : new Color(1f, 1f, 0.96f, 0.95f);
                binding.Label.color = selected ? Color.white : new Color(0.32f, 0.42f, 0.19f, 1f);

                var width = selected ? 184f : 168f;
                binding.LayoutElement.preferredWidth = width;
                binding.RectTransform.sizeDelta = new Vector2(width, 40f);
            }

            if (supportDescriptionText != null)
            {
                supportDescriptionText.text = UiTextCatalog.Get(GetSupportDescriptionKey(tab));
            }

            if (supportActionLabel != null)
            {
                supportActionLabel.text = UiTextCatalog.Get(GetSupportActionKey(tab));
            }

            if (supportNativeTemplateSlot != null)
            {
                supportNativeTemplateSlot.gameObject.SetActive(tab == SupportAdTab.NativeTemplate);
            }

            RefreshSupportActionState();
        }

        private void RefreshSupportActionState()
        {
            if (supportActionButton == null || supportStatusText == null)
            {
                return;
            }

            var adUnitId = GetSupportAdUnitId(activeSupportTab);
            var configured = !string.IsNullOrWhiteSpace(adUnitId);
            var supportedEnvironment = !Application.isEditor;
            supportActionButton.interactable = configured && supportedEnvironment && !supportAdOperationPending;

            if (!configured)
            {
                supportStatusText.text = UiTextCatalog.Get(SupportUnconfiguredKey);
            }
            else if (!supportedEnvironment)
            {
                supportStatusText.text = UiTextCatalog.Get(SupportEditorOnlyKey);
            }
            else if (!supportAdOperationPending)
            {
                supportStatusText.text = UiTextCatalog.Get(SupportReadyKey);
            }
        }

        private void HandleSupportActionClicked()
        {
            if (supportAdOperationPending || string.IsNullOrWhiteSpace(GetSupportAdUnitId(activeSupportTab)))
            {
                return;
            }

            switch (activeSupportTab)
            {
                case SupportAdTab.NativeTemplate:
                    ShowSupportNativeTemplate();
                    break;
                case SupportAdTab.Interstitial:
                    ShowSupportInterstitial();
                    break;
                default:
                    ShowSupportRewardedVideo();
                    break;
            }
        }

        private void ShowSupportNativeTemplate()
        {
            try
            {
                HideAndDestroyNativeTemplateAd();
                var style = CreateSupportNativeTemplateStyle();
                supportNativeTemplateAd = WeChatWASM.WXSDKManagerHandler.Instance.CreateCustomAd(new WeChatWASM.WXCreateCustomAdParam
                {
                    adUnitId = hallAdConfig.NativeTemplateAdUnitId,
                    adIntervals = 30,
                    style = style,
                    styleRaw = JsonUtility.ToJson(style)
                });
                supportAdOperationPending = true;
                SetSupportStatus(SupportLoadingKey);
                supportNativeTemplateAd.OnError(delegate(WeChatWASM.WXADErrorResponse response)
                {
                    if (!IsSupportOperationActive())
                    {
                        return;
                    }

                    supportAdOperationPending = false;
                    HideAndDestroyNativeTemplateAd();
                    ShowSupportResult(SupportRewardErrorKey, false);
                });
                supportNativeTemplateAd.OnClose(delegate
                {
                    supportAdOperationPending = false;
                    DestroySupportNativeTemplateAd();
                    SetSupportStatus(SupportReadyKey);
                });
                supportNativeTemplateAd.OnHide(delegate
                {
                    supportAdOperationPending = false;
                    DestroySupportNativeTemplateAd();
                    SetSupportStatus(SupportReadyKey);
                });
                supportNativeTemplateAd.OnLoad(delegate
                {
                    if (!IsSupportOperationActive())
                    {
                        HideAndDestroyNativeTemplateAd();
                        return;
                    }

                    supportNativeTemplateAd?.Show(
                        delegate
                        {
                            supportAdOperationPending = false;
                            RefreshSupportActionState();
                            if (supportStatusText != null)
                            {
                                supportStatusText.text = UiTextCatalog.Get(SupportNativeTemplateVisibleKey);
                            }
                        },
                        delegate
                        {
                            supportAdOperationPending = false;
                            HideAndDestroyNativeTemplateAd();
                            ShowSupportResult(SupportRewardErrorKey, false);
                        });
                });
            }
            catch (Exception exception)
            {
                Debug.LogWarning("原生模板广告创建失败: " + exception.Message);
                supportAdOperationPending = false;
                HideAndDestroyNativeTemplateAd();
                ShowSupportResult(SupportRewardErrorKey, false);
            }
        }

        private void ShowSupportInterstitial()
        {
            try
            {
                DestroySupportInterstitial();
                supportInterstitialAd = WeChatWASM.WXSDKManagerHandler.Instance.CreateInterstitialAd(new WeChatWASM.WXCreateInterstitialAdParam
                {
                    adUnitId = hallAdConfig.InterstitialAdUnitId
                });
                supportAdOperationPending = true;
                SetSupportStatus(SupportLoadingKey);
                supportInterstitialAd.OnClose(delegate
                {
                    supportAdOperationPending = false;
                    DestroySupportInterstitial();
                    SetSupportStatus(SupportReadyKey);
                    RefreshSupportActionState();
                });
                supportInterstitialAd.OnError(delegate(WeChatWASM.WXADErrorResponse response)
                {
                    if (!IsSupportOperationActive())
                    {
                        return;
                    }

                    supportAdOperationPending = false;
                    DestroySupportInterstitial();
                    ShowSupportResult(SupportRewardErrorKey, false);
                });
                supportInterstitialAd.Show(
                    delegate { SetSupportStatus(SupportShowingKey); },
                    delegate
                    {
                        if (!IsSupportOperationActive())
                        {
                            return;
                        }

                        supportInterstitialAd?.Load(
                            delegate
                            {
                                if (!IsSupportOperationActive())
                                {
                                    return;
                                }

                                supportInterstitialAd?.Show(
                                    delegate { SetSupportStatus(SupportShowingKey); },
                                    delegate { CompleteSupportAdError(); });
                            },
                            delegate { CompleteSupportAdError(); });
                    });
            }
            catch (Exception exception)
            {
                Debug.LogWarning("插屏广告创建失败: " + exception.Message);
                CompleteSupportAdError();
            }
        }

        private void ShowSupportRewardedVideo()
        {
            try
            {
                EnsureSupportRewardedVideoAd();
                supportAdOperationPending = true;
                supportRewardHandled = false;
                SetSupportStatus(SupportLoadingKey);
                supportRewardedVideoAd.Show(
                    delegate { SetSupportStatus(SupportShowingKey); },
                    delegate
                    {
                        if (!IsSupportOperationActive())
                        {
                            return;
                        }

                        supportRewardedVideoAd?.Load(
                            delegate
                            {
                                if (!IsSupportOperationActive())
                                {
                                    return;
                                }

                                supportRewardedVideoAd?.Show(
                                    delegate { SetSupportStatus(SupportShowingKey); },
                                    delegate { CompleteRewardedVideoAttempt(false, true); });
                            },
                            delegate { CompleteRewardedVideoAttempt(false, true); });
                    });
            }
            catch (Exception exception)
            {
                Debug.LogWarning("激励视频广告创建失败: " + exception.Message);
                CompleteRewardedVideoAttempt(false, true);
            }
        }

        private void EnsureSupportRewardedVideoAd()
        {
            if (supportRewardedVideoAd != null)
            {
                return;
            }

            supportRewardedVideoAd = WeChatWASM.WXSDKManagerHandler.Instance.CreateRewardedVideoAd(new WeChatWASM.WXCreateRewardedVideoAdParam
            {
                adUnitId = hallAdConfig.RewardedVideoAdUnitId,
                multiton = false
            });
            supportRewardedVideoAd.OnClose(delegate(WeChatWASM.WXRewardedVideoAdOnCloseResponse response)
            {
                CompleteRewardedVideoAttempt(response != null && response.isEnded, false);
            });
            supportRewardedVideoAd.OnError(delegate(WeChatWASM.WXADErrorResponse response)
            {
                CompleteRewardedVideoAttempt(false, true);
            });
        }

        private void CompleteRewardedVideoAttempt(bool completed, bool failed)
        {
            if (!supportAdOperationPending || supportRewardHandled)
            {
                return;
            }

            supportRewardHandled = true;
            supportAdOperationPending = false;

            if (completed)
            {
                grantHallRewardChest?.Invoke(1);
                ShowSupportResult(SupportRewardSuccessKey, true);
                return;
            }

            ShowSupportResult(failed ? SupportRewardErrorKey : SupportRewardIncompleteKey, false);
        }

        private void CompleteSupportAdError()
        {
            supportAdOperationPending = false;
            DestroySupportInterstitial();
            ShowSupportResult(SupportRewardErrorKey, false);
        }

        private void ShowSupportResult(string messageKey, bool success)
        {
            CloseSupportResultPopup();
            RefreshSupportActionState();
            supportResultPopupRoot = CreatePopupInstance(SupportResultPopupResourcePath, "SupportAdResultPopup");
            if (supportResultPopupRoot == null)
            {
                return;
            }

            SetPopupText(
                supportResultPopupRoot.transform,
                "Dialog/Title",
                UiTextCatalog.Get(success ? "hall.support.result.success_title" : "hall.support.result.failure_title"));
            SetPopupText(supportResultPopupRoot.transform, "Dialog/MessagePanel/Message", UiTextCatalog.Get(messageKey));
            SetPopupText(supportResultPopupRoot.transform, "Dialog/Buttons/ConfirmButton/Label", UiTextCatalog.Get("common.action.got_it"));
            SetPopupActive(supportResultPopupRoot.transform, "Dialog/Buttons/CancelButton", false);
            SetPopupActive(supportResultPopupRoot.transform, "Dialog/CloseButton", false);
            ConfigurePopupButton(supportResultPopupRoot.transform, "Dialog/Buttons/ConfirmButton", CloseSupportResultPopup);

            var blocker = FindButton(supportResultPopupRoot.transform, "Blocker");
            blocker?.onClick.RemoveAllListeners();
            supportResultPopupRoot.transform.SetAsLastSibling();
        }

        private void CloseSupportResultPopup()
        {
            if (supportResultPopupRoot == null)
            {
                return;
            }

            UnityEngine.Object.Destroy(supportResultPopupRoot);
            supportResultPopupRoot = null;
        }

        private WeChatWASM.CustomStyle CreateSupportNativeTemplateStyle()
        {
            Canvas.ForceUpdateCanvases();
            var corners = new Vector3[4];
            supportNativeTemplateSlot.GetWorldCorners(corners);
            var bottomLeft = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            var topRight = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
            var screenWidth = Mathf.Max(1f, Screen.width);
            var screenHeight = Mathf.Max(1f, Screen.height);
            var windowWidth = screenWidth;
            var windowHeight = screenHeight;

            try
            {
                var windowInfo = WeChatWASM.WXSDKManagerHandler.GetWindowInfo();
                if (windowInfo != null && windowInfo.windowWidth > 0 && windowInfo.windowHeight > 0)
                {
                    windowWidth = (float)windowInfo.windowWidth;
                    windowHeight = (float)windowInfo.windowHeight;
                }
            }
            catch (Exception)
            {
            }

            var scaleX = windowWidth / screenWidth;
            var scaleY = windowHeight / screenHeight;
            return new WeChatWASM.CustomStyle
            {
                left = Mathf.RoundToInt(bottomLeft.x * scaleX),
                top = Mathf.RoundToInt(windowHeight - (topRight.y * scaleY)),
                width = Mathf.RoundToInt(Mathf.Max(1f, topRight.x - bottomLeft.x) * scaleX)
            };
        }

        private void PrepareSupportModalClose()
        {
            var closingSupportModal = activeModalRoot != null && activeModalRoot.name == "SupportAuthorPopup";
            if (closingSupportModal)
            {
                supportAdOperationPending = false;
                DestroySupportInterstitial();
                CloseSupportResultPopup();
            }

            HideAndDestroyNativeTemplateAd();
            supportTabBindings.Clear();
            supportDescriptionText = null;
            supportStatusText = null;
            supportActionLabel = null;
            supportActionButton = null;
            supportNativeTemplateSlot = null;
        }

        private void DisposeSupportAds()
        {
            HideAndDestroyNativeTemplateAd();
            DestroySupportInterstitial();
            supportAdOperationPending = false;
        }

        private void HideAndDestroyNativeTemplateAd()
        {
            if (supportNativeTemplateAd == null)
            {
                return;
            }

            try
            {
                supportNativeTemplateAd.Hide(null, null);
                supportNativeTemplateAd.Destroy();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("原生模板广告清理失败: " + exception.Message);
            }
            finally
            {
                supportNativeTemplateAd = null;
            }
        }

        private void DestroySupportNativeTemplateAd()
        {
            if (supportNativeTemplateAd == null)
            {
                return;
            }

            try
            {
                supportNativeTemplateAd.Destroy();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("原生模板广告销毁失败: " + exception.Message);
            }
            finally
            {
                supportNativeTemplateAd = null;
            }
        }

        private void DestroySupportInterstitial()
        {
            if (supportInterstitialAd == null)
            {
                return;
            }

            try
            {
                supportInterstitialAd.Destroy();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("插屏广告清理失败: " + exception.Message);
            }
            finally
            {
                supportInterstitialAd = null;
            }
        }

        private void SetSupportStatus(string textKey)
        {
            if (supportStatusText != null)
            {
                supportStatusText.text = UiTextCatalog.Get(textKey);
            }

            RefreshSupportActionState();
        }

        private bool IsSupportOperationActive()
        {
            return supportAdOperationPending &&
                   activeModalRoot != null &&
                   activeModalRoot.name == "SupportAuthorPopup";
        }

        private string GetSupportAdUnitId(SupportAdTab tab)
        {
            if (hallAdConfig == null)
            {
                return string.Empty;
            }

            switch (tab)
            {
                case SupportAdTab.NativeTemplate:
                    return hallAdConfig.NativeTemplateAdUnitId;
                case SupportAdTab.Interstitial:
                    return hallAdConfig.InterstitialAdUnitId;
                default:
                    return hallAdConfig.RewardedVideoAdUnitId;
            }
        }

        private static string GetSupportDescriptionKey(SupportAdTab tab)
        {
            switch (tab)
            {
                case SupportAdTab.NativeTemplate:
                    return "hall.support.description.native_template";
                case SupportAdTab.Interstitial:
                    return "hall.support.description.interstitial";
                default:
                    return "hall.support.description.rewarded";
            }
        }

        private static string GetSupportActionKey(SupportAdTab tab)
        {
            switch (tab)
            {
                case SupportAdTab.NativeTemplate:
                    return "hall.support.action.native_template";
                case SupportAdTab.Interstitial:
                    return "hall.support.action.interstitial";
                default:
                    return "hall.support.action.rewarded";
            }
        }

        private static void ConfigureRect(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }
    }
}
