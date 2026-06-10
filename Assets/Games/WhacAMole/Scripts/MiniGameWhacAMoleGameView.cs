using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    /// <summary>
    /// 打地鼠运行体：动态构建 3x3 洞位、计时、得分和结算流程。
    /// </summary>
    public sealed class MiniGameWhacAMoleGameView : MiniGameBase
    {
        public const string GameIdConstant = "whacamole";

        private const int HoleCount = 9;
        private const float RoundDuration = 30f;
        private const float MinSpawnDelay = 0.34f;
        private const float MaxSpawnDelay = 0.9f;
        private const float MinMoleLifetime = 0.56f;
        private const float MaxMoleLifetime = 1.08f;
        private const float HammerEffectDuration = 0.28f;
        private const float MoleEmergeEffectDuration = 0.18f;
        private const float MoleHideEffectDuration = 0.16f;
        private const float MoleHitEffectDuration = 0.18f;
        private const int HitScore = 10;
        private const int MissPenalty = 1;

        private TextMeshProUGUI titleLabel;
        private TextMeshProUGUI scoreLabel;
        private TextMeshProUGUI timerLabel;
        private TextMeshProUGUI statusLabel;
        private Button restartButton;
        private MoleHole[] holes;
        private Coroutine hammerEffectRoutine;
        private Coroutine moleEmergeEffectRoutine;
        private Coroutine moleHideEffectRoutine;
        private Coroutine moleHitEffectRoutine;
        private int score;
        private int hitCount;
        private int missCount;
        private int activeHoleIndex = -1;
        private float timeRemaining;
        private float spawnDelayRemaining;
        private float moleLifetimeRemaining;
        private bool isRunning;
        private bool isPaused;

        public MiniGameWhacAMoleGameView(
            MonoBehaviour hostBehaviour,
            Transform parent,
            Action<MiniGameSettlement> onComplete,
            Action onExit)
            : base(GameIdConstant, "MiniGameWhacAMoleView", hostBehaviour, parent, onComplete, onExit)
        {
        }

        protected override void BuildOrBindSections()
        {
            var topBarRefs = MiniGameShellTopBarBuilder.CreateTopBar(
                Shell.TopHost,
                MiniGameShellTopBarBuilder.CreateDefaultConfig("WhacAMoleHeader"));
            titleLabel = topBarRefs.TitleText;
            scoreLabel = topBarRefs.ScoreText;

            var bottomContainerRefs = MiniGameShellBottomBarBuilder.CreateBottomContainer(
                Shell.BottomHost,
                MiniGameShellBottomBarBuilder.CreateDefaultContainerConfig("WhacAMoleActions"));
            restartButton = MiniGameShellBottomBarBuilder.CreateRestartButton(bottomContainerRefs.ActionBar).Button;

            BuildPlayfield();

            if (restartButton != null)
            {
                restartButton.gameObject.name = "RestartButton";
                restartButton.onClick.RemoveAllListeners();
                restartButton.onClick.AddListener(OnRestartClicked);
            }

            if (titleLabel == null || scoreLabel == null || timerLabel == null || statusLabel == null || restartButton == null || holes == null || holes.Length != HoleCount)
            {
                throw new InvalidOperationException("WhacAMole prefab structure is incomplete.");
            }
        }

        protected override void ResetGame()
        {
            Shell.ClosePopup();
            ClearActiveMole();
            score = 0;
            hitCount = 0;
            missCount = 0;
            timeRemaining = RoundDuration;
            spawnDelayRemaining = 0.16f;
            moleLifetimeRemaining = 0f;
            isRunning = true;
            isPaused = false;
            RefreshHud();
            UpdateStatus("whacamole.status.ready");
        }

        protected override (string helpKey, string creditsKey)? GetPauseHelpKeys()
        {
            return ("game.whacamole.help", null);
        }

        public override void Tick(float deltaTime)
        {
            if (!isRunning || isPaused || deltaTime <= 0f)
            {
                return;
            }

            timeRemaining = Mathf.Max(0f, timeRemaining - deltaTime);
            if (activeHoleIndex >= 0)
            {
                moleLifetimeRemaining -= deltaTime;
                if (moleLifetimeRemaining <= 0f)
                {
                    MissActiveMole();
                }
            }
            else
            {
                spawnDelayRemaining -= deltaTime;
                if (spawnDelayRemaining <= 0f)
                {
                    SpawnMole();
                }
            }

            RefreshHud();

            if (timeRemaining <= 0f)
            {
                FinishRound();
            }
        }

        protected override void OnPauseRequested()
        {
            if (!isRunning)
            {
                return;
            }

            isPaused = true;
            Shell.ShowPausePopup(ResumeFromPause, ConfirmExitToHall);
        }

        protected override void OnBeforeDispose()
        {
            Shell.ClosePopup();

            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(OnRestartClicked);
            }

            StopEffectRoutines();

            if (holes == null)
            {
                return;
            }

            for (var i = 0; i < holes.Length; i++)
            {
                if (holes[i] != null && holes[i].Button != null)
                {
                    holes[i].Button.onClick.RemoveAllListeners();
                }
            }
        }

        private string BuildScoreText()
        {
            return UiTextCatalog.Format("whacamole.hud.score", score);
        }

        private string BuildTimerText()
        {
            return UiTextCatalog.Format("whacamole.hud.timer", Mathf.CeilToInt(timeRemaining));
        }

        private void RefreshHud()
        {
            if (titleLabel != null)
            {
                titleLabel.text = UiTextCatalog.Get("game.whacamole.name");
            }

            if (scoreLabel != null)
            {
                scoreLabel.text = BuildScoreText();
            }

            if (timerLabel != null)
            {
                timerLabel.text = BuildTimerText();
            }
        }

        private void ResumeFromPause()
        {
            Shell.ClosePopup();
            isPaused = false;
        }

        private void ConfirmExitToHall()
        {
            Shell.ClosePopup();
            isRunning = false;
            isPaused = true;
            ClearActiveMole();
            RefreshHud();
            MiniGameSfxPlayer.Play(MiniGameSfxType.Settle, 0.92f);
            var settlement = BuildSettlement();
            ShowBackHallRewardSettlementPanel(
                settlement,
                "WhacAMoleSettlementPanel",
                new MiniGameSettlementInfoRow(UiTextCatalog.Get("whacamole.settlement.score"), score.ToString()),
                new MiniGameSettlementInfoRow(UiTextCatalog.Get("whacamole.settlement.hit_miss"), hitCount + "/" + missCount),
                delegate { CompleteGame?.Invoke(settlement); });
        }

        private void OnRestartClicked()
        {
            MiniGameSfxPlayer.Play(MiniGameSfxType.UiTap, 0.95f);
            ResetGame();
        }

        private void BuildPlayfield()
        {
            var root = CreateRectObject("WhacAMolePlayfield", Shell.ContentHost);
            var rootRect = root.GetComponent<RectTransform>();
            Stretch(rootRect, Vector2.zero, Vector2.one, new Vector2(42f, 28f), new Vector2(-42f, -20f));

            var panel = CreateRoundedRect("FieldPanel", rootRect, new Color(0.95f, 0.88f, 0.66f, 0.92f), 34f);
            Stretch(panel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            panel.raycastTarget = false;

            var contentObject = CreateRectObject("Content", rootRect);
            var content = contentObject.GetComponent<RectTransform>();
            Stretch(content, Vector2.zero, Vector2.one, new Vector2(24f, 24f), new Vector2(-24f, -24f));

            var layout = contentObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.spacing = 22f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var infoRow = CreateRectObject("InfoRow", content);
            var infoRowRect = infoRow.GetComponent<RectTransform>();
            infoRowRect.sizeDelta = new Vector2(0f, 76f);
            var infoLayoutElement = infoRow.AddComponent<LayoutElement>();
            infoLayoutElement.preferredHeight = 76f;

            var infoLayout = infoRow.AddComponent<HorizontalLayoutGroup>();
            infoLayout.spacing = 18f;
            infoLayout.childAlignment = TextAnchor.MiddleCenter;
            infoLayout.childControlWidth = true;
            infoLayout.childControlHeight = true;
            infoLayout.childForceExpandWidth = true;
            infoLayout.childForceExpandHeight = true;

            timerLabel = CreateInfoText("Timer", infoRowRect, new Color(0.82f, 0.29f, 0.20f, 1f), FontStyles.Bold, 28f);
            statusLabel = CreateInfoText("Status", infoRowRect, new Color(0.24f, 0.34f, 0.20f, 1f), FontStyles.Bold, 24f);

            var gridObject = CreateRectObject("HoleGrid", content);
            var gridRect = gridObject.GetComponent<RectTransform>();
            gridRect.sizeDelta = new Vector2(590f, 590f);
            var gridLayoutElement = gridObject.AddComponent<LayoutElement>();
            gridLayoutElement.preferredWidth = 590f;
            gridLayoutElement.preferredHeight = 590f;

            var grid = gridObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(178f, 178f);
            grid.spacing = new Vector2(20f, 20f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.childAlignment = TextAnchor.MiddleCenter;

            holes = new MoleHole[HoleCount];
            for (var i = 0; i < holes.Length; i++)
            {
                holes[i] = CreateHole(gridRect, i);
            }
        }

        private MoleHole CreateHole(Transform parent, int index)
        {
            var holeObject = new GameObject("Hole" + index, typeof(RectTransform), typeof(Button), typeof(LayoutElement));
            var holeRect = holeObject.GetComponent<RectTransform>();
            holeRect.SetParent(parent, false);
            holeRect.sizeDelta = new Vector2(178f, 178f);

            var hitArea = holeObject.AddComponent<Image>();
            hitArea.color = new Color(1f, 1f, 1f, 0f);
            hitArea.raycastTarget = true;

            var layout = holeObject.GetComponent<LayoutElement>();
            layout.preferredWidth = 178f;
            layout.preferredHeight = 178f;

            var shadow = CreateRoundedRect("Shadow", holeRect, new Color(0.24f, 0.19f, 0.12f, 0.28f), 80f);
            Stretch(shadow.rectTransform, Vector2.zero, Vector2.one, new Vector2(14f, 8f), new Vector2(-14f, -28f));
            shadow.raycastTarget = false;

            var dirt = CreateRoundedRect("Dirt", holeRect, new Color(0.39f, 0.24f, 0.13f, 1f), 76f);
            Stretch(dirt.rectTransform, Vector2.zero, Vector2.one, new Vector2(12f, 16f), new Vector2(-12f, -26f));
            dirt.raycastTarget = true;

            var rim = CreateRoundedRect("Rim", holeRect, new Color(0.56f, 0.34f, 0.18f, 1f), 66f);
            Stretch(rim.rectTransform, Vector2.zero, Vector2.one, new Vector2(24f, 26f), new Vector2(-24f, -42f));
            rim.raycastTarget = false;

            var mole = CreateRectObject("Mole", holeRect);
            var moleRect = mole.GetComponent<RectTransform>();
            moleRect.anchorMin = new Vector2(0.5f, 0.5f);
            moleRect.anchorMax = new Vector2(0.5f, 0.5f);
            moleRect.pivot = new Vector2(0.5f, 0.5f);
            moleRect.anchoredPosition = new Vector2(0f, 28f);
            moleRect.sizeDelta = new Vector2(106f, 126f);

            var face = CreateRoundedRect("Face", moleRect, new Color(0.61f, 0.38f, 0.20f, 1f), 53f);
            Stretch(face.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            face.raycastTarget = false;
            CreateDot("LeftEye", moleRect, new Vector2(-22f, 24f), new Vector2(12f, 16f), Color.black);
            CreateDot("RightEye", moleRect, new Vector2(22f, 24f), new Vector2(12f, 16f), Color.black);
            CreateDot("Nose", moleRect, new Vector2(0f, -2f), new Vector2(22f, 16f), new Color(0.96f, 0.68f, 0.58f, 1f));
            var hitExpression = CreateHitExpression(moleRect);

            var hammer = CreateHammer(holeRect);

            var button = holeObject.GetComponent<Button>();
            button.targetGraphic = hitArea;
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.06f, 1.06f, 1.06f, 1f);
            colors.pressedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.58f, 0.58f, 0.58f, 0.65f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            var capturedIndex = index;
            button.onClick.AddListener(delegate { OnHoleClicked(capturedIndex); });
            mole.SetActive(false);

            return new MoleHole(button, mole, moleRect, hammer, hitExpression, moleRect.anchoredPosition);
        }

        private void OnHoleClicked(int index)
        {
            if (!isRunning || isPaused)
            {
                return;
            }

            PlayHammerEffect(index);

            if (index == activeHoleIndex)
            {
                score += HitScore;
                hitCount++;
                PlayMoleHitEffect(index);
                activeHoleIndex = -1;
                moleLifetimeRemaining = 0f;
                spawnDelayRemaining = UnityEngine.Random.Range(MinSpawnDelay, MaxSpawnDelay);
                MiniGameSfxPlayer.Play(MiniGameSfxType.MatchSuccess, 0.9f, 1f + Mathf.Min(hitCount, 8) * 0.03f);
                UpdateStatus("whacamole.status.hit");
                RefreshHud();
                return;
            }

            score = Mathf.Max(0, score - MissPenalty);
            missCount++;
            MiniGameSfxPlayer.Play(MiniGameSfxType.MatchFail, 0.6f);
            UpdateStatus("whacamole.status.miss");
            RefreshHud();
        }

        private void SpawnMole()
        {
            if (holes == null || holes.Length == 0)
            {
                return;
            }

            var index = UnityEngine.Random.Range(0, holes.Length);
            activeHoleIndex = index;
            moleLifetimeRemaining = Mathf.Lerp(MaxMoleLifetime, MinMoleLifetime, 1f - (timeRemaining / RoundDuration));
            ResetMoleVisual(holes[index]);
            holes[index].MoleRoot.SetActive(true);
            PlayMoleEmergeEffect(index);
            UpdateStatus("whacamole.status.find");
        }

        private void MissActiveMole()
        {
            if (activeHoleIndex < 0)
            {
                return;
            }

            var missedHoleIndex = activeHoleIndex;
            activeHoleIndex = -1;
            moleLifetimeRemaining = 0f;
            missCount++;
            spawnDelayRemaining = UnityEngine.Random.Range(MinSpawnDelay, MaxSpawnDelay);
            PlayMoleHideEffect(missedHoleIndex);
            UpdateStatus("whacamole.status.escape");
        }

        private void ClearActiveMole()
        {
            StopMoleEmergeEffect();
            StopMoleHideEffect();
            StopMoleHitEffect();

            if (holes != null)
            {
                for (var i = 0; i < holes.Length; i++)
                {
                    if (holes[i] != null && holes[i].MoleRoot != null)
                    {
                        ResetMoleVisual(holes[i]);
                        holes[i].MoleRoot.SetActive(false);
                    }
                }
            }

            activeHoleIndex = -1;
            moleLifetimeRemaining = 0f;
        }

        private void FinishRound()
        {
            isRunning = false;
            isPaused = true;
            ClearActiveMole();
            RefreshHud();
            MiniGameSfxPlayer.Play(MiniGameSfxType.Settle, 0.92f);

            var settlement = BuildSettlement();
            ShowRewardSettlementPanel(
                settlement,
                new MiniGameRewardSettlementPanelParams
                {
                    RootName = "WhacAMoleSettlementPanel",
                    Style = MiniGameRewardSettlementPanelStyle.Success,
                    PrimaryAction = MiniGameRewardSettlementPrimaryAction.Retry,
                    Title = UiTextCatalog.Get("whacamole.settlement.win_title"),
                    PrimaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("whacamole.settlement.score"), score.ToString()),
                    SecondaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("whacamole.settlement.hit_miss"), hitCount + "/" + missCount),
                    RewardLabel = UiTextCatalog.Get("settlement.reward_label"),
                    CoinCount = settlement.CoinCount,
                    ChestCount = settlement.ChestCount
                },
                ResetGame,
                delegate { CompleteGame?.Invoke(settlement); },
                true);
        }

        private MiniGameSettlement BuildSettlement()
        {
            return new MiniGameSettlement
            {
                Score = score,
                ChestCount = Mathf.Clamp(score / 80, 0, 3),
                CoinCount = Mathf.Max(1, score / 10),
                Summary = UiTextCatalog.Format("whacamole.settlement.summary", score, hitCount, missCount)
            };
        }

        private void UpdateStatus(string key)
        {
            if (statusLabel != null)
            {
                statusLabel.text = UiTextCatalog.Get(key);
            }
        }

        private GameObject CreateHammer(Transform parent)
        {
            var hammer = CreateRectObject("Hammer", parent);
            var hammerRect = hammer.GetComponent<RectTransform>();
            hammerRect.anchorMin = new Vector2(0.5f, 0.5f);
            hammerRect.anchorMax = new Vector2(0.5f, 0.5f);
            hammerRect.pivot = new Vector2(0.5f, 0.12f);
            hammerRect.anchoredPosition = new Vector2(120f, 42f);
            hammerRect.sizeDelta = new Vector2(150f, 150f);
            hammer.transform.SetAsLastSibling();

            var handle = CreateRoundedRect("WoodHandle", hammerRect, new Color(0.62f, 0.38f, 0.18f, 1f), 13f);
            var handleRect = handle.rectTransform;
            handleRect.anchorMin = new Vector2(0.5f, 0.5f);
            handleRect.anchorMax = new Vector2(0.5f, 0.5f);
            handleRect.pivot = new Vector2(0.5f, 0.5f);
            handleRect.anchoredPosition = new Vector2(0f, 4f);
            handleRect.sizeDelta = new Vector2(30f, 122f);
            handle.raycastTarget = false;

            var head = CreateRoundedRect("WoodHead", hammerRect, new Color(0.82f, 0.58f, 0.30f, 1f), 20f);
            var headRect = head.rectTransform;
            headRect.anchorMin = new Vector2(0.5f, 0.5f);
            headRect.anchorMax = new Vector2(0.5f, 0.5f);
            headRect.pivot = new Vector2(0.5f, 0.5f);
            headRect.anchoredPosition = new Vector2(0f, 74f);
            headRect.sizeDelta = new Vector2(132f, 54f);
            head.raycastTarget = false;

            hammer.SetActive(false);
            return hammer;
        }

        private void PlayHammerEffect(int index)
        {
            if (holes == null || index < 0 || index >= holes.Length || holes[index] == null || holes[index].HammerRoot == null)
            {
                return;
            }

            if (hammerEffectRoutine != null)
            {
                HostBehaviour.StopCoroutine(hammerEffectRoutine);
                hammerEffectRoutine = null;
            }

            HideAllHammers();
            hammerEffectRoutine = HostBehaviour.StartCoroutine(AnimateHammer(holes[index]));
        }

        private IEnumerator AnimateHammer(MoleHole hole)
        {
            if (hole == null || hole.HammerRoot == null)
            {
                yield break;
            }

            var hammer = hole.HammerRoot;
            var rect = hammer.transform as RectTransform;
            hammer.SetActive(true);
            hammer.transform.SetAsLastSibling();

            var elapsed = 0f;
            while (elapsed < HammerEffectDuration && rect != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / HammerEffectDuration);
                var strike = t < 0.46f ? Mathf.SmoothStep(0f, 1f, t / 0.46f) : Mathf.SmoothStep(1f, 0f, (t - 0.46f) / 0.54f);
                rect.anchoredPosition = Vector2.Lerp(new Vector2(120f, 74f), new Vector2(126f, 42f), strike);
                rect.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(0f, 90f, strike));
                rect.localScale = Vector3.one * Mathf.Lerp(1.04f, 0.98f, strike);
                yield return null;
            }

            if (hammer != null)
            {
                hammer.SetActive(false);
            }

            hammerEffectRoutine = null;
        }

        private void PlayMoleHitEffect(int index)
        {
            if (holes == null || index < 0 || index >= holes.Length || holes[index] == null)
            {
                return;
            }

            StopMoleEmergeEffect();
            StopMoleHitEffect();
            moleHitEffectRoutine = HostBehaviour.StartCoroutine(AnimateMoleHit(holes[index]));
        }

        private void PlayMoleEmergeEffect(int index)
        {
            if (holes == null || index < 0 || index >= holes.Length || holes[index] == null)
            {
                return;
            }

            StopMoleEmergeEffect();
            moleEmergeEffectRoutine = HostBehaviour.StartCoroutine(AnimateMoleEmerge(holes[index]));
        }

        private void PlayMoleHideEffect(int index)
        {
            if (holes == null || index < 0 || index >= holes.Length || holes[index] == null)
            {
                return;
            }

            StopMoleEmergeEffect();
            StopMoleHideEffect();
            moleHideEffectRoutine = HostBehaviour.StartCoroutine(AnimateMoleHide(holes[index]));
        }

        private IEnumerator AnimateMoleEmerge(MoleHole hole)
        {
            if (hole == null || hole.MoleRoot == null || hole.MoleRect == null)
            {
                yield break;
            }

            var hiddenPosition = hole.MoleDefaultPosition + new Vector2(0f, -62f);
            var elapsed = 0f;
            hole.MoleRoot.SetActive(true);
            while (elapsed < MoleEmergeEffectDuration && hole.MoleRect != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / MoleEmergeEffectDuration));
                hole.MoleRect.anchoredPosition = Vector2.Lerp(hiddenPosition, hole.MoleDefaultPosition, t);
                hole.MoleRect.localScale = Vector3.one * Mathf.Lerp(0.74f, 1f, t);
                yield return null;
            }

            ResetMoleVisual(hole);
            moleEmergeEffectRoutine = null;
        }

        private IEnumerator AnimateMoleHide(MoleHole hole)
        {
            if (hole == null || hole.MoleRoot == null || hole.MoleRect == null)
            {
                yield break;
            }

            var hiddenPosition = hole.MoleDefaultPosition + new Vector2(0f, -62f);
            var elapsed = 0f;
            hole.MoleRoot.SetActive(true);
            while (elapsed < MoleHideEffectDuration && hole.MoleRect != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / MoleHideEffectDuration));
                hole.MoleRect.anchoredPosition = Vector2.Lerp(hole.MoleDefaultPosition, hiddenPosition, t);
                hole.MoleRect.localScale = Vector3.one * Mathf.Lerp(1f, 0.74f, t);
                yield return null;
            }

            ResetMoleVisual(hole);
            if (hole.MoleRoot != null)
            {
                hole.MoleRoot.SetActive(false);
            }

            moleHideEffectRoutine = null;
        }

        private IEnumerator AnimateMoleHit(MoleHole hole)
        {
            if (hole == null || hole.MoleRoot == null || hole.MoleRect == null)
            {
                yield break;
            }

            hole.MoleRoot.SetActive(true);
            if (hole.HitExpressionRoot != null)
            {
                hole.HitExpressionRoot.SetActive(true);
            }

            var elapsed = 0f;
            while (elapsed < MoleHitEffectDuration && hole.MoleRect != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / MoleHitEffectDuration);
                var squash = t < 0.45f ? Mathf.SmoothStep(0f, 1f, t / 0.45f) : Mathf.SmoothStep(1f, 0f, (t - 0.45f) / 0.55f);
                hole.MoleRect.localScale = new Vector3(Mathf.Lerp(1f, 1.22f, squash), Mathf.Lerp(1f, 0.58f, squash), 1f);
                hole.MoleRect.anchoredPosition = Vector2.Lerp(hole.MoleDefaultPosition, hole.MoleDefaultPosition + new Vector2(0f, -28f), squash);
                yield return null;
            }

            if (hole.HitExpressionRoot != null)
            {
                hole.HitExpressionRoot.SetActive(false);
            }

            ResetMoleVisual(hole);
            if (hole.MoleRoot != null)
            {
                hole.MoleRoot.SetActive(false);
            }

            moleHitEffectRoutine = null;
        }

        private void StopEffectRoutines()
        {
            if (hammerEffectRoutine != null)
            {
                HostBehaviour.StopCoroutine(hammerEffectRoutine);
                hammerEffectRoutine = null;
            }

            HideAllHammers();
            StopMoleEmergeEffect();
            StopMoleHideEffect();
            StopMoleHitEffect();
        }

        private void StopMoleEmergeEffect()
        {
            if (moleEmergeEffectRoutine != null)
            {
                HostBehaviour.StopCoroutine(moleEmergeEffectRoutine);
                moleEmergeEffectRoutine = null;
            }
        }

        private void StopMoleHitEffect()
        {
            if (moleHitEffectRoutine != null)
            {
                HostBehaviour.StopCoroutine(moleHitEffectRoutine);
                moleHitEffectRoutine = null;
            }
        }

        private void StopMoleHideEffect()
        {
            if (moleHideEffectRoutine != null)
            {
                HostBehaviour.StopCoroutine(moleHideEffectRoutine);
                moleHideEffectRoutine = null;
            }
        }

        private static void ResetMoleVisual(MoleHole hole)
        {
            if (hole == null)
            {
                return;
            }

            if (hole.MoleRect != null)
            {
                hole.MoleRect.localScale = Vector3.one;
                hole.MoleRect.anchoredPosition = hole.MoleDefaultPosition;
            }

            if (hole.HitExpressionRoot != null)
            {
                hole.HitExpressionRoot.SetActive(false);
            }

            if (hole.HammerRoot != null)
            {
                hole.HammerRoot.SetActive(false);
            }
        }

        private void HideAllHammers()
        {
            if (holes == null)
            {
                return;
            }

            for (var i = 0; i < holes.Length; i++)
            {
                if (holes[i] != null && holes[i].HammerRoot != null)
                {
                    holes[i].HammerRoot.SetActive(false);
                }
            }
        }

        private static TextMeshProUGUI CreateInfoText(string name, Transform parent, Color color, FontStyles fontStyle, float fontSize)
        {
            var root = CreateRoundedRect(name + "Panel", parent, new Color(1f, 0.98f, 0.91f, 0.82f), 24f);
            var rootRect = root.rectTransform;
            var layout = root.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 68f;
            layout.flexibleWidth = 1f;

            var labelObject = CreateRectObject(name, rootRect);
            var labelRect = labelObject.GetComponent<RectTransform>();
            Stretch(labelRect, Vector2.zero, Vector2.one, new Vector2(12f, 6f), new Vector2(-12f, -6f));

            var label = labelObject.AddComponent<TextMeshProUGUI>();
            label.color = color;
            label.fontSize = fontSize;
            label.fontStyle = fontStyle;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.raycastTarget = false;
            return label;
        }

        private static void CreateDot(string name, Transform parent, Vector2 anchoredPosition, Vector2 size, Color color)
        {
            var dot = CreateRoundedRect(name, parent, color, Mathf.Min(size.x, size.y) * 0.5f);
            var rect = dot.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            dot.raycastTarget = false;
        }

        private static GameObject CreateHitExpression(Transform parent)
        {
            var expression = CreateRectObject("HitExpression", parent);
            var expressionRect = expression.GetComponent<RectTransform>();
            Stretch(expressionRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            CreateCrossEye(expressionRect, "LeftDizzyEye", new Vector2(-22f, 24f));
            CreateCrossEye(expressionRect, "RightDizzyEye", new Vector2(22f, 24f));

            expression.SetActive(false);
            return expression;
        }

        private static void CreateCrossEye(Transform parent, string name, Vector2 anchoredPosition)
        {
            var root = CreateRectObject(name, parent);
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = anchoredPosition;
            rootRect.sizeDelta = new Vector2(28f, 28f);

            CreateExpressionStroke("StrokeA", rootRect, 45f);
            CreateExpressionStroke("StrokeB", rootRect, -45f);
        }

        private static void CreateExpressionStroke(string name, Transform parent, float zRotation)
        {
            var stroke = CreateRoundedRect(name, parent, Color.black, 3f);
            var strokeRect = stroke.rectTransform;
            strokeRect.anchorMin = new Vector2(0.5f, 0.5f);
            strokeRect.anchorMax = new Vector2(0.5f, 0.5f);
            strokeRect.pivot = new Vector2(0.5f, 0.5f);
            strokeRect.anchoredPosition = Vector2.zero;
            strokeRect.sizeDelta = new Vector2(26f, 6f);
            strokeRect.localEulerAngles = new Vector3(0f, 0f, zRotation);
            stroke.raycastTarget = false;
        }

        private static RoundedRectGraphic CreateRoundedRect(string name, Transform parent, Color color, float cornerRadius)
        {
            var graphicObject = CreateRectObject(name, parent);
            graphicObject.AddComponent<CanvasRenderer>();
            var graphic = graphicObject.AddComponent<RoundedRectGraphic>();
            graphic.color = color;
            graphic.CornerRadius = cornerRadius;
            return graphic;
        }

        private static GameObject CreateRectObject(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.localScale = Vector3.one;
        }

        private sealed class MoleHole
        {
            public MoleHole(
                Button button,
                GameObject moleRoot,
                RectTransform moleRect,
                GameObject hammerRoot,
                GameObject hitExpressionRoot,
                Vector2 moleDefaultPosition)
            {
                Button = button;
                MoleRoot = moleRoot;
                MoleRect = moleRect;
                HammerRoot = hammerRoot;
                HitExpressionRoot = hitExpressionRoot;
                MoleDefaultPosition = moleDefaultPosition;
            }

            public Button Button { get; }

            public GameObject MoleRoot { get; }

            public RectTransform MoleRect { get; }

            public GameObject HammerRoot { get; }

            public GameObject HitExpressionRoot { get; }

            public Vector2 MoleDefaultPosition { get; }
        }
    }
}
