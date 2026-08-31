using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    internal sealed partial class HallRenderer
    {
        private const string SupportTitleKey = "hall.support.title";
        private const string SupportRewardSuccessKey = "hall.support.result.success";
        private const string SupportRewardIncompleteKey = "hall.support.result.incomplete";
        private const string SupportRewardErrorKey = "hall.support.result.error";
        private const string SupportResultPopupResourcePath = "MiniGamePopup";
        private const int SupportNativeTemplateAdBottomOffset = 168;

        private HallAdConfig hallAdConfig;
        private Button supportRewardedVideoButton;
        private Button supportInterstitialButton;
        private WeChatWASM.WXCustomAd supportNativeTemplateAd;
        private WeChatWASM.WXInterstitialAd supportInterstitialAd;
        private WeChatWASM.WXRewardedVideoAd supportRewardedVideoAd;
        private GameObject supportResultPopupRoot;
        private bool supportAdOperationPending;
        private bool supportRewardHandled;
        private bool supportNativeTemplateLoading;

        private void ShowSupportAuthorPopup()
        {
            CloseActiveModal();
            hallAdConfig = HallAdConfig.Load();
            activeModalRoot = CreateSupportAuthorPopup();
            ShowSupportNativeTemplate();
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

            RefreshSupportActionState();
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

        private void CreateSupportContent(Transform dialog)
        {
            var content = CreatePopupPanel(dialog, "Content", new Vector2(588f, 400f));
            content.anchorMin = new Vector2(0.5f, 1f);
            content.anchorMax = new Vector2(0.5f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = new Vector2(0f, -132f);
            content.GetComponent<RoundedRectGraphic>().color = new Color(1f, 0.97f, 0.88f, 1f);
            content.GetComponent<Shadow>().effectColor = new Color(0.31f, 0.27f, 0.17f, 0.10f);

            CreateSupportAdDescription(
                content,
                "RewardedVideoDescription",
                "hall.support.description.rewarded",
                new Vector2(0f, -52f));
            supportRewardedVideoButton = CreateDialogButton(
                "RewardedVideoButton",
                content,
                UiTextCatalog.Get("hall.support.action.rewarded"),
                new Vector2(420f, 64f),
                new Color(0.98f, 0.78f, 0.30f, 1f),
                ShowSupportRewardedVideo);
            ConfigureRect(supportRewardedVideoButton.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -122f), new Vector2(420f, 64f));

            CreateSupportAdDescription(
                content,
                "InterstitialDescription",
                "hall.support.description.interstitial",
                new Vector2(0f, -212f));
            supportInterstitialButton = CreateDialogButton(
                "InterstitialButton",
                content,
                UiTextCatalog.Get("hall.support.action.interstitial"),
                new Vector2(420f, 64f),
                new Color(0.98f, 0.78f, 0.30f, 1f),
                ShowSupportInterstitial);
            ConfigureRect(supportInterstitialButton.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -282f), new Vector2(420f, 64f));

        }

        private void CreateSupportAdDescription(Transform parent, string name, string textKey, Vector2 position)
        {
            var description = CreatePopupText(name, UiTextCatalog.Get(textKey), 18f, FontStyles.Normal, TextAlignmentOptions.Center);
            description.transform.SetParent(parent, false);
            description.enableWordWrapping = true;
            description.color = new Color(0.35f, 0.31f, 0.20f, 1f);
            ConfigureRect(description.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), position, new Vector2(490f, 58f));
        }

        private void RefreshSupportActionState()
        {
            var supportedEnvironment = !Application.isEditor;
            var available = supportedEnvironment && !supportAdOperationPending;
            if (supportRewardedVideoButton != null)
            {
                supportRewardedVideoButton.interactable = available && !string.IsNullOrWhiteSpace(hallAdConfig?.RewardedVideoAdUnitId);
            }

            if (supportInterstitialButton != null)
            {
                supportInterstitialButton.interactable = available && !string.IsNullOrWhiteSpace(hallAdConfig?.InterstitialAdUnitId);
            }
        }

        private void ShowSupportNativeTemplate()
        {
            if (supportNativeTemplateAd != null || supportNativeTemplateLoading ||
                !IsSupportPopupActive() || Application.isEditor ||
                string.IsNullOrWhiteSpace(hallAdConfig?.NativeTemplateAdUnitId))
            {
                return;
            }

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
                supportNativeTemplateLoading = true;
                supportNativeTemplateAd.OnError(delegate(WeChatWASM.WXADErrorResponse response)
                {
                    if (!IsSupportPopupActive())
                    {
                        return;
                    }

                    supportNativeTemplateLoading = false;
                    HideAndDestroyNativeTemplateAd();
                });
                supportNativeTemplateAd.OnClose(delegate
                {
                    supportNativeTemplateLoading = false;
                    DestroySupportNativeTemplateAd();
                });
                supportNativeTemplateAd.OnHide(delegate
                {
                    supportNativeTemplateLoading = false;
                    DestroySupportNativeTemplateAd();
                });
                supportNativeTemplateAd.OnLoad(delegate
                {
                    if (!IsSupportPopupActive())
                    {
                        HideAndDestroyNativeTemplateAd();
                        return;
                    }

                    supportNativeTemplateAd?.Show(
                        delegate
                        {
                            supportNativeTemplateLoading = false;
                        },
                        delegate
                        {
                            supportNativeTemplateLoading = false;
                            HideAndDestroyNativeTemplateAd();
                        });
                });
            }
            catch (Exception exception)
            {
                Debug.LogWarning("原生模板广告创建失败: " + exception.Message);
                supportNativeTemplateLoading = false;
                HideAndDestroyNativeTemplateAd();
            }
        }

        private void ShowSupportInterstitial()
        {
            if (supportAdOperationPending || string.IsNullOrWhiteSpace(hallAdConfig?.InterstitialAdUnitId))
            {
                return;
            }

            try
            {
                DestroySupportInterstitial();
                supportInterstitialAd = WeChatWASM.WXSDKManagerHandler.Instance.CreateInterstitialAd(new WeChatWASM.WXCreateInterstitialAdParam
                {
                    adUnitId = hallAdConfig.InterstitialAdUnitId
                });
                supportAdOperationPending = true;
                supportInterstitialAd.OnClose(delegate
                {
                    supportAdOperationPending = false;
                    DestroySupportInterstitial();
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
                    delegate { },
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
                                    delegate { },
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
            if (supportAdOperationPending || string.IsNullOrWhiteSpace(hallAdConfig?.RewardedVideoAdUnitId))
            {
                return;
            }

            try
            {
                EnsureSupportRewardedVideoAd();
                supportAdOperationPending = true;
                supportRewardHandled = false;
                supportRewardedVideoAd.Show(
                    delegate { },
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
                                    delegate { },
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
            var windowWidth = Mathf.Max(1f, Screen.width);
            var windowHeight = Mathf.Max(1f, Screen.height);

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

            var width = Mathf.Max(1, Mathf.RoundToInt(windowWidth * 0.92f));
            return new WeChatWASM.CustomStyle
            {
                left = Mathf.RoundToInt((windowWidth - width) * 0.5f),
                top = Mathf.Max(0, Mathf.RoundToInt(windowHeight) - SupportNativeTemplateAdBottomOffset),
                width = width
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
            supportNativeTemplateLoading = false;
            supportRewardedVideoButton = null;
            supportInterstitialButton = null;
        }

        private void DisposeSupportAds()
        {
            HideAndDestroyNativeTemplateAd();
            DestroySupportInterstitial();
            supportAdOperationPending = false;
            supportNativeTemplateLoading = false;
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

        private bool IsSupportOperationActive()
        {
            return supportAdOperationPending &&
                   IsSupportPopupActive();
        }

        private bool IsSupportPopupActive()
        {
            return activeModalRoot != null && activeModalRoot.name == "SupportAuthorPopup";
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
