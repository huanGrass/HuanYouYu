using System;
using System.Text;
using TMPro;
using UnityEngine;

namespace HuanYouYu.MiniGameHall
{
    public abstract class MiniGameBase : IDisposable
    {
        private readonly MonoBehaviour hostBehaviour;
        private readonly Action<MiniGameSettlement> completeGame;
        private readonly Action exitToHall;
        private string pauseHelpText;
        private MiniGameShell shell;
        private MiniGameWinSettlementView rewardSettlementView;
        private MiniGameTutorialOverlay tutorialOverlay;
        private bool isDisposed;

        protected MiniGameBase(
            string gameId,
            string rootName,
            MonoBehaviour runtimeHostBehaviour,
            Transform parent,
            Action<MiniGameSettlement> onComplete,
            Action onExit)
        {
            if (string.IsNullOrWhiteSpace(gameId))
            {
                throw new ArgumentException("Game id is required.", nameof(gameId));
            }

            GameId = gameId;
            hostBehaviour = runtimeHostBehaviour;
            completeGame = onComplete;
            exitToHall = onExit;
            shell = new MiniGameShell(parent, rootName, HandlePauseRequested, ResolvePauseHelpText);
            shell.ApplyLayout(CreateShellLayout());

            var pauseHelpKeys = GetPauseHelpKeys();
            if (pauseHelpKeys.HasValue)
            {
                var creditsKey = pauseHelpKeys.Value.creditsKey;
                if (string.IsNullOrWhiteSpace(creditsKey))
                {
                    creditsKey = "popup.help.default_credits";
                }
                ConfigurePauseHelp(pauseHelpKeys.Value.helpKey, creditsKey, null);
            }

            BuildOrBindSections();
            ResetGame();
        }

        public string GameId { get; }

        protected MonoBehaviour HostBehaviour
        {
            get { return hostBehaviour; }
        }

        protected Action<MiniGameSettlement> CompleteGame
        {
            get { return completeGame; }
        }

        protected Action ExitToHall
        {
            get { return exitToHall; }
        }

        protected void GrantSettlementReward(MiniGameSettlement settlement)
        {
            var rewardSink = hostBehaviour as IMiniGameRewardSink;
            if (rewardSink != null)
            {
                rewardSink.GrantSettlementReward(GameId, settlement);
            }
        }

        protected void ShowRewardSettlementPanel(
            MiniGameSettlement settlement,
            MiniGameRewardSettlementPanelParams panelParams,
            Action onPrimaryAction,
            Action onBackHall,
            bool grantRewardBeforePrimaryAction)
        {
            if (settlement == null || panelParams == null || shell == null)
            {
                return;
            }

            CloseRewardSettlementPanel();
            shell.ClosePopup();
            panelParams.AutoTick = true;
            rewardSettlementView = MiniGameWinSettlementView.Create(
                shell.PopupHost,
                MiniGameFontProvider.DefaultFont,
                panelParams,
                delegate
                {
                    if (grantRewardBeforePrimaryAction)
                    {
                        GrantSettlementReward(settlement);
                    }

                    CloseRewardSettlementPanel();
                    onPrimaryAction?.Invoke();
                },
                delegate
                {
                    CloseRewardSettlementPanel();
                    onBackHall?.Invoke();
                });
        }

        protected void ShowBackHallRewardSettlementPanel(
            MiniGameSettlement settlement,
            string rootName,
            MiniGameSettlementInfoRow primaryInfo,
            MiniGameSettlementInfoRow secondaryInfo,
            Action onBackHall)
        {
            if (settlement == null)
            {
                return;
            }

            ShowRewardSettlementPanel(
                settlement,
                new MiniGameRewardSettlementPanelParams
                {
                    RootName = rootName,
                    Style = MiniGameRewardSettlementPanelStyle.Neutral,
                    PrimaryAction = MiniGameRewardSettlementPrimaryAction.BackHall,
                    Title = UiTextCatalog.Get("popup.settlement.title"),
                    PrimaryInfo = primaryInfo,
                    SecondaryInfo = secondaryInfo,
                    RewardLabel = UiTextCatalog.Get("settlement.reward_label"),
                    CoinCount = settlement.CoinCount,
                    ChestCount = settlement.ChestCount
                },
                null,
                onBackHall,
                true);
        }

        protected void CloseRewardSettlementPanel()
        {
            if (rewardSettlementView != null)
            {
                rewardSettlementView.Dispose();
                rewardSettlementView = null;
            }
        }

