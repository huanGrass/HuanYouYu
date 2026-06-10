using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace HuanYouYu.MiniGameHall
{
    internal static class HallAnnouncementCatalog
    {
        private const string UpdatesResourcePath = "Announcements/hall.updates.zh-CN";
        private const string CreditsResourcePath = "Announcements/hall.credits.zh-CN";

        private static readonly List<UpdateEntry> EmptyUpdates = new List<UpdateEntry>();
        private static bool loaded;
        private static List<UpdateEntry> updates = EmptyUpdates;
        private static string creditsText = string.Empty;

        [Serializable]
        internal sealed class UpdateEntry
        {
            public string date;
            public string body;
        }

        [Serializable]
        private sealed class UpdatesPayload
        {
            public List<UpdateEntry> updates = new List<UpdateEntry>();
        }

        [Serializable]
        private sealed class CreditsPayload
        {
            public List<string> names = new List<string>();
        }

        public static IReadOnlyList<UpdateEntry> Updates
        {
            get
            {
                EnsureLoaded();
                return updates;
            }
        }

        public static string CreditsText
        {
            get
            {
                EnsureLoaded();
                return creditsText;
            }
        }

        private static void EnsureLoaded()
        {
            if (loaded)
            {
                return;
            }

            loaded = true;
            updates = LoadUpdates();
            creditsText = LoadCreditsText();
        }

        private static List<UpdateEntry> LoadUpdates()
        {
            var asset = Resources.Load<TextAsset>(UpdatesResourcePath);
            if (asset == null || string.IsNullOrWhiteSpace(asset.text))
            {
                Debug.LogWarning("公告更新配置缺失: Resources/" + UpdatesResourcePath);
                return EmptyUpdates;
            }

            try
            {
                var payload = JsonUtility.FromJson<UpdatesPayload>(asset.text);
                if (payload == null || payload.updates == null)
                {
                    return EmptyUpdates;
                }

                return payload.updates;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("解析公告更新配置失败: " + exception.Message);
                return EmptyUpdates;
            }
        }

        private static string LoadCreditsText()
        {
            var asset = Resources.Load<TextAsset>(CreditsResourcePath);
            if (asset == null || string.IsNullOrWhiteSpace(asset.text))
            {
                Debug.LogWarning("共创名单配置缺失: Resources/" + CreditsResourcePath);
                return string.Empty;
            }

            try
            {
                var payload = JsonUtility.FromJson<CreditsPayload>(asset.text);
                if (payload == null || payload.names == null || payload.names.Count == 0)
                {
                    return string.Empty;
                }

                var builder = new StringBuilder();
                for (var i = 0; i < payload.names.Count; i++)
                {
                    var name = payload.names[i];
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    if (builder.Length > 0)
                    {
                        builder.Append("、");
                    }

                    builder.Append(name.Trim());
                }

                return builder.ToString();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("解析共创名单配置失败: " + exception.Message);
                return string.Empty;
            }
        }
    }
}
