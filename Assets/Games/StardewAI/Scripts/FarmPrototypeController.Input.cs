using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using HuanYouYu.MiniGameHall;

namespace FarmPrototype
{
    public sealed partial class FarmPrototypeController
    {
        private void HandleToolSelection()
        {
            if (_isDialogueOpen || _isMerchantShopOpen)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                SetActiveTool(ToolType.Hoe);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                SetActiveTool(ToolType.WateringCan);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                SetActiveTool(ToolType.Seeds);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha4))
            {
                SetActiveTool(ToolType.Harvest);
            }
        }

        private void HandleMouseInput()
        {
            if (!Input.GetMouseButtonDown(0))
            {
                return;
            }

            if (_isDialogueOpen)
            {
                AdvanceDialogue();
                return;
            }

            Vector2 screenPosition = Input.mousePosition;
            if (_isMerchantShopOpen)
            {
                TryHandleMerchantShopClick(screenPosition);
                return;
            }

            if (TryHandleToolButtonClick(screenPosition))
            {
                return;
            }

            if (TryHandleInfoTabButtonClick(screenPosition))
            {
                return;
            }

            if (TryHandlePopupCloseButtonClick(screenPosition))
            {
                return;
            }

            if (TryHandleAdvanceDayButtonClick(screenPosition))
            {
                return;
            }

            if (TryBeginInventoryPanelDrag(screenPosition))
            {
                return;
            }

            if (TryBeginInventoryInteraction(screenPosition))
            {
                return;
            }

            if (IsPointerOverUi())
            {
                return;
            }

            if (TryGetWorldInteractableFromScreen(screenPosition, out QueuedActionType actionType, out Vector2Int walkCell, out Vector2 worldTarget, out string label))
            {
                TriggerWorldClickFeedback(worldTarget, new Color(0.98f, 0.86f, 0.44f, 1f));
                FaceTowardsWorldTarget(worldTarget);

                if (IsWalkCellInReach(walkCell))
                {
                    CancelQueuedMouseAction();
                    ExecuteQueuedAction(actionType, _activeTool, default, label);
                    return;
                }

                QueueWorldAction(actionType, walkCell, label);
                return;
            }

            if (TryGetTileFromScreen(screenPosition, out Vector2Int clickedGrid))
            {
                TriggerTileClickFeedback(clickedGrid, new Color(0.99f, 0.93f, 0.62f, 1f));

                Vector2Int playerGrid = WorldToGrid(_playerPosition);
                Vector2Int delta = clickedGrid - playerGrid;
                if (delta != Vector2Int.zero)
                {
                    FaceTowardsWorldTarget(GridToWorld(clickedGrid));
                }

                if (IsTileInReach(clickedGrid))
                {
                    CancelQueuedMouseAction();
                    PlayFeedbackClip(_tileClickClip, 0.9f);
                    UseToolOnGrid(clickedGrid);
                    return;
                }

                QueueTileAction(clickedGrid);
                return;
            }

            if (TryGetWalkCellFromScreen(screenPosition, out Vector2Int clickedWalkCell, out Vector2 clickedWorld))
            {
                TriggerWorldClickFeedback(clickedWorld, new Color(0.99f, 0.93f, 0.62f, 1f));
                FaceTowardsWorldTarget(clickedWorld);
                QueueWorldAction(QueuedActionType.None, clickedWalkCell, UiTextCatalog.Get("stardewai.world.tile"));
            }
        }

        private static bool IsPointerOverUi()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            if (eventSystem.IsPointerOverGameObject())
            {
                return true;
            }

            for (int i = 0; i < Input.touchCount; i++)
            {
                if (eventSystem.IsPointerOverGameObject(Input.GetTouch(i).fingerId))
                {
                    return true;
                }
            }

            return false;
        }

        private void HandleInventoryDrag()
        {
            if (_isDialogueOpen || _isMerchantShopOpen)
            {
                CancelInventoryDrag();
                CancelInventoryPanelDrag();
                return;
            }

            if (_inventoryPanel == null || !_inventoryPanel.gameObject.activeSelf)
            {
                CancelInventoryDrag();
                CancelInventoryPanelDrag();
                return;
            }

            Vector2 screenPosition = Input.mousePosition;

            if (_isDraggingInventoryPanel)
            {
                UpdateInventoryPanelDrag(screenPosition);
                if (!Input.GetMouseButton(0) || Input.GetMouseButtonUp(0))
                {
                    CancelInventoryPanelDrag();
                }

                return;
            }

            if (_pressedInventorySlotIndex < 0)
            {
                UpdateInventoryDragGhost(screenPosition);
                return;
            }

            if (!_isDraggingInventory && Input.GetMouseButton(0))
            {
                float distance = Vector2.Distance(screenPosition, _inventoryPressScreenPosition);
                if (distance >= InventoryDragThreshold)
                {
                    BeginInventoryDrag(_pressedInventorySlotIndex);
                }
            }

            if (Input.GetMouseButtonUp(0))
            {
                CompleteInventoryDrag(screenPosition);
                return;
            }

            UpdateInventoryDragGhost(screenPosition);
        }

        private void UpdateHoveredTool()
        {
            Vector2 mousePosition = Input.mousePosition;
            _hoveredToolIndex = -1;

            for (int i = 0; i < _toolButtonRects.Length; i++)
            {
                RectTransform rect = _toolButtonRects[i];
                if (rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, mousePosition, null))
                {
                    _hoveredToolIndex = i;
                    return;
                }
            }
        }

        private void UpdateHoveredInfoTab()
        {
            Vector2 mousePosition = Input.mousePosition;
            _hoveredInfoTabIndex = -1;

            for (int i = 0; i < _infoTabButtonRects.Length; i++)
            {
                RectTransform rect = _infoTabButtonRects[i];
                if (rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, mousePosition, null))
                {
                    _hoveredInfoTabIndex = i;
                    return;
                }
            }
        }

        private void UpdateHoveredInventorySlot()
        {
            if (_inventoryPanel == null || !_inventoryPanel.gameObject.activeSelf)
            {
                _hoveredInventorySlotIndex = -1;
                return;
            }

            Vector2 mousePosition = Input.mousePosition;
            _hoveredInventorySlotIndex = -1;

            for (int i = 0; i < _inventorySlotUis.Length; i++)
            {
                InventorySlotUi slotUi = _inventorySlotUis[i];
                RectTransform rect = slotUi != null ? slotUi.Rect : null;
                if (rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, mousePosition, null))
                {
                    _hoveredInventorySlotIndex = i;
                    return;
                }
            }
        }

        private void UpdateToolButtons()
        {
            for (int i = 0; i < _toolButtonImages.Length; i++)
            {
                _toolButtonPressTimers[i] = Mathf.Max(0f, _toolButtonPressTimers[i] - Time.deltaTime);

                Image image = _toolButtonImages[i];
                TextMeshProUGUI text = _toolButtonTexts[i];
                RectTransform rect = _toolButtonRects[i];
                if (image == null || text == null)
                {
                    continue;
                }

                bool isActive = i == (int)_activeTool;
                bool isHovered = i == _hoveredToolIndex;
                float pressedT = 1f - Mathf.Clamp01(_toolButtonPressTimers[i] / ButtonPressFeedbackDuration);
                float pressedPulse = _toolButtonPressTimers[i] > 0f ? Mathf.Sin(pressedT * Mathf.PI) : 0f;
                Color baseColor;
                Color textColor;

                if (isActive)
                {
                    baseColor = GetToolTint((ToolType)i);
                    textColor = new Color(0.14f, 0.12f, 0.1f);
                }
                else if (isHovered)
                {
                    baseColor = new Color(0.82f, 0.74f, 0.5f, 0.98f);
                    textColor = new Color(0.22f, 0.18f, 0.13f);
                }
                else
                {
                    baseColor = new Color(0.31f, 0.35f, 0.28f, 0.94f);
                    textColor = new Color(0.94f, 0.95f, 0.9f);
                }

                image.color = Color.Lerp(baseColor, Color.white, 0.18f * pressedPulse);
                text.color = Color.Lerp(textColor, Color.white, 0.1f * pressedPulse);

                if (rect != null)
                {
                    rect.localScale = Vector3.one * (1f - (0.08f * pressedPulse));
                }
            }
        }

        private void UpdateInfoTabButtons()
        {
            for (int i = 0; i < _infoTabButtonImages.Length; i++)
            {
                Image image = _infoTabButtonImages[i];
                TextMeshProUGUI text = _infoTabButtonTexts[i];
                RectTransform rect = _infoTabButtonRects[i];
                if (image == null || text == null)
                {
                    continue;
                }

                InfoCardTab tab = (InfoCardTab)i;
                bool isActive = tab == InfoCardTab.Backpack
                    ? _inventoryPanel != null && _inventoryPanel.gameObject.activeSelf
                    : _infoCardPanel != null && _infoCardPanel.gameObject.activeSelf && _activeInfoTab == tab;
                bool isHovered = i == _hoveredInfoTabIndex;
                Color tint = GetInfoTabTint(tab);

                if (isActive)
                {
                    image.color = tint;
                    text.color = new Color(0.16f, 0.13f, 0.1f);
                }
                else if (isHovered)
                {
                    image.color = Color.Lerp(new Color(0.39f, 0.29f, 0.18f, 0.96f), tint, 0.42f);
                    text.color = new Color(0.98f, 0.96f, 0.9f);
                }
                else
                {
                    image.color = new Color(0.33f, 0.24f, 0.14f, 0.94f);
                    text.color = new Color(0.93f, 0.9f, 0.82f);
                }

                if (rect != null)
                {
                    rect.localScale = isActive
                        ? Vector3.one * 1.03f
                        : (isHovered ? Vector3.one * 1.01f : Vector3.one);
                }
            }
        }

        private void UpdateAdvanceDayButton()
        {
            _advanceDayButtonPressTimer = Mathf.Max(0f, _advanceDayButtonPressTimer - Time.deltaTime);

            if (_advanceDayButtonImage == null || _advanceDayButtonText == null)
            {
                return;
            }

            bool isHovered = _advanceDayButtonRect != null &&
                RectTransformUtility.RectangleContainsScreenPoint(_advanceDayButtonRect, Input.mousePosition, null);
            float pressedPulse = _advanceDayButtonPressTimer > 0f
                ? Mathf.Clamp01(_advanceDayButtonPressTimer / ButtonPressFeedbackDuration)
                : 0f;

            Color baseColor = isHovered
                ? new Color(0.78f, 0.65f, 0.36f, 0.98f)
                : new Color(0.62f, 0.5f, 0.28f, 0.96f);
            Color textColor = isHovered
                ? new Color(1f, 0.98f, 0.9f)
                : new Color(0.98f, 0.95f, 0.86f);

            _advanceDayButtonImage.color = Color.Lerp(baseColor, new Color(0.98f, 0.86f, 0.44f, 1f), 0.55f * pressedPulse);
            _advanceDayButtonText.color = Color.Lerp(textColor, Color.white, 0.2f * pressedPulse);

            if (_advanceDayButtonRect != null)
            {
                _advanceDayButtonRect.localScale = Vector3.one * (1f - (0.07f * pressedPulse));
            }
        }

        private bool TryHandleToolButtonClick(Vector2 screenPosition)
        {
            for (int i = 0; i < _toolButtonRects.Length; i++)
            {
                RectTransform rect = _toolButtonRects[i];
                if (rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, screenPosition, null))
                {
                    TriggerToolButtonFeedback(i);
                    PlayFeedbackClip(_uiClickClip, 1f);
                    SetActiveTool((ToolType)i);
                    return true;
                }
            }

            return false;
        }

        private bool TryHandleAdvanceDayButtonClick(Vector2 screenPosition)
        {
            if (_advanceDayButtonRect == null ||
                !RectTransformUtility.RectangleContainsScreenPoint(_advanceDayButtonRect, screenPosition, null))
            {
                return false;
            }

            TriggerAdvanceDayButtonFeedback();

            if (_isMerchantShopOpen)
            {
                PlayFeedbackClip(_blockedClip, 0.75f);
                SetMessage(UiTextCatalog.Get("stardewai.msg.close_shop_first"));
                return true;
            }

            CancelQueuedMouseAction();
            CancelInventoryDrag();
            CancelInventoryPanelDrag();
            PlayFeedbackClip(_uiClickClip, 0.95f);
            AdvanceDay();
            return true;
        }

        private void SetActiveInfoTab(InfoCardTab tab)
        {
            InfoCardTab previousTab = _activeInfoTab;
            _activeInfoTab = tab;
            if (tab == InfoCardTab.Calendar && previousTab != InfoCardTab.Calendar)
            {
                _calendarViewSeason = _calendarDate.Season;
            }

            bool showInventory = tab == InfoCardTab.Backpack;
            bool showInfoCard = !showInventory;

            if (_infoCardPanel != null)
            {
                _infoCardPanel.gameObject.SetActive(showInfoCard);
            }

            if (_inventoryPanel != null)
            {
                _inventoryPanel.gameObject.SetActive(showInventory);
                if (!showInventory)
                {
                    CancelInventoryPanelDrag();
                }
            }

            if (previousTab != tab)
            {
                _hoveredInventorySlotIndex = -1;
                CancelInventoryDrag();
            }
        }

        private bool TryHandleInfoTabButtonClick(Vector2 screenPosition)
        {
            for (int i = 0; i < _infoTabButtonRects.Length; i++)
            {
                RectTransform rect = _infoTabButtonRects[i];
                if (rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, screenPosition, null))
                {
                    InfoCardTab clickedTab = (InfoCardTab)i;
                    if (clickedTab == InfoCardTab.Calendar &&
                        _activeInfoTab == InfoCardTab.Calendar &&
                        _infoCardPanel != null &&
                        _infoCardPanel.gameObject.activeSelf)
                    {
                        CycleCalendarViewSeason();
                    }

                    SetActiveInfoTab(clickedTab);
                    PlayFeedbackClip(_uiClickClip, 0.9f);
                    return true;
                }
            }

            return false;
        }

        private bool TryHandlePopupCloseButtonClick(Vector2 screenPosition)
        {
            if (_infoCardPanel != null &&
                _infoCardPanel.gameObject.activeSelf &&
                _infoCloseButtonRect != null &&
                RectTransformUtility.RectangleContainsScreenPoint(_infoCloseButtonRect, screenPosition, null))
            {
                _infoCardPanel.gameObject.SetActive(false);
                PlayFeedbackClip(_uiClickClip, 0.9f);
                return true;
            }

            if (_inventoryPanel != null &&
                _inventoryPanel.gameObject.activeSelf &&
                _inventoryCloseButtonRect != null &&
                RectTransformUtility.RectangleContainsScreenPoint(_inventoryCloseButtonRect, screenPosition, null))
            {
                _inventoryPanel.gameObject.SetActive(false);
                _hoveredInventorySlotIndex = -1;
                CancelInventoryDrag();
                CancelInventoryPanelDrag();
                PlayFeedbackClip(_uiClickClip, 0.9f);
                return true;
            }

            return false;
        }

        private bool TryBeginInventoryInteraction(Vector2 screenPosition)
        {
            if (_inventoryPanel == null || !_inventoryPanel.gameObject.activeSelf)
            {
                return false;
            }

            if (!TryGetInventorySlotIndex(screenPosition, out int index))
            {
                return false;
            }

            _pressedInventorySlotIndex = index;
            _inventoryPressScreenPosition = screenPosition;
            SetSelectedInventorySlot(index);
            PlayFeedbackClip(_uiClickClip, 0.85f);
            return true;
        }

        private bool TryBeginInventoryPanelDrag(Vector2 screenPosition)
        {
            if (_inventoryPanel == null || !_inventoryPanel.gameObject.activeSelf || _hudCanvasRect == null)
            {
                return false;
            }

            if (!RectTransformUtility.RectangleContainsScreenPoint(_inventoryPanel, screenPosition, null))
            {
                return false;
            }

            if (_inventoryCloseButtonRect != null &&
                RectTransformUtility.RectangleContainsScreenPoint(_inventoryCloseButtonRect, screenPosition, null))
            {
                return false;
            }

            if (TryGetInventorySlotIndex(screenPosition, out _))
            {
                return false;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_inventoryPanel, screenPosition, null, out Vector2 panelLocal))
            {
                return false;
            }

            float titleThreshold = (_inventoryPanel.rect.height * 0.5f) - 52f;
            if (panelLocal.y < titleThreshold)
            {
                return false;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_hudCanvasRect, screenPosition, null, out Vector2 canvasLocal))
            {
                return false;
            }

            _inventoryPanelDragOffset = _inventoryPanel.anchoredPosition - canvasLocal;
            _isDraggingInventoryPanel = true;
            CancelInventoryDrag();
            PlayFeedbackClip(_uiClickClip, 0.8f);
            return true;
        }

        private void UpdateInventoryPanelDrag(Vector2 screenPosition)
        {
            if (_inventoryPanel == null || _hudCanvasRect == null)
            {
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_hudCanvasRect, screenPosition, null, out Vector2 canvasLocal))
            {
                return;
            }

            Vector2 target = canvasLocal + _inventoryPanelDragOffset;
            _inventoryPanel.anchoredPosition = ClampInventoryPanelPosition(target);
        }

        private Vector2 ClampInventoryPanelPosition(Vector2 anchoredPosition)
        {
            if (_inventoryPanel == null || _hudCanvasRect == null)
            {
                return anchoredPosition;
            }

            Rect canvasRect = _hudCanvasRect.rect;
            float halfPanelWidth = _inventoryPanel.rect.width * 0.5f;
            float halfPanelHeight = _inventoryPanel.rect.height * 0.5f;
            float minX = (canvasRect.xMin + halfPanelWidth) + 8f;
            float maxX = (canvasRect.xMax - halfPanelWidth) - 8f;
            float minY = (canvasRect.yMin + halfPanelHeight) + 8f;
            float maxY = (canvasRect.yMax - halfPanelHeight) - 8f;

            return new Vector2(
                Mathf.Clamp(anchoredPosition.x, minX, maxX),
                Mathf.Clamp(anchoredPosition.y, minY, maxY));
        }

        private bool TryHandleInventoryQuickShip(Vector2 screenPosition)
        {
            if (_inventoryPanel == null || !_inventoryPanel.gameObject.activeSelf)
            {
                return false;
            }

            if (!TryGetInventorySlotIndex(screenPosition, out int index))
            {
                return false;
            }

            SetSelectedInventorySlot(index);
            InventorySlotUi slotUi = _inventorySlotUis[index];
            ItemStack stack = GetInventorySlotStack(slotUi);
            if (stack.IsEmpty)
            {
                PlayFeedbackClip(_blockedClip, 0.7f);
                SetMessage(UiTextCatalog.Get("stardewai.msg.quickship_no_item"));
                return true;
            }

            if (slotUi.ContainerType != FarmInventoryContainerType.Backpack)
            {
                PlayFeedbackClip(_blockedClip, 0.7f);
                SetMessage(UiTextCatalog.Get("stardewai.msg.quickship_backpack_only"));
                return true;
            }

            if (!CanStoreItemInContainer(stack.ItemType, FarmInventoryContainerType.ShippingBin))
            {
                PlayFeedbackClip(_blockedClip, 0.7f);
                SetMessage(UiTextCatalog.Format("stardewai.msg.quickship_no_item_with_item", GetItemLabel(stack.ItemType)));
                return true;
            }

            ItemType movedItemType = stack.ItemType;
            int originalCount = stack.Count;
            int movedCount = AddItem(_shippingSlots, stack.ItemType, stack.Count);
            if (movedCount <= 0)
            {
                PlayFeedbackClip(_blockedClip, 0.8f);
                SetMessage(UiTextCatalog.Get("stardewai.msg.no_empty_slot"));
                return true;
            }

            stack.Count -= movedCount;
            if (stack.Count <= 0)
            {
                stack.Clear();
            }

            int remaining = GetBackpackCropCount();
            int shippedGold = GetContainerSellValue(_shippingSlots);
            PlayFeedbackClip(_uiClickClip, 0.9f);
            if (movedCount < originalCount)
            {
                SetMessage(UiTextCatalog.Format(
                    "stardewai.msg.shipped_some",
                    movedCount,
                    GetItemLabel(movedItemType),
                    remaining));
            }
            else
            {
                SetMessage(UiTextCatalog.Format(
                    "stardewai.msg.shipped_done",
                    movedCount,
                    GetItemLabel(movedItemType),
                    shippedGold));
            }

            return true;
        }

        private bool TryGetInventorySlotIndex(Vector2 screenPosition, out int index)
        {
            if (_inventoryPanel == null || !_inventoryPanel.gameObject.activeSelf)
            {
                index = -1;
                return false;
            }

            for (int i = 0; i < _inventorySlotUis.Length; i++)
            {
                InventorySlotUi slotUi = _inventorySlotUis[i];
                RectTransform rect = slotUi != null ? slotUi.Rect : null;
                if (rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, screenPosition, null))
                {
                    index = i;
                    return true;
                }
            }

            index = -1;
            return false;
        }

        private void BeginInventoryDrag(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _inventorySlotUis.Length)
            {
                return;
            }

            InventorySlotUi slotUi = _inventorySlotUis[slotIndex];
            if (slotUi == null || GetInventorySlotStack(slotUi).IsEmpty)
            {
                return;
            }

            _draggedInventorySlotIndex = slotIndex;
            _isDraggingInventory = true;
        }

        private void CompleteInventoryDrag(Vector2 screenPosition)
        {
            int sourceIndex = _pressedInventorySlotIndex;
            bool wasDragging = _isDraggingInventory;
            CancelInventoryDrag();

            if (!wasDragging || sourceIndex < 0)
            {
                return;
            }

            if (!TryGetInventorySlotIndex(screenPosition, out int targetIndex) || targetIndex == sourceIndex)
            {
                return;
            }

            if (TryMoveOrSwapInventorySlots(sourceIndex, targetIndex, out string message))
            {
                SetSelectedInventorySlot(targetIndex);
                PlayFeedbackClip(_uiClickClip, 0.95f);
            }
            else
            {
                PlayFeedbackClip(_blockedClip, 0.85f);
            }

            SetMessage(message);
        }

        private void CancelInventoryDrag()
        {
            _pressedInventorySlotIndex = -1;
            _draggedInventorySlotIndex = -1;
            _isDraggingInventory = false;

            if (_dragGhostRect != null)
            {
                _dragGhostRect.gameObject.SetActive(false);
            }
        }

        private void CancelInventoryPanelDrag()
        {
            _isDraggingInventoryPanel = false;
        }

        private void UpdateInventoryDragGhost(Vector2 screenPosition)
        {
            if (_dragGhostRect == null || _hudCanvasRect == null || !_isDraggingInventory || _draggedInventorySlotIndex < 0)
            {
                if (_dragGhostRect != null)
                {
                    _dragGhostRect.gameObject.SetActive(false);
                }

                return;
            }

            InventorySlotUi slotUi = _inventorySlotUis[_draggedInventorySlotIndex];
            if (slotUi == null)
            {
                _dragGhostRect.gameObject.SetActive(false);
                return;
            }

            ItemStack stack = GetInventorySlotStack(slotUi);
            if (stack.IsEmpty)
            {
                _dragGhostRect.gameObject.SetActive(false);
                return;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(_hudCanvasRect, screenPosition, null, out Vector2 localPoint);
            _dragGhostRect.gameObject.SetActive(true);
            _dragGhostRect.anchoredPosition = localPoint + new Vector2(28f, -28f);
            _dragGhostBackground.color = Color.Lerp(GetItemAccentColor(stack.ItemType), new Color(0.08f, 0.08f, 0.08f, 0.94f), 0.6f);
            _dragGhostIcon.sprite = GetItemSprite(stack.ItemType);
            _dragGhostIcon.color = Color.white;
            _dragGhostCountText.text = stack.Count.ToString();
        }

        private bool TryMoveOrSwapInventorySlots(int sourceIndex, int targetIndex, out string message)
        {
            message = UiTextCatalog.Get("stardewai.msg.no_item_to_move");
            if (sourceIndex < 0 || sourceIndex >= _inventorySlotUis.Length || targetIndex < 0 || targetIndex >= _inventorySlotUis.Length)
            {
                return false;
            }

            InventorySlotUi sourceUi = _inventorySlotUis[sourceIndex];
            InventorySlotUi targetUi = _inventorySlotUis[targetIndex];
            if (sourceUi == null || targetUi == null)
            {
                return false;
            }

            ItemStack sourceStack = GetInventorySlotStack(sourceUi);
            ItemStack targetStack = GetInventorySlotStack(targetUi);
            if (sourceStack.IsEmpty)
            {
                return false;
            }

            if (!targetStack.IsEmpty && sourceStack.ItemType == targetStack.ItemType)
            {
                if (!CanStoreItemInContainer(sourceStack.ItemType, targetUi.ContainerType))
                {
                    message = UiTextCatalog.Format(
                        "stardewai.msg.cannot_place_item",
                        GetContainerLabel(targetUi.ContainerType),
                        GetItemLabel(sourceStack.ItemType));
                    return false;
                }

                int maxStack = GetItemMaxStack(sourceStack.ItemType);
                int movable = Mathf.Min(maxStack - targetStack.Count, sourceStack.Count);
                if (movable <= 0)
                {
                message = UiTextCatalog.Get("stardewai.msg.slot_full");
                    return false;
                }

                targetStack.Count += movable;
                sourceStack.Count -= movable;
                if (sourceStack.Count <= 0)
                {
                    sourceStack.Clear();
                }

                message = UiTextCatalog.Format(
                    "stardewai.msg.stack_to_slot",
                    movable,
                    GetItemLabel(targetStack.ItemType),
                    GetContainerLabel(targetUi.ContainerType),
                    targetUi.SlotIndex + 1);
                return true;
            }

            if (!CanStoreItemInContainer(sourceStack.ItemType, targetUi.ContainerType))
            {
                message = UiTextCatalog.Format(
                    "stardewai.msg.cannot_place_item",
                    GetContainerLabel(targetUi.ContainerType),
                    GetItemLabel(sourceStack.ItemType));
                return false;
            }

            bool targetWasEmpty = targetStack.IsEmpty;
            if (!targetStack.IsEmpty && !CanStoreItemInContainer(targetStack.ItemType, sourceUi.ContainerType))
            {
                message = UiTextCatalog.Format(
                    "stardewai.msg.cannot_place_item",
                    GetContainerLabel(sourceUi.ContainerType),
                    GetItemLabel(targetStack.ItemType));
                return false;
            }

            SwapStacks(sourceStack, targetStack);
            if (targetWasEmpty)
            {
                message = UiTextCatalog.Format(
                    "stardewai.msg.move_to_slot",
                    GetItemLabel(targetStack.ItemType),
                    GetContainerLabel(targetUi.ContainerType),
                    targetUi.SlotIndex + 1);
            }
            else
            {
                message = UiTextCatalog.Format(
                    "stardewai.msg.swap_slots",
                    GetContainerLabel(sourceUi.ContainerType),
                    sourceUi.SlotIndex + 1,
                    GetContainerLabel(targetUi.ContainerType),
                    targetUi.SlotIndex + 1);
            }

            return true;
        }

        private bool TryGetTileFromScreen(Vector2 screenPosition, out Vector2Int grid)
        {
            grid = default;
            if (_mainCamera == null)
            {
                return false;
            }

            float planeDistance = -_mainCamera.transform.position.z;
            Vector3 world = _mainCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, planeDistance));
            Vector2Int candidate = WorldToGrid(new Vector2(world.x, world.y));
            if (!TryGetTile(candidate, out _))
            {
                return false;
            }

            grid = candidate;
            return true;
        }

        private bool IsTileInReach(Vector2Int grid)
        {
            Vector2Int playerGrid = WorldToGrid(_playerPosition);
            int distance = Mathf.Abs(grid.x - playerGrid.x) + Mathf.Abs(grid.y - playerGrid.y);
            return distance <= 1;
        }

        private bool TryGetWorldInteractableFromScreen(
            Vector2 screenPosition,
            out QueuedActionType actionType,
            out Vector2Int walkCell,
            out Vector2 worldTarget,
            out string label)
        {
            actionType = QueuedActionType.None;
            walkCell = default;
            worldTarget = default;
            label = string.Empty;

            if (_mainCamera == null)
            {
                return false;
            }

            float planeDistance = -_mainCamera.transform.position.z;
            Vector3 world = _mainCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, planeDistance));
            Vector2 point = new Vector2(world.x, world.y);

            if (TryGetNpcUnderPoint(point, out NpcIdentity clickedNpc))
            {
                actionType = QueuedActionType.TalkNpc;
                _queuedTalkNpc = clickedNpc;
                walkCell = GetNpcTalkWalkCell(clickedNpc);
                worldTarget = GetNpcWorldPosition(clickedNpc);
                label = GetNpcDisplayName(clickedNpc);
                return true;
            }

            if (_shippingBinClickRect.Contains(point))
            {
                actionType = QueuedActionType.ShipCrops;
                walkCell = _shippingBinWalkCell;
                worldTarget = _shippingBinPosition;
                label = UiTextCatalog.Get("stardewai.shipping.label");
                return true;
            }

            if (_seedChestClickRect.Contains(point))
            {
                actionType = QueuedActionType.BuySeeds;
                walkCell = _seedChestWalkCell;
                worldTarget = _seedChestPosition;
                label = UiTextCatalog.Get("stardewai.seedChest.label");
                return true;
            }

            return false;
        }

        private bool TryGetWalkCellFromScreen(Vector2 screenPosition, out Vector2Int walkCell, out Vector2 worldPoint)
        {
            walkCell = default;
            worldPoint = default;
            if (_mainCamera == null)
            {
                return false;
            }

            float planeDistance = -_mainCamera.transform.position.z;
            Vector3 world = _mainCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, planeDistance));
            worldPoint = new Vector2(world.x, world.y);
            walkCell = WorldToWalkCell(worldPoint);
            return IsWalkCellInBounds(walkCell);
        }

        private bool IsWalkCellInReach(Vector2Int walkCell)
        {
            Vector2Int playerCell = WorldToWalkCell(_playerPosition);
            int distance = Mathf.Abs(walkCell.x - playerCell.x) + Mathf.Abs(walkCell.y - playerCell.y);
            return distance <= 1;
        }

        private void FaceTowardsWorldTarget(Vector2 worldTarget)
        {
            Vector2 delta = worldTarget - _playerPosition;
            if (delta.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            _facing = DominantDirection(delta);
            UpdatePlayerVisual(Vector2.zero);
        }

        private void QueueWorldAction(QueuedActionType actionType, Vector2Int walkCell, string label)
        {
            QueueAction(actionType, walkCell, new Vector2Int(int.MinValue, int.MinValue), label);
        }

        private void QueueTileAction(Vector2Int targetGrid)
        {
            QueueAction(
                QueuedActionType.ToolOnTile,
                new Vector2Int(_fieldOriginCell.x + targetGrid.x, _fieldOriginCell.y + targetGrid.y),
                targetGrid,
                DescribeTile(targetGrid));
        }

        private void QueueAction(QueuedActionType actionType, Vector2Int walkCell, Vector2Int targetGrid, string label)
        {
            if (!TryPlanQueuedPath(walkCell))
            {
                CancelQueuedMouseAction();
                if (targetGrid.x != int.MinValue)
                {
                    TriggerTileClickFeedback(targetGrid, new Color(1f, 0.55f, 0.4f, 1f));
                }

                PlayFeedbackClip(_blockedClip, 1f);
                SetMessage(UiTextCatalog.Get("stardewai.msg.path_blocked"));
                return;
            }

            _queuedActionType = actionType;
            _queuedActionGrid = targetGrid;
            _queuedActionTool = _activeTool;
            _queuedActionLabel = label;
            _hasQueuedMouseAction = true;
            _queuedPathIndex = 0;
            RefreshQueuedMoveTarget();
            PlayFeedbackClip(_tileClickClip, 0.9f);

            if (actionType == QueuedActionType.ToolOnTile)
            {
                SetMessage(UiTextCatalog.Format(
                    "stardewai.msg.walking_to_use_tool",
                    GetToolLabel(_queuedActionTool)));
            }
            else
            {
                SetMessage(UiTextCatalog.Format("stardewai.msg.walking_to_interact", label));
            }
        }

        private void CompleteQueuedMouseAction()
        {
            if (!_hasQueuedMouseAction)
            {
                return;
            }

            QueuedActionType actionType = _queuedActionType;
            Vector2Int actionGrid = _queuedActionGrid;
            ToolType actionTool = _queuedActionTool;
            string actionLabel = _queuedActionLabel;
            _hasQueuedMouseAction = false;
            _queuedActionType = QueuedActionType.None;
            _queuedPathCells.Clear();
            _queuedPathIndex = 0;
            _queuedActionLabel = string.Empty;
            ExecuteQueuedAction(actionType, actionTool, actionGrid, actionLabel);
        }

        private void CancelQueuedMouseAction()
        {
            _hasQueuedMouseAction = false;
            _queuedActionType = QueuedActionType.None;
            _queuedPathCells.Clear();
            _queuedPathIndex = 0;
            _queuedActionLabel = string.Empty;
        }

        private void ExecuteQueuedAction(QueuedActionType actionType, ToolType actionTool, Vector2Int actionGrid, string actionLabel)
        {
            switch (actionType)
            {
                case QueuedActionType.ToolOnTile:
                    UseToolOnGrid(actionGrid, actionTool);
                    break;
                case QueuedActionType.ShipCrops:
                    ShipBackpackCrops();
                    break;
                case QueuedActionType.BuySeeds:
                    BuySeedBundle();
                    break;
                case QueuedActionType.TalkNpc:
                    BeginNpcDialogue(_queuedTalkNpc);
                    break;
                default:
                    if (!string.IsNullOrEmpty(actionLabel))
                    {
                        SetMessage(UiTextCatalog.Format("stardewai.msg.arrived", actionLabel));
                    }
                    break;
            }
        }

        private void ShipBackpackCrops()
        {
            int cropCount = GetBackpackCropCount();
            if (cropCount <= 0)
            {
                PlayFeedbackClip(_blockedClip, 0.9f);
                SetMessage(UiTextCatalog.Get("stardewai.msg.nothing_to_ship"));
                return;
            }

            int shippedCount = MoveAllCropsToShipping();
            if (shippedCount <= 0)
            {
                PlayFeedbackClip(_blockedClip, 0.9f);
                SetMessage(UiTextCatalog.Get("stardewai.msg.shipping_full"));
                return;
            }

            int remaining = GetBackpackCropCount();
            int shippedGold = GetContainerSellValue(_shippingSlots);
            PlayFeedbackClip(_uiClickClip, 0.9f);

            if (remaining > 0)
            {
                SetMessage(UiTextCatalog.Format(
                    "stardewai.msg.quickship_some",
                    shippedCount,
                    GetItemLabel(ItemType.Parsnip),
                    remaining));
            }
            else
            {
                SetMessage(UiTextCatalog.Format(
                    "stardewai.msg.quickship_done",
                    shippedCount,
                    GetItemLabel(ItemType.Parsnip),
                    shippedGold));
            }
        }

        private void BuySeedBundle()
        {
            int bundleSize = GetCurrentSeedBundleSize();
            if (_gold < SeedBundleCost)
            {
                PlayFeedbackClip(_blockedClip, 0.9f);
                SetMessage(UiTextCatalog.Get("stardewai.msg.insufficient_gold"));
                return;
            }

            ItemType[] lineup = GetSeedPurchaseLineup();
            int[] counts = BuildSeedBundleCounts(lineup, bundleSize);
            if (!CanStoreSeedBundle(lineup, counts))
            {
                PlayFeedbackClip(_blockedClip, 0.9f);
                SetMessage(UiTextCatalog.Get("stardewai.msg.no_backpack_space"));
                return;
            }

            _gold -= SeedBundleCost;
            for (int i = 0; i < lineup.Length; i++)
            {
                if (counts[i] > 0)
                {
                    AddItem(_backpackSlots, lineup[i], counts[i]);
                }
            }

            PlayFeedbackClip(_uiClickClip, 0.95f);
            SetMessage(UiTextCatalog.Format(
                "stardewai.msg.bought_seeds",
                SeedBundleCost,
                FormatSeedBundleSummary(lineup, counts)));
        }

        private void OpenMerchantShop()
        {
            CancelQueuedMouseAction();
            CancelInventoryDrag();
            CancelInventoryPanelDrag();
            if (IsVillageFestivalDay())
            {
                PlayFeedbackClip(_blockedClip, 0.85f);
                SetMessage(UiTextCatalog.Get("stardewai.msg.festival_no_shop"));
                return;
            }

            _isDialogueOpen = false;
            _dialogueState.Clear();
            _isMerchantShopOpen = true;
            _merchantShopPage = 0;

            if (_merchantShopPanel != null)
            {
                _merchantShopPanel.gameObject.SetActive(true);
            }

            RefreshMerchantShopPanel();
            SetMessage(UiTextCatalog.Get("stardewai.msg.shop_opened"));
        }

        private void CloseMerchantShop()
        {
            _isMerchantShopOpen = false;
            if (_merchantShopPanel != null)
            {
                _merchantShopPanel.gameObject.SetActive(false);
            }

            SetMessage(UiTextCatalog.Get("stardewai.msg.shop_closed"));
        }

        private bool TryHandleMerchantShopClick(Vector2 screenPosition)
        {
            if (!_isMerchantShopOpen || _merchantShopPanel == null || !_merchantShopPanel.gameObject.activeSelf)
            {
                return false;
            }

            if (_merchantShopCloseButtonRect != null &&
                RectTransformUtility.RectangleContainsScreenPoint(_merchantShopCloseButtonRect, screenPosition, null))
            {
                PlayFeedbackClip(_uiClickClip, 0.9f);
                CloseMerchantShop();
                return true;
            }

            if (_merchantShopPrevButtonRect != null &&
                RectTransformUtility.RectangleContainsScreenPoint(_merchantShopPrevButtonRect, screenPosition, null))
            {
                _merchantShopPage = Mathf.Max(0, _merchantShopPage - 1);
                PlayFeedbackClip(_uiClickClip, 0.85f);
                RefreshMerchantShopPanel();
                return true;
            }

            if (_merchantShopNextButtonRect != null &&
                RectTransformUtility.RectangleContainsScreenPoint(_merchantShopNextButtonRect, screenPosition, null))
            {
                int maxPage = Mathf.Max(0, ((CropTypeCount - 1) / MerchantShopPageSize));
                _merchantShopPage = Mathf.Min(maxPage, _merchantShopPage + 1);
                PlayFeedbackClip(_uiClickClip, 0.85f);
                RefreshMerchantShopPanel();
                return true;
            }

            int startIndex = _merchantShopPage * MerchantShopPageSize;
            for (int i = 0; i < MerchantShopPageSize; i++)
            {
                RectTransform rect = _merchantShopItemButtonRects[i];
                if (rect == null || !rect.gameObject.activeSelf)
                {
                    continue;
                }

                if (!RectTransformUtility.RectangleContainsScreenPoint(rect, screenPosition, null))
                {
                    continue;
                }

                int cropIndex = startIndex + i;
                if (cropIndex >= 0 && cropIndex < CropTypeCount)
                {
                    BuySeedFromMerchant((ItemType)(FirstSeedItemType + cropIndex));
                }

                return true;
            }

            return true;
        }

        private void BuySeedFromMerchant(ItemType seedItem)
        {
            int price = GetSeedShopPrice(seedItem);
            if (price <= 0)
            {
                PlayFeedbackClip(_blockedClip, 0.8f);
                SetMessage(UiTextCatalog.Get("stardewai.msg.not_for_sale"));
                return;
            }

            if (_gold < price)
            {
                PlayFeedbackClip(_blockedClip, 0.9f);
                SetMessage(UiTextCatalog.Format(
                    "stardewai.msg.insufficient_gold_for_item",
                    GetItemLabel(seedItem),
                    price - _gold));
                return;
            }

            if (GetAvailableCapacity(_backpackSlots, seedItem) <= 0)
            {
                PlayFeedbackClip(_blockedClip, 0.9f);
                SetMessage(UiTextCatalog.Format("stardewai.msg.no_backpack_space_for_item", GetItemLabel(seedItem)));
                return;
            }

            _gold -= price;
            AddItem(_backpackSlots, seedItem, 1);
            PlayFeedbackClip(_uiClickClip, 0.95f);
            SetMessage(UiTextCatalog.Format("stardewai.msg.bought_seed_item", GetItemLabel(seedItem), price));
            RefreshMerchantShopPanel();
        }

        private void RefreshMerchantShopPanel()
        {
            if (_merchantShopPanel == null || !_isMerchantShopOpen)
            {
                return;
            }

            int pageCount = Mathf.Max(1, (CropTypeCount + MerchantShopPageSize - 1) / MerchantShopPageSize);
            _merchantShopPage = Mathf.Clamp(_merchantShopPage, 0, pageCount - 1);

            if (_merchantShopTitleText != null)
            {
                _merchantShopTitleText.text = UiTextCatalog.Get("stardewai.shop.title");
            }

            if (_merchantShopPageText != null)
            {
                _merchantShopPageText.text = UiTextCatalog.Format("stardewai.shop.page", _merchantShopPage + 1, pageCount);
            }

            if (_merchantShopHintText != null)
            {
                _merchantShopHintText.text = UiTextCatalog.Get("stardewai.shop.hint");
            }

            int startIndex = _merchantShopPage * MerchantShopPageSize;
            for (int i = 0; i < MerchantShopPageSize; i++)
            {
                int cropIndex = startIndex + i;
                RectTransform rect = _merchantShopItemButtonRects[i];
                TextMeshProUGUI text = _merchantShopItemButtonTexts[i];
                Image image = _merchantShopItemButtonImages[i];
                if (rect == null || text == null || image == null)
                {
                    continue;
                }

                bool valid = cropIndex >= 0 && cropIndex < CropTypeCount;
                rect.gameObject.SetActive(valid);
                if (!valid)
                {
                    continue;
                }

                ItemType seedItem = (ItemType)(FirstSeedItemType + cropIndex);
                int seedCount = GetItemCount(_backpackSlots, seedItem);
                int price = GetSeedShopPrice(seedItem);
            text.text = UiTextCatalog.Format("stardewai.shop.item", GetItemLabel(seedItem), price, seedCount);
                image.color = i % 2 == 0
                    ? new Color(0.2f, 0.16f, 0.1f, 0.82f)
                    : new Color(0.24f, 0.18f, 0.12f, 0.82f);
            }
        }

        private void TriggerToolButtonFeedback(int index)
        {
            if (index >= 0 && index < _toolButtonPressTimers.Length)
            {
                _toolButtonPressTimers[index] = ButtonPressFeedbackDuration;
            }
        }

        private void TriggerAdvanceDayButtonFeedback()
        {
            _advanceDayButtonPressTimer = ButtonPressFeedbackDuration;
            UpdateAdvanceDayButton();
        }

        private void TriggerTileClickFeedback(Vector2Int grid, Color color)
        {
            TriggerWorldClickFeedback(GridToWorld(grid), color);
        }

        private void TriggerWorldClickFeedback(Vector2 worldPosition, Color color)
        {
            if (_clickFeedbackRenderer == null)
            {
                return;
            }

            _clickFeedbackTimer = TileClickFeedbackDuration;
            _clickFeedbackColor = color;
            _clickFeedbackRenderer.enabled = true;
            _clickFeedbackRenderer.transform.position = worldPosition;
            _clickFeedbackRenderer.transform.localScale = Vector3.one * 0.82f;
            _clickFeedbackRenderer.color = color;
        }

        private void UpdateClickFeedback()
        {
            if (_clickFeedbackRenderer == null)
            {
                return;
            }

            if (_clickFeedbackTimer <= 0f)
            {
                _clickFeedbackRenderer.enabled = false;
                return;
            }

            _clickFeedbackTimer = Mathf.Max(0f, _clickFeedbackTimer - Time.deltaTime);
            float t = 1f - Mathf.Clamp01(_clickFeedbackTimer / TileClickFeedbackDuration);
            float scale = Mathf.Lerp(0.82f, 1.34f, t);
            float alpha = Mathf.Lerp(0.92f, 0f, t);

            _clickFeedbackRenderer.enabled = true;
            _clickFeedbackRenderer.transform.localScale = Vector3.one * scale;

            Color color = _clickFeedbackColor;
            color.a *= alpha;
            _clickFeedbackRenderer.color = color;
        }

        private void PlayFeedbackClip(AudioClip clip, float volumeScale)
        {
            MiniGameSfxPlayer.Play(clip, volumeScale);
        }

    }
}