        protected void TryStartTutorial(int version, MiniGameTutorialStep[] steps)
        {
            if (version <= 0 || steps == null || steps.Length == 0 || shell == null)
            {
                return;
            }

            var tutorialStore = hostBehaviour as IMiniGameTutorialStore;
            if (tutorialStore == null || tutorialStore.GetGameTutorialSeenVersion(GameId) >= version || tutorialOverlay != null)
            {
                return;
            }

            shell.ClosePopup();
            tutorialOverlay = MiniGameTutorialOverlay.Show(
                shell.PopupHost,
                steps,
                delegate
                {
                    tutorialOverlay = null;
                    tutorialStore.SetGameTutorialSeenVersion(GameId, version);
                });
        }

        protected void CloseTutorial()
        {
            if (tutorialOverlay == null)
            {
                return;
            }

            tutorialOverlay.Dispose();
            tutorialOverlay = null;
        }

        /// <summary>
        /// 弹出结算框并在确认后回到大厅，供“退出即结算”的场景复用。
        /// </summary>
        protected void ShowSettlementAndComplete(MiniGameSettlement settlement)
        {
            if (settlement == null || shell == null)
            {
                return;
            }

            shell.ShowSettlementPopup(settlement.Summary, delegate
            {
                shell.ClosePopup();
                completeGame?.Invoke(settlement);
            });
        }

        private protected MiniGameShell Shell
        {
            get { return shell; }
        }

        public virtual void Tick(float deltaTime)
        {
        }

        protected virtual MiniGameShellLayout CreateShellLayout()
        {
            return MiniGameShellLayout.Default;
        }

        protected abstract (string helpKey, string creditsKey)? GetPauseHelpKeys();

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            OnBeforeDispose();
            CloseTutorial();
            CloseRewardSettlementPanel();

            if (shell != null)
            {
                shell.Dispose();
                shell = null;
            }
        }

        private void HandlePauseRequested()
        {
            if (!isDisposed)
            {
                OnPauseRequested();
            }
        }

        private string ResolvePauseHelpText()
        {
            if (!string.IsNullOrWhiteSpace(pauseHelpText))
            {
                return pauseHelpText;
            }

            return UiTextCatalog.Get("popup.help.fallback");
        }

        internal void ConfigurePauseHelp(string helpKey, string creditsKey, string fallbackGameplayText)
        {
            var gameplay = ResolvePauseHelpSectionText(helpKey, fallbackGameplayText);
            var credits = ResolvePauseHelpSectionText(creditsKey, null);
            pauseHelpText = BuildPauseHelpText(gameplay, credits);
        }

        private static string ResolvePauseHelpSectionText(string key, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                var value = UiTextCatalog.Get(key);
                if (!string.IsNullOrWhiteSpace(value) && !string.Equals(value, "?", StringComparison.Ordinal))
                {
                    return NormalizeHelpSectionText(value);
                }
            }

            return NormalizeHelpSectionText(fallback);
        }

        private static string BuildPauseHelpText(string gameplay, string credits)
        {
            if (string.IsNullOrWhiteSpace(gameplay) && string.IsNullOrWhiteSpace(credits))
            {
                return null;
            }

            var builder = new StringBuilder();
            AppendHelpSection(
                builder,
                UiTextCatalog.Get("popup.help.section_gameplay"),
                gameplay);
            AppendHelpCreditLine(builder, credits);
            return builder.ToString();
        }

        private static string NormalizeHelpSectionText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var trimmed = text.Trim();
            return string.Equals(trimmed, "?", StringComparison.Ordinal) ? null : trimmed;
        }

        private static void AppendHelpSection(StringBuilder builder, string title, string content)
        {
            if (builder == null || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(content))
            {
                return;
            }

            if (builder.Length > 0)
            {
                builder.AppendLine();
                builder.AppendLine();
            }

            builder.AppendLine(title.Trim());
            builder.Append(content);
        }

        private static void AppendHelpCreditLine(StringBuilder builder, string content)
        {
            if (builder == null || string.IsNullOrWhiteSpace(content))
            {
                return;
            }

            if (builder.Length > 0)
            {
                builder.AppendLine();
                builder.AppendLine();
            }

            builder.Append(content.Trim());
        }

        protected abstract void BuildOrBindSections();

        protected abstract void ResetGame();

        protected abstract void OnPauseRequested();

        protected virtual void OnBeforeDispose()
        {
        }
    }
}
