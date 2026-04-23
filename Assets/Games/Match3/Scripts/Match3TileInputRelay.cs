using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HuanYouYu.MiniGameHall
{
    /// <summary>
    /// 将 UGUI Pointer 事件中转成可订阅回调，供 Match3GameView 统一处理输入。
    /// </summary>
    public sealed class Match3TileInputRelay : MonoBehaviour, IPointerDownHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        public event Action<PointerEventData> PointerDown;
        public event Action<PointerEventData> Drag;
        public event Action<PointerEventData> EndDrag;
        public event Action<PointerEventData> PointerClick;

        /// <summary>
        /// 转发按下事件。
        /// </summary>
        public void OnPointerDown(PointerEventData eventData)
        {
            PointerDown?.Invoke(eventData);
        }

        /// <summary>
        /// 转发拖拽事件。
        /// </summary>
        public void OnDrag(PointerEventData eventData)
        {
            Drag?.Invoke(eventData);
        }

        /// <summary>
        /// 转发结束拖拽事件。
        /// </summary>
        public void OnEndDrag(PointerEventData eventData)
        {
            EndDrag?.Invoke(eventData);
        }

        /// <summary>
        /// 转发点击事件。
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            PointerClick?.Invoke(eventData);
        }
    }
}

