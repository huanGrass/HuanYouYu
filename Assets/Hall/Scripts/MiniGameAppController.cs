using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    /// <summary>
    /// 大厅主控制器：负责大厅与小游戏切换、进度存档、音频与基础 UI 环境初始化。
    /// </summary>
    public sealed partial class MiniGameAppController : MonoBehaviour, IMiniGameLevelProgressStore, IMiniGameRewardSink, IMiniGameTutorialStore
    {
        public const float ReferenceWidth = 750f;
        public const float ReferenceHeight = 1334f;
        private const string BackgroundMusicResourcePath = "Audio/bgm";
private static readonly string[] DefaultFavoriteGameIds =
        {
            "classic-link",
            "game2048",
            "match-3",
            "water-sort"
        };

        private readonly Dictionary<string, MiniGameProgressData> progressLookup = new Dictionary<string, MiniGameProgressData>();
        private readonly List<string> favoriteGameIds = new List<string>();

        private Canvas rootCanvas;
        private AudioSource backgroundMusicSource;
        private MiniGameSaveStore saveStore;
        private MiniGameHallView hallView;
        private IReadOnlyList<MiniGameDefinition> definitions;
        private MiniGameBase activeGame;
        private string activeGameId;

        public bool IsHallVisible
        {
            get { return hallView != null && hallView.IsVisible; }
        }

        public bool HasActiveGame
        {
            get { return activeGame != null; }
        }

        public string ActiveGameId
        {
            get { return activeGameId; }
        }

        private void Awake()
        {
            EnsureEventSystem();
            EnsureCanvas();
            EnsureBackgroundMusic();
            EnsureSfxPlayer();
            MiniGameRuntimeSettings.Changed += HandleRuntimeSettingsChanged;
            ApplyRuntimeSettings();

            saveStore = new MiniGameSaveStore();
            definitions = MiniGameCatalog.GetDefinitions();
            var loaded = saveStore.Load(definitions);
            foreach (var pair in loaded.ProgressLookup)
            {
                progressLookup[pair.Key] = pair.Value;
            }

            foreach (var favoriteGameId in loaded.FavoriteGameIds)
            {
                if (!favoriteGameIds.Contains(favoriteGameId))
                {
                    favoriteGameIds.Add(favoriteGameId);
                }
            }

            EnsureDefaultFavorites(loaded.HasPersistedState);

            hallView = new MiniGameHallView(rootCanvas.transform, EnterGame, ToggleFavorite);
            RefreshHall();
        }

        private void OnDestroy()
        {
            MiniGameRuntimeSettings.Changed -= HandleRuntimeSettingsChanged;
        }

        private void Update()
        {
            if (activeGame != null)
            {
                activeGame.Tick(Time.deltaTime);
            }
        }

        /// <summary>
        /// 进入指定小游戏；若不可玩或创建失败则回到大厅提示。
        /// </summary>
        public void EnterGame(string gameId)
        {
            var definition = FindDefinition(gameId);
            if (definition == null || !definition.IsPlayable)
            {
                RefreshHall();
                return;
            }

            DisposeActiveGame();
            hallView.Hide();

            activeGameId = gameId;
            activeGame = CreateGameRuntime(gameId);
            if (activeGame == null)
            {
                activeGameId = null;
                RefreshHall();
                return;
            }
        }

        /// <summary>
        /// 主动结束当前小游戏并返回大厅。
        /// </summary>
        public void ExitCurrentGameToHall()
        {
            DisposeActiveGame();
            RefreshHall();
        }

        /// <summary>
        /// 接收小游戏结算并更新进度，然后返回大厅。
        /// </summary>
        public void CompleteCurrentGame(MiniGameSettlement settlement)
        {
            if (string.IsNullOrWhiteSpace(activeGameId))
            {
                return;
            }

            ApplySettlementProgress(activeGameId, settlement, true);

            DisposeActiveGame();

            RefreshHall();
        }

        public void GrantSettlementReward(string gameId, MiniGameSettlement settlement)
        {
            ApplySettlementProgress(gameId, settlement, false);
        }

        private void ApplySettlementProgress(string gameId, MiniGameSettlement settlement, bool countPlay)
        {
            if (settlement == null || string.IsNullOrWhiteSpace(gameId))
            {
                return;
            }

            MiniGameProgressData progress;
            if (!progressLookup.TryGetValue(gameId, out progress))
            {
                progress = MiniGameSaveStore.CreateEmpty(gameId);
                progressLookup[gameId] = progress;
            }

            if (countPlay)
            {
                progress.PlayCount += 1;
            }

            progress.BestScore = Mathf.Max(progress.BestScore, settlement.Score);
            progress.TotalChestCount += Mathf.Max(0, settlement.ChestCount);
            progress.TotalCoinCount += Mathf.Max(0, settlement.CoinCount);
            SaveHallState();
        }

        /// <summary>
        /// 获取指定游戏当前进度，不存在时返回空进度对象。
        /// </summary>
        public MiniGameProgressData GetProgress(string gameId)
        {
            MiniGameProgressData progress;
            if (progressLookup.TryGetValue(gameId, out progress))
            {
                return progress;
            }

            return MiniGameSaveStore.CreateEmpty(gameId);
        }

        public int GetTotalCoinCount()
        {
            var totalCoinCount = 0;
            foreach (var pair in progressLookup)
            {
                var progress = pair.Value;
                if (progress == null)
                {
                    continue;
                }

                totalCoinCount += Mathf.Max(0, progress.TotalCoinCount);
            }

            return totalCoinCount;
        }

        public int GetTotalChestCount()
        {
            var totalChestCount = 0;
            foreach (var pair in progressLookup)
            {
                var progress = pair.Value;
                if (progress == null)
                {
                    continue;
                }

                totalChestCount += Mathf.Max(0, progress.TotalChestCount);
            }

            return totalChestCount;
        }

        public int GetHallGrowthLevel()
        {
            var totalExp = 0;
            foreach (var pair in progressLookup)
            {
                var progress = pair.Value;
                if (progress == null)
                {
                    continue;
                }

                totalExp += Mathf.Max(0, progress.TotalChestCount) * 35;
                totalExp += Mathf.Max(0, progress.TotalCoinCount) / 50;
            }

            var level = 1;
            var expPool = Mathf.Max(0, totalExp);
            var required = GetHallLevelUpRequiredExp(level);
            while (expPool >= required && level < 99)
            {
                expPool -= required;
                level += 1;
                required = GetHallLevelUpRequiredExp(level);
            }

            return level;
        }

        public void SetLevelProgress(string gameId, int currentLevelIndex, int unlockedLevelCount)
        {
            if (string.IsNullOrWhiteSpace(gameId))
            {
                return;
            }

            MiniGameProgressData progress;
            if (!progressLookup.TryGetValue(gameId, out progress))
            {
                progress = MiniGameSaveStore.CreateEmpty(gameId);
                progressLookup[gameId] = progress;
            }

            progress.CurrentLevelIndex = Mathf.Max(0, currentLevelIndex);
            progress.UnlockedLevelCount = Mathf.Max(1, unlockedLevelCount);
            if (progress.CurrentLevelIndex >= progress.UnlockedLevelCount)
            {
                progress.CurrentLevelIndex = progress.UnlockedLevelCount - 1;
            }

            SaveHallState();
        }

        private static int GetHallLevelUpRequiredExp(int level)
        {
            return 100 + Mathf.Max(0, level - 1) * 60;
        }

        public int GetGameTutorialSeenVersion(string gameId)
        {
            if (string.IsNullOrWhiteSpace(gameId))
            {
                return 0;
            }

            MiniGameProgressData progress;
            if (!progressLookup.TryGetValue(gameId, out progress) || progress == null)
            {
                return 0;
            }

            return Mathf.Max(0, progress.TutorialSeenVersion);
        }

        public void SetGameTutorialSeenVersion(string gameId, int version)
        {
            if (string.IsNullOrWhiteSpace(gameId))
            {
                return;
            }

            MiniGameProgressData progress;
            if (!progressLookup.TryGetValue(gameId, out progress))
            {
                progress = MiniGameSaveStore.CreateEmpty(gameId);
                progressLookup[gameId] = progress;
            }

            progress.TutorialSeenVersion = Mathf.Max(progress.TutorialSeenVersion, version);
            SaveHallState();
        }

        public void RefreshHallView()
        {
            if (hallView == null)
            {
                return;
            }

            RefreshHall();
        }

        public void ClearSaveData()
        {
            DisposeActiveGame();
            progressLookup.Clear();
            favoriteGameIds.Clear();

            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition != null && !string.IsNullOrWhiteSpace(definition.Id))
                {
                    progressLookup[definition.Id] = MiniGameSaveStore.CreateEmpty(definition.Id);
                }
            }

            MiniGameSaveStore.ClearPersistedState();
            EnsureDefaultFavorites(false);
            RefreshHall();
        }

        public bool IsFavorite(string gameId)
        {
            return !string.IsNullOrWhiteSpace(gameId) && favoriteGameIds.Contains(gameId);
        }

        public int GetFavoriteOrder(string gameId)
        {
            if (string.IsNullOrWhiteSpace(gameId))
            {
                return -1;
            }

            return favoriteGameIds.IndexOf(gameId);
        }

        public void ToggleFavorite(string gameId)
        {
            if (string.IsNullOrWhiteSpace(gameId) || FindDefinition(gameId) == null)
            {
                return;
            }

            if (favoriteGameIds.Contains(gameId))
            {
                favoriteGameIds.Remove(gameId);
            }
            else
            {
                favoriteGameIds.Add(gameId);
            }

            SaveHallState();

            if (hallView != null && hallView.IsVisible && hallView.IsAllGamesTabActive)
            {
                hallView.RefreshFavoriteState(gameId, IsFavorite(gameId), GetFavoriteOrder(gameId));
                return;
            }

            RefreshHall();
        }

        private void RefreshHall()
        {
            hallView.Show();

            var cards = new List<MiniGameCardViewModel>(definitions.Count);
            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                cards.Add(new MiniGameCardViewModel
                {
                    Definition = definition,
                    Progress = GetProgress(definition.Id),
                    IsFavorite = IsFavorite(definition.Id),
                    FavoriteOrder = GetFavoriteOrder(definition.Id)
                });
            }

            hallView.Refresh(cards);
        }

        private void SaveHallState()
        {
            saveStore.Save(progressLookup, favoriteGameIds);
        }

        private void EnsureDefaultFavorites(bool hasPersistedState)
        {
            if (hasPersistedState)
            {
                return;
            }

            var changed = false;
            for (var i = 0; i < DefaultFavoriteGameIds.Length; i++)
            {
                var gameId = DefaultFavoriteGameIds[i];
                if (string.IsNullOrWhiteSpace(gameId) || FindDefinition(gameId) == null)
                {
                    continue;
                }

                if (!favoriteGameIds.Contains(gameId))
                {
                    favoriteGameIds.Add(gameId);
                    changed = true;
                }
            }

            if (changed)
            {
                SaveHallState();
            }
        }

        private MiniGameDefinition FindDefinition(string gameId)
        {
            for (var i = 0; i < definitions.Count; i++)
            {
                if (definitions[i].Id == gameId)
                {
                    return definitions[i];
                }
            }

            return null;
        }

        private void DisposeActiveGame()
        {
            if (activeGame != null)
            {
                activeGame.Dispose();
                activeGame = null;
            }

            activeGameId = null;
        }

        private void EnsureCanvas()
        {
            rootCanvas = GetComponentInChildren<Canvas>();
            if (rootCanvas != null)
            {
                return;
            }

            var canvasObject = new GameObject("MiniGameCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            rootCanvas = canvasObject.GetComponent<Canvas>();
            rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            rootCanvas.pixelPerfect = false;
            rootCanvas.sortingOrder = 100;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        private static void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null)
            {
                return;
            }

            var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private void EnsureBackgroundMusic()
        {
            EnsureAudioOutputState();

            backgroundMusicSource = GetComponent<AudioSource>();
            if (backgroundMusicSource == null)
            {
                backgroundMusicSource = gameObject.AddComponent<AudioSource>();
            }

            backgroundMusicSource.loop = true;
            backgroundMusicSource.playOnAwake = false;
            backgroundMusicSource.volume = 0.6f;
            backgroundMusicSource.ignoreListenerPause = true;

            if (backgroundMusicSource.clip == null)
            {
                backgroundMusicSource.clip = Resources.Load<AudioClip>(BackgroundMusicResourcePath);
            }

            if (backgroundMusicSource.clip == null)
            {
                Debug.LogWarning("未找到背景音乐资源: Resources/" + BackgroundMusicResourcePath);
                return;
            }

            if (!backgroundMusicSource.isPlaying)
            {
                backgroundMusicSource.Play();
            }
        }

        private void HandleRuntimeSettingsChanged()
        {
            ApplyRuntimeSettings();
        }

        private void ApplyRuntimeSettings()
        {
            if (backgroundMusicSource != null)
            {
                backgroundMusicSource.volume = MiniGameRuntimeSettings.MusicEnabled ? 0.6f : 0f;
            }
        }

        private static void EnsureAudioOutputState()
        {
            AudioListener.pause = false;
            if (AudioListener.volume <= 0.001f)
            {
                AudioListener.volume = 1f;
            }
        }

        private void EnsureSfxPlayer()
        {
            if (GetComponent<MiniGameSfxPlayer>() == null)
            {
                gameObject.AddComponent<MiniGameSfxPlayer>();
            }
        }
    }
}
