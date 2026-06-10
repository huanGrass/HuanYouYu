using System;

namespace HuanYouYu.MiniGameHall
{
    [Serializable]
    public sealed class MiniGameDefinition
    {
        public string Id;
        public string Name;
        public string Description;
        public bool IsPlayable;
        public string StatusLabel;
        public string Category;
    }
}
