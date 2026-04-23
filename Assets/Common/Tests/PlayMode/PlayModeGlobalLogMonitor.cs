using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Tests
{
    internal static class PlayModeGlobalLogMonitor
    {
        private struct LogEntry
        {
            public LogType Type;
            public string Condition;
            public string StackTrace;
        }

        private static readonly object Sync = new object();
        private static readonly List<LogEntry> Entries = new List<LogEntry>();
        private static bool _installed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Install()
        {
            if (_installed)
            {
                Application.logMessageReceivedThreaded -= HandleLogMessage;
            }

            Application.logMessageReceivedThreaded += HandleLogMessage;
            _installed = true;
            Clear();
        }

        public static void Clear()
        {
            lock (Sync)
            {
                Entries.Clear();
            }
        }

        public static string BuildFailureReport(int maxEntries = 20)
        {
            List<LogEntry> snapshot;
            lock (Sync)
            {
                snapshot = new List<LogEntry>(Entries);
            }

            if (snapshot.Count == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            var shownCount = Mathf.Min(maxEntries, snapshot.Count);
            for (var i = 0; i < shownCount; i++)
            {
                var entry = snapshot[i];
                builder.Append('[').Append(entry.Type).Append("] ").AppendLine(entry.Condition);
                if (!string.IsNullOrWhiteSpace(entry.StackTrace))
                {
                    builder.AppendLine(entry.StackTrace);
                }

                if (i < shownCount - 1)
                {
                    builder.AppendLine();
                }
            }

            if (snapshot.Count > shownCount)
            {
                builder.AppendLine();
                builder.Append("... and ").Append(snapshot.Count - shownCount).Append(" more.");
            }

            return builder.ToString();
        }

        private static void HandleLogMessage(string condition, string stackTrace, LogType type)
        {
            if (!IsFailureType(type))
            {
                return;
            }

            if (ShouldIgnore(condition))
            {
                return;
            }

            lock (Sync)
            {
                Entries.Add(new LogEntry
                {
                    Type = type,
                    Condition = condition ?? string.Empty,
                    StackTrace = stackTrace ?? string.Empty
                });
            }
        }

        private static bool IsFailureType(LogType type)
        {
            return type == LogType.Error || type == LogType.Exception || type == LogType.Assert;
        }

        private static bool ShouldIgnore(string condition)
        {
            if (string.IsNullOrEmpty(condition))
            {
                return false;
            }

            return condition.Contains("[Licensing::Module] Error: Access token is unavailable; failed to update");
        }
    }
}
