using System;
using UnityEngine;

namespace HuanYouYu.MiniGameHall
{
    /// <summary>
    /// 小游戏运行时设置，负责暂停弹窗设置项的读写与默认值管理。
    /// </summary>
    public static class MiniGameRuntimeSettings
    {
        public const string PlayerPrefsKey = "huanyouyu.mini_game_hall.runtime_settings";

        [Serializable]
        private sealed class RuntimeSettingsData
        {
            public bool MusicEnabled = true;
            public bool SfxEnabled = true;
            public bool VibrationEnabled;
        }

        private static RuntimeSettingsData cachedData;

        public static event Action Changed;

        public static bool MusicEnabled
        {
            get { return GetData().MusicEnabled; }
        }

        public static bool SfxEnabled
        {
            get { return GetData().SfxEnabled; }
        }

        public static bool VibrationEnabled
        {
            get { return GetData().VibrationEnabled; }
        }

        public static void SetMusicEnabled(bool enabled)
        {
            Update(delegate(RuntimeSettingsData data) { data.MusicEnabled = enabled; });
        }

        public static void SetSfxEnabled(bool enabled)
        {
            Update(delegate(RuntimeSettingsData data) { data.SfxEnabled = enabled; });
        }

        public static void SetVibrationEnabled(bool enabled)
        {
            Update(delegate(RuntimeSettingsData data) { data.VibrationEnabled = enabled; });
        }

        private static RuntimeSettingsData GetData()
        {
            if (cachedData != null)
            {
                if (!PlayerPrefs.HasKey(PlayerPrefsKey))
                {
                    cachedData = new RuntimeSettingsData();
                }

                return cachedData;
            }

            if (!PlayerPrefs.HasKey(PlayerPrefsKey))
            {
                cachedData = new RuntimeSettingsData();
                return cachedData;
            }

            var rawJson = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
            cachedData = string.IsNullOrWhiteSpace(rawJson)
                ? new RuntimeSettingsData()
                : JsonUtility.FromJson<RuntimeSettingsData>(rawJson);

            if (cachedData == null)
            {
                cachedData = new RuntimeSettingsData();
            }

            return cachedData;
        }

        private static void Update(Action<RuntimeSettingsData> applyChange)
        {
            var data = GetData();
            var oldMusic = data.MusicEnabled;
            var oldSfx = data.SfxEnabled;
            var oldVibration = data.VibrationEnabled;

            applyChange(data);
            if (oldMusic == data.MusicEnabled &&
                oldSfx == data.SfxEnabled &&
                oldVibration == data.VibrationEnabled)
            {
                return;
            }

            PlayerPrefs.SetString(PlayerPrefsKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
