using System.Collections.Generic;
using HuanYouYu.MiniGameHall;
using UnityEditor;
using UnityEngine;

namespace HuanYouYu.Editor.MiniGameHall
{
    internal static class HallEditorLevelProgressUtility
    {
        private const string MenuRoot = "幻游域/关卡进度/";
        private const string ResourceMenuRoot = "幻游域/资源/";
        private const int DebugCoinGrant = 999999;
        private const int DebugChestGrant = 9999;

        [MenuItem(MenuRoot + "开放所有关卡进度")]
        public static void OpenAllLevelProgress()
        {
            MiniGameAppController controller;
            if (TryGetRunningController(out controller))
            {
                OpenAllLevelProgress(controller);
                Debug.Log("已开放所有关卡进度。");
                return;
            }

            var loaded = LoadState();
            var changed = false;
            var entries = MiniGameLevelCatalog.GetEntries();
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (string.IsNullOrWhiteSpace(entry.GameId) || entry.LevelCount < 1)
                {
                    continue;
                }

                MiniGameProgressData progress;
                if (!loaded.ProgressLookup.TryGetValue(entry.GameId, out progress))
                {
                    progress = MiniGameSaveStore.CreateEmpty(entry.GameId);
                    loaded.ProgressLookup[entry.GameId] = progress;
                }

                var currentLevelIndex = Mathf.Clamp(progress.CurrentLevelIndex, 0, entry.LevelCount - 1);
                if (progress.CurrentLevelIndex != currentLevelIndex || progress.UnlockedLevelCount != entry.LevelCount)
                {
                    progress.CurrentLevelIndex = currentLevelIndex;
                    progress.UnlockedLevelCount = entry.LevelCount;
                    changed = true;
                }
            }

            SaveAndRefresh(loaded, changed, "已开放所有关卡进度。");
        }

        [MenuItem(MenuRoot + "清除所有关卡进度")]
        public static void ClearAllLevelProgress()
        {
            MiniGameAppController controller;
            if (TryGetRunningController(out controller))
            {
                ClearAllLevelProgress(controller);
                Debug.Log("已清除所有关卡进度。");
                return;
            }

            var loaded = LoadState();
            var changed = false;
            var seenGameIds = new HashSet<string>();
            var entries = MiniGameLevelCatalog.GetEntries();
            for (var i = 0; i < entries.Count; i++)
            {
                changed |= ResetLevelProgress(loaded.ProgressLookup, seenGameIds, entries[i].GameId);
            }

            var definitions = MiniGameCatalog.GetDefinitions();
            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition == null)
                {
                    continue;
                }

