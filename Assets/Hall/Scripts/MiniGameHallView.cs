using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuanYouYu.MiniGameHall
{
    /// <summary>
    /// 大厅视图门面，向上层暴露 Show/Hide/Refresh，内部委托给 HallRenderer。
    /// </summary>
    public sealed class MiniGameHallView
    {
        private readonly HallRenderer renderer;

        /// <summary>
        /// 创建大厅视图并完成首屏渲染器绑定。
        /// </summary>
        public MiniGameHallView(
            Transform parent,
            Action<string> enterGameAction,
            Action<string> toggleFavoriteAction)
        {
            renderer = new HallRenderer(
                parent,
                enterGameAction,
                toggleFavoriteAction);
        }

        public bool IsVisible
        {
            get { return renderer.IsVisible; }
        }

        /// <summary>
        /// 当前是否停留在“全部游戏”页签。
        /// </summary>
        public bool IsAllGamesTabActive
        {
            get { return renderer.IsAllGamesTabActive; }
        }

        /// <summary>
        /// 显示大厅界面。
        /// </summary>
        public void Show()
        {
            renderer.Show();
        }

        /// <summary>
        /// 隐藏大厅界面。
        /// </summary>
        public void Hide()
        {
            renderer.Hide();
        }

        /// <summary>
        /// 使用最新卡片数据刷新当前页签内容。
        /// </summary>
        public void Refresh(IList<MiniGameCardViewModel> cards)
        {
            renderer.Refresh(cards);
        }

        /// <summary>
        /// 仅刷新当前可见卡片的收藏状态。
        /// </summary>
        public void RefreshFavoriteBadge(string gameId, bool isFavorite)
        {
            renderer.RefreshFavoriteBadge(gameId, isFavorite);
        }

        /// <summary>
        /// 同步收藏状态到缓存，并刷新当前可见卡片。
        /// </summary>
        public void RefreshFavoriteState(string gameId, bool isFavorite, int favoriteOrder)
        {
            renderer.RefreshFavoriteState(gameId, isFavorite, favoriteOrder);
        }
    }
}