                changed |= ResetLevelProgress(loaded.ProgressLookup, seenGameIds, definition.Id);
            }

            SaveAndRefresh(loaded, changed, "已清除所有关卡进度。");
        }

        [MenuItem(MenuRoot + "清空存档")]
        public static void ClearAllSaveData()
        {
            MiniGameAppController controller;
            if (TryGetRunningController(out controller))
            {
                controller.ClearSaveData();
                Debug.Log("已清空存档。");
                return;
            }

            MiniGameSaveStore.ClearPersistedState();
            Debug.Log("已清空存档。");
        }

        [MenuItem(ResourceMenuRoot + "增加大量金币和宝箱")]
        public static void GrantLotsOfCoinsAndChests()
        {
            string gameId;
            if (!TryGetResourceGrantGameId(out gameId))
            {
                Debug.LogWarning("未找到可写入资源的玩法进度。");
                return;
            }

            var settlement = new MiniGameSettlement
            {
                CoinCount = DebugCoinGrant,
                ChestCount = DebugChestGrant,
                Summary = "编辑器工具增加资源"
            };

            MiniGameAppController controller;
            if (TryGetRunningController(out controller))
            {
                controller.GrantSettlementReward(gameId, settlement);
                controller.RefreshHallView();
                Debug.Log("已增加金币 " + DebugCoinGrant + "、宝箱 " + DebugChestGrant + "。");
                return;
            }

            var loaded = LoadState();
            MiniGameProgressData progress;
            if (!loaded.ProgressLookup.TryGetValue(gameId, out progress))
            {
                progress = MiniGameSaveStore.CreateEmpty(gameId);
                loaded.ProgressLookup[gameId] = progress;
            }

            progress.TotalCoinCount += DebugCoinGrant;
            progress.TotalChestCount += DebugChestGrant;
            SaveAndRefresh(loaded, true, "已增加金币 " + DebugCoinGrant + "、宝箱 " + DebugChestGrant + "。");
        }

        private static void OpenAllLevelProgress(MiniGameAppController controller)
        {
            var entries = MiniGameLevelCatalog.GetEntries();
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (string.IsNullOrWhiteSpace(entry.GameId) || entry.LevelCount < 1)
                {
                    continue;
                }

                var progress = controller.GetProgress(entry.GameId);
                var currentLevelIndex = progress != null ? progress.CurrentLevelIndex : 0;
                controller.SetLevelProgress(
                    entry.GameId,
                    Mathf.Clamp(currentLevelIndex, 0, entry.LevelCount - 1),
                    entry.LevelCount);
            }

            controller.RefreshHallView();
        }

        private static void ClearAllLevelProgress(MiniGameAppController controller)
        {
            var seenGameIds = new HashSet<string>();
            var entries = MiniGameLevelCatalog.GetEntries();
            for (var i = 0; i < entries.Count; i++)
            {
                var gameId = entries[i].GameId;
                if (string.IsNullOrWhiteSpace(gameId) || !seenGameIds.Add(gameId))
                {
                    continue;
                }

                controller.SetLevelProgress(gameId, 0, 1);
            }

            var definitions = MiniGameCatalog.GetDefinitions();
            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition == null || string.IsNullOrWhiteSpace(definition.Id) || !seenGameIds.Add(definition.Id))
                {
                    continue;
                }

                controller.SetLevelProgress(definition.Id, 0, 1);
            }

            controller.RefreshHallView();
        }

        private static MiniGameSaveStore.LoadedState LoadState()
        {
            var store = new MiniGameSaveStore();
            return store.Load(MiniGameCatalog.GetDefinitions());
        }

        private static bool TryGetResourceGrantGameId(out string gameId)
        {
            gameId = string.Empty;
            var definitions = MiniGameCatalog.GetDefinitions();
            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
                {
                    continue;
                }

                gameId = definition.Id;
                return true;
            }

            return false;
        }

        private static bool ResetLevelProgress(
            Dictionary<string, MiniGameProgressData> progressLookup,
            HashSet<string> seenGameIds,
            string gameId)
        {
            if (string.IsNullOrWhiteSpace(gameId) || !seenGameIds.Add(gameId))
            {
                return false;
            }

            MiniGameProgressData progress;
            if (!progressLookup.TryGetValue(gameId, out progress))
            {
                progress = MiniGameSaveStore.CreateEmpty(gameId);
                progressLookup[gameId] = progress;
            }

            if (progress.CurrentLevelIndex == 0 && progress.UnlockedLevelCount == 1)
            {
                return false;
            }

            progress.CurrentLevelIndex = 0;
            progress.UnlockedLevelCount = 1;
            return true;
        }

        private static void SaveAndRefresh(MiniGameSaveStore.LoadedState loaded, bool changed, string message)
        {
            var store = new MiniGameSaveStore();
            store.Save(loaded.ProgressLookup, loaded.FavoriteGameIds);

            Debug.Log(changed ? message : message + " 当前存档无需变化。");
        }

        private static bool TryGetRunningController(out MiniGameAppController controller)
        {
            controller = null;
            if (!EditorApplication.isPlaying)
            {
                return false;
            }

            controller = Object.FindObjectOfType<MiniGameAppController>();
            return controller != null;
        }
    }
}
