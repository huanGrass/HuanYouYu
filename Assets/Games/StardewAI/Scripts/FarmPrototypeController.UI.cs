using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using HuanYouYu.MiniGameHall;

namespace FarmPrototype
{
    public sealed partial class FarmPrototypeController
    {
        private void UpdateTargetIndicator()
        {
            if (_targetIndicator == null)
            {
                return;
            }

            if (!TryGetPreviewTargetGrid(out Vector2Int targetGrid) || !TryGetTile(targetGrid, out _))
            {
                _targetIndicator.enabled = false;
                return;
            }

            _targetIndicator.enabled = true;
            _targetIndicator.transform.position = GridToWorld(targetGrid);
            _targetIndicator.color = IsTileInReach(targetGrid)
                ? new Color(1f, 1f, 1f, 0.32f)
                : new Color(1f, 0.65f, 0.4f, 0.28f);
        }

        private void UpdateHud()
        {
            if (_statusText == null || _messageText == null || _controlsText == null || _inventoryTitleText == null)
            {
                return;
            }

            bool portrait = Screen.height > Screen.width;
            Vector2Int playerGrid = WorldToGrid(_playerPosition);
            string targetLabel = GetCurrentTargetLabel();
            TimePeriod currentPeriod = GetCurrentTimePeriod();

            _statusText.text = portrait
                ? UiTextCatalog.Format(
                    "stardewai.ui.status.portrait",
                    _day,
                    FormatTime(_timeOfDayMinutes),
                    GetTimePeriodLabel(currentPeriod),
                    _gold,
                    GetToolLabel(_activeTool),
                    targetLabel)
                : UiTextCatalog.Format(
                    "stardewai.ui.status",
                    _day,
                    FormatTime(_timeOfDayMinutes),
                    GetTimePeriodLabel(currentPeriod),
                    _gold,
                    GetToolLabel(_activeTool),
                    targetLabel);

            _infoCardTitleText.text = GetInfoCardTitle();
            _messageText.text = GetInfoCardBody(targetLabel, currentPeriod);

            _controlsText.text = _isMerchantShopOpen
                ? UiTextCatalog.Get("stardewai.ui.controls.shop")
                : UiTextCatalog.Get("stardewai.ui.controls.explore");

            _inventoryTitleText.text = UiTextCatalog.Format(
                "stardewai.ui.inventory_title",
                GetOccupiedSlotCount(_backpackSlots),
                BackpackSlotCount,
                GetInventorySlotLabel(_selectedInventorySlotUiIndex));
        }

        private string GetInfoCardTitle()
        {
            switch (_activeInfoTab)
            {
                case InfoCardTab.Overview:
                    return UiTextCatalog.Get("stardewai.card.overview");
                case InfoCardTab.Event:
                    return UiTextCatalog.Get("stardewai.card.event");
                case InfoCardTab.Calendar:
                    return UiTextCatalog.Get("stardewai.card.calendar");
                case InfoCardTab.Backpack:
                    return UiTextCatalog.Get("stardewai.card.backpack");
                case InfoCardTab.Controls:
                    return UiTextCatalog.Get("stardewai.card.controls");
                default:
                    return UiTextCatalog.Get("stardewai.card.generic");
            }
        }

        private string GetInfoCardBody(string targetLabel, TimePeriod currentPeriod)
        {
            switch (_activeInfoTab)
            {
                case InfoCardTab.Event:
                    return GetEventCardBody(currentPeriod);
                case InfoCardTab.Calendar:
                    return GetCalendarCardBody();
                case InfoCardTab.Backpack:
                    return GetBackpackCardBody();
                case InfoCardTab.Controls:
                    return GetControlsCardBody();
                default:
                    return GetOverviewCardBody(targetLabel, currentPeriod);
            }
        }

        private string GetOverviewCardBody(string targetLabel, TimePeriod currentPeriod)
        {
            return UiTextCatalog.Format(
                "stardewai.card.overview.body",
                GetCurrentHudMessage(),
                targetLabel,
                GetTimePeriodLabel(currentPeriod),
                GetNpcScheduleSummary(currentPeriod));
        }

        private string GetEventCardBody(TimePeriod currentPeriod)
        {
            return UiTextCatalog.Format(
                "stardewai.card.event.body",
                GetDailyEventLabel(_dailyEvent),
                GetTimePeriodLabel(currentPeriod),
                GetDailyEventEffectSummary(),
                GetNpcScheduleSummary(currentPeriod));
        }

        private string GetCalendarCardBody()
        {
            StringBuilder builder = new StringBuilder(512);
            builder.Append(UiTextCatalog.Get("stardewai.card.calendar.view_season"));
            builder.Append(GetSeasonLabel(_calendarViewSeason));
            builder.Append(UiTextCatalog.Get("stardewai.card.calendar.current_date"));
            builder.Append(_calendarDate.ToDisplayLabel());
            builder.Append('\n');
            builder.Append(UiTextCatalog.Get("stardewai.card.calendar.next_season_hint"));
            builder.Append('\n');
            builder.Append('\n');

            for (int day = 1; day <= VillageCalendar.DaysPerSeason; day++)
            {
                VillageDate date = new VillageDate(_calendarDate.Year, _calendarViewSeason, day);
                bool hasFestival = _villageCalendar.TryGetFestival(date, out VillageFestival festival);
                string festivalLabel = hasFestival ? GetFestivalShortLabel(festival.DisplayName) : UiTextCatalog.Get("stardewai.common.none");
                if (day == _calendarDate.Day && _calendarViewSeason == _calendarDate.Season)
                {
                    builder.Append('>');
                }
                else
                {
                    builder.Append(' ');
                }

                builder.Append(day.ToString("00"));
                builder.Append(':');
                builder.Append(festivalLabel);

                if (day % 4 == 0)
                {
                    builder.Append('\n');
                }
                else
                {
                    builder.Append("   ");
                }
            }

            builder.Append('\n');
            builder.Append(UiTextCatalog.Get("stardewai.card.calendar.festival_full"));
            builder.Append('\n');
            bool hasSeasonFestival = false;
            for (int i = 0; i < _villageCalendar.Festivals.Count; i++)
            {
                VillageFestival festival = _villageCalendar.Festivals[i];
                if (festival.Season != _calendarViewSeason)
                {
                    continue;
                }

                hasSeasonFestival = true;
                builder.Append(festival.Day.ToString("00"));
                builder.Append(UiTextCatalog.Get("stardewai.card.calendar.festival_sep"));
                builder.Append(festival.DisplayName);
                builder.Append('\n');
            }

            if (!hasSeasonFestival)
            {
                builder.Append(UiTextCatalog.Get("stardewai.card.calendar.no_festival"));
            }

            return builder.ToString();
        }

        private string GetBackpackCardBody()
        {
            return UiTextCatalog.Format(
                "stardewai.card.backpack.body",
                GetSelectedInventoryDetail(),
                GetOccupiedSlotCount(_backpackSlots),
                BackpackSlotCount);
        }

        private static string GetControlsCardBody()
        {
            return UiTextCatalog.Get("stardewai.card.controls.body");
        }

        private string GetCurrentHudMessage()
        {
            return _messageAge < 7f
                ? _lastMessage
                : UiTextCatalog.Get("stardewai.ui.hud.fallback");
        }

        private string GetDailyEventEffectSummary()
        {
            switch (_dailyEvent)
            {
                case DailyEventType.NeighborVisit:
                    return _dailyGiftClaimed
                        ? UiTextCatalog.Get("stardewai.event.effect.neighbor_visit.claimed")
                        : UiTextCatalog.Get("stardewai.event.effect.neighbor_visit.available");
                case DailyEventType.DewMorning:
                    return UiTextCatalog.Get("stardewai.event.effect.dew_morning");
                case DailyEventType.SeedMarket:
                    return UiTextCatalog.Format("stardewai.event.effect.seed_market", GetCurrentSeedBundleSize());
                case DailyEventType.HarvestDay:
                    return UiTextCatalog.Get("stardewai.event.effect.harvest_day");
                case DailyEventType.VillageFestival:
                    return UiTextCatalog.Get("stardewai.event.effect.village_festival");
                default:
                    return UiTextCatalog.Get("stardewai.event.effect.none");
            }
        }

        private string GetNpcScheduleSummary(TimePeriod currentPeriod)
        {
            if (IsVillageFestivalDay())
            {
                switch (currentPeriod)
                {
                    case TimePeriod.Morning:
                        return UiTextCatalog.Get("stardewai.schedule.festival.morning");
                    case TimePeriod.Noon:
                        return UiTextCatalog.Get("stardewai.schedule.festival.noon");
                    case TimePeriod.Evening:
                        return UiTextCatalog.Get("stardewai.schedule.festival.evening");
                    case TimePeriod.Night:
                        return UiTextCatalog.Get("stardewai.schedule.festival.night");
                    default:
                        return UiTextCatalog.Get("stardewai.schedule.festival.default");
                }
            }

            switch (currentPeriod)
            {
                case TimePeriod.Morning:
                    return _dailyEvent == DailyEventType.SeedMarket
                        ? UiTextCatalog.Get("stardewai.schedule.morning.seed_market")
                        : UiTextCatalog.Get("stardewai.schedule.morning.default");
                case TimePeriod.Noon:
                    return _dailyEvent == DailyEventType.HarvestDay
                        ? UiTextCatalog.Get("stardewai.schedule.noon.harvest_day")
                        : UiTextCatalog.Get("stardewai.schedule.noon.default");
                case TimePeriod.Evening:
                    return UiTextCatalog.Get("stardewai.schedule.evening");
                case TimePeriod.Night:
                    return UiTextCatalog.Get("stardewai.schedule.night");
                default:
                    return UiTextCatalog.Get("stardewai.schedule.default");
            }
        }

        private void CreateToolButton(Transform parent, int index, string textKey, string fallbackLabel)
        {
            RectTransform buttonRect = new GameObject("ToolButton_" + index, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            buttonRect.SetParent(parent, false);
            buttonRect.anchorMin = new Vector2(0f, 1f);
            buttonRect.anchorMax = new Vector2(0f, 1f);
            buttonRect.pivot = new Vector2(0f, 1f);
            buttonRect.anchoredPosition = new Vector2(index * 108f, 0f);
            buttonRect.sizeDelta = new Vector2(98f, 46f);

            Image image = buttonRect.GetComponent<Image>();
            image.color = new Color(0.31f, 0.35f, 0.28f, 0.94f);

            TextMeshProUGUI text = CreateText(buttonRect, "Label", 16f, 12f, TextAlignmentOptions.Center);
            text.text = UiTextCatalog.GetOrFallback(textKey, fallbackLabel);
            text.color = new Color(0.94f, 0.95f, 0.9f);
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;

            _toolButtonRects[index] = buttonRect;
            _toolButtonImages[index] = image;
            _toolButtonTexts[index] = text;
        }

        private void CreateInfoTabButton(Transform parent, int index, string textKey, string fallbackLabel)
        {
            RectTransform buttonRect = new GameObject("InfoTabButton_" + index, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            buttonRect.SetParent(parent, false);
            buttonRect.anchorMin = new Vector2(0f, 1f);
            buttonRect.anchorMax = new Vector2(0f, 1f);
            buttonRect.pivot = new Vector2(0f, 1f);
            buttonRect.anchoredPosition = new Vector2(index * 64f, 0f);
            buttonRect.sizeDelta = new Vector2(58f, 30f);

            Image image = buttonRect.GetComponent<Image>();
            image.color = new Color(0.33f, 0.24f, 0.14f, 0.94f);

            TextMeshProUGUI text = CreateText(buttonRect, "Label", 15f, 11f, TextAlignmentOptions.Center);
            text.text = UiTextCatalog.GetOrFallback(textKey, fallbackLabel);
            text.color = new Color(0.93f, 0.9f, 0.82f);
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;

            _infoTabButtonRects[index] = buttonRect;
            _infoTabButtonImages[index] = image;
            _infoTabButtonTexts[index] = text;
        }

        private RectTransform CreateCloseButton(Transform parent, string objectName)
        {
            RectTransform buttonRect = new GameObject(objectName, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            buttonRect.SetParent(parent, false);
            buttonRect.anchorMin = new Vector2(1f, 1f);
            buttonRect.anchorMax = new Vector2(1f, 1f);
            buttonRect.pivot = new Vector2(1f, 1f);
            buttonRect.anchoredPosition = new Vector2(-10f, -10f);
            buttonRect.sizeDelta = new Vector2(30f, 30f);

            Image image = buttonRect.GetComponent<Image>();
            image.color = new Color(0.56f, 0.24f, 0.18f, 0.95f);

            TextMeshProUGUI text = CreateText(buttonRect, "Label", 20f, 14f, TextAlignmentOptions.Center);
            text.text = "×";
            text.color = new Color(0.98f, 0.95f, 0.91f);
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            return buttonRect;
        }

        private RectTransform CreateMerchantShopActionButton(Transform parent, string objectName, Vector2 anchoredPosition, Vector2 size, string textKey, string fallbackLabel)
        {
            RectTransform buttonRect = new GameObject(objectName, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            buttonRect.SetParent(parent, false);
            buttonRect.anchorMin = new Vector2(0f, 1f);
            buttonRect.anchorMax = new Vector2(0f, 1f);
            buttonRect.pivot = new Vector2(0f, 1f);
            buttonRect.anchoredPosition = anchoredPosition;
            buttonRect.sizeDelta = size;

            Image image = buttonRect.GetComponent<Image>();
            image.color = new Color(0.35f, 0.28f, 0.16f, 0.96f);

            TextMeshProUGUI text = CreateText(buttonRect, "Label", 16f, 12f, TextAlignmentOptions.Center);
            text.text = UiTextCatalog.GetOrFallback(textKey, fallbackLabel);
            text.color = new Color(0.95f, 0.92f, 0.86f);
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            return buttonRect;
        }

        private void CreateMerchantShopItemButton(Transform parent, int index)
        {
            RectTransform buttonRect = new GameObject("MerchantShopItem_" + index, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            buttonRect.SetParent(parent, false);
            buttonRect.anchorMin = new Vector2(0f, 1f);
            buttonRect.anchorMax = new Vector2(0f, 1f);
            buttonRect.pivot = new Vector2(0f, 1f);
            buttonRect.anchoredPosition = new Vector2(0f, -(index * 42f));
            buttonRect.sizeDelta = new Vector2(428f, 36f);

            Image image = buttonRect.GetComponent<Image>();
            image.color = (index % 2 == 0)
                ? new Color(0.2f, 0.16f, 0.1f, 0.82f)
                : new Color(0.24f, 0.18f, 0.12f, 0.82f);

            TextMeshProUGUI text = CreateText(buttonRect, "Label", 16f, 12f, TextAlignmentOptions.MidlineLeft);
            text.rectTransform.offsetMin = new Vector2(12f, 0f);
            text.rectTransform.offsetMax = new Vector2(-12f, 0f);
            text.color = new Color(0.95f, 0.93f, 0.89f);

            _merchantShopItemButtonRects[index] = buttonRect;
            _merchantShopItemButtonImages[index] = image;
            _merchantShopItemButtonTexts[index] = text;
        }

        private static Color GetInfoTabTint(InfoCardTab tab)
        {
            switch (tab)
            {
                case InfoCardTab.Overview:
                    return new Color(0.83f, 0.72f, 0.47f, 0.98f);
                case InfoCardTab.Event:
                    return new Color(0.94f, 0.62f, 0.34f, 0.98f);
                case InfoCardTab.Calendar:
                    return new Color(0.95f, 0.86f, 0.4f, 0.98f);
                case InfoCardTab.Backpack:
                    return new Color(0.61f, 0.78f, 0.49f, 0.98f);
                case InfoCardTab.Controls:
                    return new Color(0.64f, 0.75f, 0.88f, 0.98f);
                default:
                    return new Color(0.8f, 0.8f, 0.8f, 0.98f);
            }
        }

        private void CycleCalendarViewSeason()
        {
            int next = ((int)_calendarViewSeason + 1) % VillageCalendar.SeasonsPerYear;
            _calendarViewSeason = (VillageSeason)next;
        }

        private static string GetSeasonLabel(VillageSeason season)
        {
            switch (season)
            {
                case VillageSeason.Spring:
                    return UiTextCatalog.Get("stardewai.season.spring");
                case VillageSeason.Summer:
                    return UiTextCatalog.Get("stardewai.season.summer");
                case VillageSeason.Fall:
                    return UiTextCatalog.Get("stardewai.season.autumn");
                case VillageSeason.Winter:
                    return UiTextCatalog.Get("stardewai.season.winter");
                default:
                    return UiTextCatalog.Get("stardewai.season.unknown");
            }
        }

        private static string GetFestivalShortLabel(string festivalName)
        {
            if (string.IsNullOrEmpty(festivalName))
            {
                return UiTextCatalog.Get("stardewai.common.festival_short");
            }

            string normalized = festivalName.Trim();
            if (normalized.Length <= 2)
            {
                return normalized;
            }

            return normalized.Substring(0, 2);
        }

        private TextMeshProUGUI CreateSectionLabel(Transform parent, string objectName, string label, Vector2 anchoredPosition)
        {
            RectTransform rect = new GameObject(objectName, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(272f, 18f);

            TextMeshProUGUI text = CreateText(rect, "Label", 15f, 11f, TextAlignmentOptions.MidlineLeft);
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            text.text = label;
            text.color = new Color(0.34f, 0.22f, 0.08f);
            return text;
        }

        private void BuildInventoryDragGhost(Transform parent)
        {
            _dragGhostRect = new GameObject("InventoryDragGhost", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            _dragGhostRect.SetParent(parent, false);
            _dragGhostRect.anchorMin = new Vector2(0.5f, 0.5f);
            _dragGhostRect.anchorMax = new Vector2(0.5f, 0.5f);
            _dragGhostRect.pivot = new Vector2(0.5f, 0.5f);
            _dragGhostRect.sizeDelta = new Vector2(66f, 66f);
            _dragGhostRect.gameObject.SetActive(false);

            _dragGhostBackground = _dragGhostRect.GetComponent<Image>();
            _dragGhostBackground.color = new Color(0.1f, 0.12f, 0.12f, 0.94f);

            RectTransform iconRect = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            iconRect.SetParent(_dragGhostRect, false);
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(0f, 2f);
            iconRect.sizeDelta = new Vector2(28f, 28f);

            _dragGhostIcon = iconRect.GetComponent<Image>();
            _dragGhostIcon.preserveAspect = true;

            _dragGhostCountText = CreateText(_dragGhostRect, "Count", 13f, 10f, TextAlignmentOptions.BottomRight);
            _dragGhostCountText.rectTransform.offsetMin = new Vector2(6f, 6f);
            _dragGhostCountText.rectTransform.offsetMax = new Vector2(-6f, -6f);
            _dragGhostCountText.color = new Color(1f, 0.98f, 0.92f);
        }

        private void BuildInventoryTooltip(Transform parent)
        {
            _inventoryTooltipPanel = new GameObject("InventoryTooltip", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            _inventoryTooltipPanel.SetParent(parent, false);
            _inventoryTooltipPanel.anchorMin = new Vector2(0.5f, 0.5f);
            _inventoryTooltipPanel.anchorMax = new Vector2(0.5f, 0.5f);
            _inventoryTooltipPanel.pivot = new Vector2(0f, 1f);
            _inventoryTooltipPanel.sizeDelta = new Vector2(240f, 90f);
            _inventoryTooltipPanel.gameObject.SetActive(false);

            Image panelImage = _inventoryTooltipPanel.GetComponent<Image>();
            panelImage.color = new Color(0.97f, 0.91f, 0.75f, 0.98f);

            _inventoryTooltipTitleText = CreateText(_inventoryTooltipPanel, "Title", 16f, 12f, TextAlignmentOptions.TopLeft);
            _inventoryTooltipTitleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _inventoryTooltipTitleText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _inventoryTooltipTitleText.rectTransform.pivot = new Vector2(0f, 1f);
            _inventoryTooltipTitleText.rectTransform.anchoredPosition = new Vector2(10f, -8f);
            _inventoryTooltipTitleText.rectTransform.sizeDelta = new Vector2(-20f, 24f);
            _inventoryTooltipTitleText.color = new Color(0.2f, 0.12f, 0.04f);
            _inventoryTooltipTitleText.enableWordWrapping = false;
            _inventoryTooltipTitleText.overflowMode = TextOverflowModes.Ellipsis;

            _inventoryTooltipBodyText = CreateText(_inventoryTooltipPanel, "Body", 13f, 10f, TextAlignmentOptions.TopLeft);
            _inventoryTooltipBodyText.rectTransform.anchorMin = new Vector2(0f, 0f);
            _inventoryTooltipBodyText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _inventoryTooltipBodyText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _inventoryTooltipBodyText.rectTransform.offsetMin = new Vector2(10f, 8f);
            _inventoryTooltipBodyText.rectTransform.offsetMax = new Vector2(-10f, -34f);
            _inventoryTooltipBodyText.enableWordWrapping = true;
            _inventoryTooltipBodyText.color = new Color(0.28f, 0.18f, 0.08f);
        }

        private void UpdateInventoryTooltip()
        {
            if (_inventoryTooltipPanel == null)
            {
                return;
            }

            if (_inventoryPanel == null || !_inventoryPanel.gameObject.activeSelf || _isDraggingInventory || _isDraggingInventoryPanel)
            {
                _inventoryTooltipPanel.gameObject.SetActive(false);
                return;
            }

            if (_hoveredInventorySlotIndex < 0 || _hoveredInventorySlotIndex >= _inventorySlotUis.Length)
            {
                _inventoryTooltipPanel.gameObject.SetActive(false);
                return;
            }

            InventorySlotUi slotUi = _inventorySlotUis[_hoveredInventorySlotIndex];
            if (slotUi == null)
            {
                _inventoryTooltipPanel.gameObject.SetActive(false);
                return;
            }

            ItemStack stack = GetInventorySlotStack(slotUi);
            if (stack.IsEmpty)
            {
                _inventoryTooltipPanel.gameObject.SetActive(false);
                return;
            }

            if (_hudCanvasRect == null)
            {
                _inventoryTooltipPanel.gameObject.SetActive(false);
                return;
            }

            int maxStack = GetItemMaxStack(stack.ItemType);
            string title = GetItemLabel(stack.ItemType);
            string body;
            if (IsSeedItem(stack.ItemType))
            {
                body = UiTextCatalog.Format("stardewai.tooltip.seed", stack.Count, maxStack);
            }
            else
            {
                int sellPrice = GetItemSellPrice(stack.ItemType);
                body = UiTextCatalog.Format("stardewai.tooltip.shipping", stack.Count, maxStack, sellPrice);
            }

            _inventoryTooltipTitleText.text = title;
            _inventoryTooltipBodyText.text = body;

            const float width = 240f;
            const float minHeight = 92f;
            float textWidth = width - 20f;
            Vector2 bodySize = _inventoryTooltipBodyText.GetPreferredValues(body, textWidth, 0f);
            float height = Mathf.Clamp(bodySize.y + 50f, minHeight, 148f);
            _inventoryTooltipPanel.sizeDelta = new Vector2(width, height);

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_hudCanvasRect, Input.mousePosition, null, out Vector2 localPoint))
            {
                _inventoryTooltipPanel.gameObject.SetActive(false);
                return;
            }

            Vector2 anchored = localPoint + new Vector2(18f, -18f);
            Vector2 canvasHalf = _hudCanvasRect.rect.size * 0.5f;
            anchored.x = Mathf.Clamp(anchored.x, -canvasHalf.x, canvasHalf.x - width);
            anchored.y = Mathf.Clamp(anchored.y, -canvasHalf.y + height, canvasHalf.y);
            _inventoryTooltipPanel.anchoredPosition = anchored;
            _inventoryTooltipPanel.gameObject.SetActive(true);
        }

        private void CreateInventorySlot(
            Transform parent,
            int index,
            FarmInventoryContainerType containerType,
            int slotIndex,
            Vector2 anchoredPosition,
            Color accentColor,
            Sprite emptySprite)
        {
            RectTransform slotRect = new GameObject("InventorySlot_" + index, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            slotRect.SetParent(parent, false);
            slotRect.anchorMin = new Vector2(0f, 1f);
            slotRect.anchorMax = new Vector2(0f, 1f);
            slotRect.pivot = new Vector2(0f, 1f);
            slotRect.anchoredPosition = anchoredPosition;
            slotRect.sizeDelta = new Vector2(66f, 66f);

            Image slotBackground = slotRect.GetComponent<Image>();
            slotBackground.color = new Color(0.9f, 0.78f, 0.58f, 0.98f);

            RectTransform borderRect = new GameObject("Border", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            borderRect.SetParent(slotRect, false);
            borderRect.anchorMin = new Vector2(0.5f, 0.5f);
            borderRect.anchorMax = new Vector2(0.5f, 0.5f);
            borderRect.pivot = new Vector2(0.5f, 0.5f);
            borderRect.sizeDelta = new Vector2(62f, 62f);
            borderRect.GetComponent<Image>().color = new Color(0.45f, 0.3f, 0.14f, 0.95f);

            RectTransform innerRect = new GameObject("Inner", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            innerRect.SetParent(borderRect, false);
            innerRect.anchorMin = new Vector2(0.5f, 0.5f);
            innerRect.anchorMax = new Vector2(0.5f, 0.5f);
            innerRect.pivot = new Vector2(0.5f, 0.5f);
            innerRect.sizeDelta = new Vector2(56f, 56f);
            innerRect.GetComponent<Image>().color = new Color(0.96f, 0.88f, 0.72f, 0.98f);

            Image fillImage = new GameObject("Fill", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            RectTransform fillRect = fillImage.rectTransform;
            fillRect.SetParent(innerRect, false);
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(0f, 0f);
            fillRect.pivot = new Vector2(0f, 0f);
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = Vector2.zero;
            fillImage.color = new Color(0f, 0f, 0f, 0f);

            RectTransform accentRect = new GameObject("Accent", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            accentRect.SetParent(innerRect, false);
            accentRect.anchorMin = new Vector2(0f, 1f);
            accentRect.anchorMax = new Vector2(1f, 1f);
            accentRect.pivot = new Vector2(0.5f, 1f);
            accentRect.anchoredPosition = Vector2.zero;
            accentRect.sizeDelta = new Vector2(0f, 3f);
            accentRect.GetComponent<Image>().color = accentColor;

            TextMeshProUGUI nameText = CreateText(innerRect, "Name", 10f, 8f, TextAlignmentOptions.TopLeft);
            nameText.rectTransform.anchorMin = new Vector2(0f, 1f);
            nameText.rectTransform.anchorMax = new Vector2(1f, 1f);
            nameText.rectTransform.pivot = new Vector2(0.5f, 1f);
            nameText.rectTransform.anchoredPosition = new Vector2(0f, -6f);
            nameText.rectTransform.sizeDelta = new Vector2(-10f, 12f);

            RectTransform iconRect = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            iconRect.SetParent(innerRect, false);
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(0f, -2f);
            iconRect.sizeDelta = new Vector2(28f, 28f);

            Image iconImage = iconRect.GetComponent<Image>();
            iconImage.sprite = emptySprite;
            iconImage.preserveAspect = true;
            iconImage.color = new Color(1f, 1f, 1f, 0f);

            TextMeshProUGUI countText = CreateText(innerRect, "Count", 11f, 9f, TextAlignmentOptions.BottomRight);
            countText.rectTransform.offsetMin = new Vector2(6f, 4f);
            countText.rectTransform.offsetMax = new Vector2(-4f, -22f);

            _inventorySlotUis[index] = new InventorySlotUi
            {
                Rect = slotRect,
                Background = slotBackground,
                Fill = fillImage,
                Icon = iconImage,
                Name = nameText,
                Count = countText,
                ContainerType = containerType,
                SlotIndex = slotIndex,
                EmptyAccentColor = accentColor,
                EmptySprite = emptySprite
            };
        }

        private void UpdateInventorySlots()
        {
            for (int i = 0; i < _inventorySlotUis.Length; i++)
            {
                InventorySlotUi slotUi = _inventorySlotUis[i];
                if (slotUi == null)
                {
                    continue;
                }

                ItemStack stack = GetInventorySlotStack(slotUi);
                bool isSelected = i == _selectedInventorySlotUiIndex;
                bool isDragSource = i == _draggedInventorySlotIndex;
                bool isHovered = i == _hoveredInventorySlotIndex;
                int maxStack = stack.IsEmpty ? GetDefaultSlotCapacity(slotUi.ContainerType) : GetItemMaxStack(stack.ItemType);
                Color accent = stack.IsEmpty ? slotUi.EmptyAccentColor : GetItemAccentColor(stack.ItemType);
                Color baseColor = stack.IsEmpty
                    ? new Color(0.9f, 0.78f, 0.58f, 0.98f)
                    : Color.Lerp(new Color(0.9f, 0.78f, 0.58f, 0.98f), accent, 0.16f);

                if (isDragSource)
                {
                    slotUi.Background.color = Color.Lerp(baseColor, Color.black, 0.1f);
                    slotUi.Rect.localScale = Vector3.one * 0.96f;
                }
                else if (isSelected)
                {
                    slotUi.Background.color = new Color(0.97f, 0.82f, 0.36f, 1f);
                    slotUi.Rect.localScale = Vector3.one * 1.04f;
                }
                else if (isHovered)
                {
                    slotUi.Background.color = Color.Lerp(baseColor, new Color(0.98f, 0.9f, 0.7f, 1f), 0.35f);
                    slotUi.Rect.localScale = Vector3.one * 1.02f;
                }
                else
                {
                    slotUi.Background.color = baseColor;
                    slotUi.Rect.localScale = Vector3.one;
                }

                slotUi.Name.text = string.Empty;
                slotUi.Count.text = stack.IsEmpty
                    ? string.Empty
                    : stack.Count.ToString();
                slotUi.Name.color = isSelected ? new Color(0.21f, 0.12f, 0.03f) : new Color(0.34f, 0.22f, 0.1f);
                slotUi.Count.color = isSelected ? new Color(0.19f, 0.11f, 0.03f) : new Color(0.35f, 0.23f, 0.11f);
                slotUi.Icon.sprite = stack.IsEmpty ? null : GetItemSprite(stack.ItemType);
                slotUi.Icon.color = stack.IsEmpty
                    ? new Color(1f, 1f, 1f, 0f)
                    : (isDragSource ? new Color(1f, 1f, 1f, 0.26f) : Color.white);
                slotUi.Fill.rectTransform.sizeDelta = Vector2.zero;
                slotUi.Fill.color = new Color(0f, 0f, 0f, 0f);
            }
        }

        private void SetSelectedInventorySlot(int index)
        {
            if (index < 0 || index >= _inventorySlotUis.Length)
            {
                return;
            }

            _selectedInventorySlotUiIndex = index;
            SetMessage(GetInventorySlotSelectionMessage(index));
        }

        private static string GetToolLabel(ToolType tool)
        {
            switch (tool)
            {
                case ToolType.Hoe:
                    return UiTextCatalog.Get("stardewai.tool.hoe");
                case ToolType.WateringCan:
                    return UiTextCatalog.Get("stardewai.tool.watering");
                case ToolType.Seeds:
                    return UiTextCatalog.Get("stardewai.tool.seeds");
                case ToolType.Harvest:
                    return UiTextCatalog.Get("stardewai.tool.harvest");
                default:
                    return UiTextCatalog.Get("stardewai.tool.unknown");
            }
        }

        private ItemStack GetInventorySlotStack(InventorySlotUi slotUi)
        {
            ItemStack[] slots = GetContainerSlots(slotUi.ContainerType);
            return slots[slotUi.SlotIndex];
        }

        private string GetInventorySlotLabel(int index)
        {
            if (index < 0 || index >= _inventorySlotUis.Length || _inventorySlotUis[index] == null)
            {
                return UiTextCatalog.Get("stardewai.inventory.slot");
            }

            InventorySlotUi slotUi = _inventorySlotUis[index];
            ItemStack stack = GetInventorySlotStack(slotUi);
            string slotLabel = slotUi.ContainerType == FarmInventoryContainerType.ShippingBin
                ? UiTextCatalog.Format("stardewai.shipping.slot", slotUi.SlotIndex + 1)
                : UiTextCatalog.Format("stardewai.backpack.slot", slotUi.SlotIndex + 1);
            return stack.IsEmpty ? slotLabel : slotLabel + " · " + GetItemLabel(stack.ItemType);
        }

        private string GetSelectedInventoryDetail()
        {
            if (_selectedInventorySlotUiIndex < 0 || _selectedInventorySlotUiIndex >= _inventorySlotUis.Length)
            {
                return string.Empty;
            }

            InventorySlotUi slotUi = _inventorySlotUis[_selectedInventorySlotUiIndex];
            if (slotUi == null)
            {
                return string.Empty;
            }

            ItemStack stack = GetInventorySlotStack(slotUi);
            string slotLabel = slotUi.ContainerType == FarmInventoryContainerType.ShippingBin
                ? UiTextCatalog.Format("stardewai.shipping.slot", slotUi.SlotIndex + 1)
                : UiTextCatalog.Format("stardewai.backpack.slot", slotUi.SlotIndex + 1);
            if (stack.IsEmpty)
            {
                return UiTextCatalog.Format("stardewai.inventory.detail.empty", slotLabel, GetDefaultSlotCapacity(slotUi.ContainerType));
            }

            string detail = UiTextCatalog.Format(
                slotUi.ContainerType == FarmInventoryContainerType.ShippingBin
                    ? "stardewai.inventory.detail.shipping"
                    : IsSeedItem(stack.ItemType)
                        ? "stardewai.inventory.detail.seed"
                        : "stardewai.inventory.detail.backpack",
                slotLabel,
                GetItemLabel(stack.ItemType),
                stack.Count,
                GetItemMaxStack(stack.ItemType),
                stack.Count * GetItemSellPrice(stack.ItemType));
            if (slotUi.ContainerType == FarmInventoryContainerType.ShippingBin)
            {
                return detail;
            }
            return detail;
        }

        private string GetInventorySlotSelectionMessage(int index)
        {
            if (index < 0 || index >= _inventorySlotUis.Length || _inventorySlotUis[index] == null)
            {
                return UiTextCatalog.Get("stardewai.inventory.select.none");
            }

            InventorySlotUi slotUi = _inventorySlotUis[index];
            ItemStack stack = GetInventorySlotStack(slotUi);
            if (stack.IsEmpty)
            {
                return UiTextCatalog.Format(
                    "stardewai.inventory.select.empty",
                    slotUi.ContainerType == FarmInventoryContainerType.ShippingBin
                        ? UiTextCatalog.Format("stardewai.shipping.slot", slotUi.SlotIndex + 1)
                        : UiTextCatalog.Format("stardewai.backpack.slot", slotUi.SlotIndex + 1));
            }

            return UiTextCatalog.Format(
                "stardewai.inventory.select.filled",
                slotUi.ContainerType == FarmInventoryContainerType.ShippingBin
                    ? UiTextCatalog.Format("stardewai.shipping.slot", slotUi.SlotIndex + 1)
                    : UiTextCatalog.Format("stardewai.backpack.slot", slotUi.SlotIndex + 1),
                GetItemLabel(stack.ItemType),
                stack.Count,
                GetItemMaxStack(stack.ItemType));
        }

        private ItemStack[] GetContainerSlots(FarmInventoryContainerType containerType)
        {
            return containerType == FarmInventoryContainerType.ShippingBin ? _shippingSlots : _backpackSlots;
        }

        private static string GetContainerLabel(FarmInventoryContainerType containerType)
        {
            return containerType == FarmInventoryContainerType.ShippingBin
                ? UiTextCatalog.Get("stardewai.shipping.label")
                : UiTextCatalog.Get("stardewai.backpack.label");
        }

        private static bool CanStoreItemInContainer(ItemType itemType, FarmInventoryContainerType containerType)
        {
            if (itemType == ItemType.None)
            {
                return true;
            }

            if (containerType == FarmInventoryContainerType.Backpack)
            {
                return true;
            }

            return GetItemSellPrice(itemType) > 0;
        }

        private static int GetDefaultSlotCapacity(FarmInventoryContainerType containerType)
        {
            return containerType == FarmInventoryContainerType.ShippingBin ? CropMaxStack : Mathf.Max(SeedMaxStack, CropMaxStack);
        }

        private static int GetItemMaxStack(ItemType itemType)
        {
            if (IsSeedItem(itemType))
            {
                return SeedMaxStack;
            }

            if (IsCropItem(itemType))
            {
                return CropMaxStack;
            }

            return 0;
        }

        private static string GetItemLabel(ItemType itemType)
        {
            if (itemType == ItemType.None)
            {
                return UiTextCatalog.Get("stardewai.common.empty");
            }

            int index;
            if (TryGetCropIndexFromSeedItem(itemType, out index))
            {
                return UiTextCatalog.Format("stardewai.common.seed_suffix", CropDisplayNames[index]);
            }

            if (TryGetCropIndexFromCropItem(itemType, out index))
            {
                return CropDisplayNames[index];
            }

            return UiTextCatalog.Get("stardewai.common.unknown_item");
        }

        private Sprite GetItemSprite(ItemType itemType)
        {
            if (IsSeedItem(itemType))
            {
                return GetTileSprite(FarmTileArt.CropSeed);
            }

            if (IsCropItem(itemType))
            {
                return GetTileSprite(FarmTileArt.CropRipe);
            }

            return null;
        }

        private static Color GetItemAccentColor(ItemType itemType)
        {
            if (itemType == ItemType.None)
            {
                return Color.white;
            }

            int index;
            if (TryGetCropIndexFromSeedItem(itemType, out index))
            {
                return GetCropAccentColor(index, 0.58f, 0.78f);
            }

            if (TryGetCropIndexFromCropItem(itemType, out index))
            {
                return GetCropAccentColor(index, 0.72f, 0.92f);
            }

            return Color.white;
        }

        private static int GetItemSellPrice(ItemType itemType)
        {
            int index;
            if (TryGetCropIndexFromCropItem(itemType, out index))
            {
                return index < CropSellPrices.Length ? CropSellPrices[index] : CropSellPrice;
            }

            return 0;
        }

        private static int GetSeedShopPrice(ItemType seedItem)
        {
            int index;
            if (!TryGetCropIndexFromSeedItem(seedItem, out index))
            {
                return 0;
            }

            int sellPrice = index < CropSellPrices.Length ? CropSellPrices[index] : CropSellPrice;
            return Mathf.Max(12, Mathf.RoundToInt(sellPrice * 0.55f));
        }

        private static bool IsSeedItem(ItemType itemType)
        {
            int value = (int)itemType;
            return value >= FirstSeedItemType && value <= LastSeedItemType;
        }

        private static bool IsCropItem(ItemType itemType)
        {
            int value = (int)itemType;
            return value >= FirstCropItemType && value <= LastCropItemType;
        }

        private static ItemType GetCropFromSeedItem(ItemType seedItem)
        {
            int index;
            if (!TryGetCropIndexFromSeedItem(seedItem, out index))
            {
                return ItemType.Parsnip;
            }

            return (ItemType)(FirstCropItemType + index);
        }

        private static ItemType GetSeedFromCropItem(ItemType cropItem)
        {
            int index;
            if (!TryGetCropIndexFromCropItem(cropItem, out index))
            {
                return ItemType.ParsnipSeeds;
            }

            return (ItemType)(FirstSeedItemType + index);
        }

        private static int GetCropDaysToRipen(ItemType cropItem)
        {
            int index;
            if (TryGetCropIndexFromCropItem(cropItem, out index) && index < CropDaysToRipenByType.Length)
            {
                return CropDaysToRipenByType[index];
            }

            return CropDaysToRipen;
        }

        private ItemType GetPreferredSeedItemForPlanting()
        {
            for (int value = FirstSeedItemType; value <= LastSeedItemType; value++)
            {
                ItemType seedItem = (ItemType)value;
                if (GetItemCount(_backpackSlots, seedItem) > 0)
                {
                    return seedItem;
                }
            }

            return ItemType.None;
        }

        private ItemType[] GetSeedPurchaseLineup()
        {
            int lineupSize = Mathf.Min(4, CropTypeCount);
            ItemType[] lineup = new ItemType[lineupSize];
            int start = Mathf.Abs((_day - 1) * 3) % CropTypeCount;
            for (int i = 0; i < lineupSize; i++)
            {
                int cropIndex = (start + i) % CropTypeCount;
                lineup[i] = (ItemType)(FirstSeedItemType + cropIndex);
            }

            return lineup;
        }

        private static int[] BuildSeedBundleCounts(ItemType[] lineup, int bundleSize)
        {
            if (lineup == null || lineup.Length == 0)
            {
                return new int[0];
            }

            int[] counts = new int[lineup.Length];
            int safeBundleSize = Mathf.Max(0, bundleSize);
            int baseCount = safeBundleSize / lineup.Length;
            int remainder = safeBundleSize % lineup.Length;
            for (int i = 0; i < counts.Length; i++)
            {
                counts[i] = baseCount + (i < remainder ? 1 : 0);
            }

            return counts;
        }

        private bool CanStoreSeedBundle(ItemType[] lineup, int[] counts)
        {
            if (lineup == null || counts == null || lineup.Length != counts.Length)
            {
                return false;
            }

            for (int i = 0; i < lineup.Length; i++)
            {
                if (counts[i] <= 0)
                {
                    continue;
                }

                if (GetAvailableCapacity(_backpackSlots, lineup[i]) < counts[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static string FormatSeedBundleSummary(ItemType[] lineup, int[] counts)
        {
            if (lineup == null || counts == null || lineup.Length == 0 || lineup.Length != counts.Length)
            {
                return UiTextCatalog.Get("stardewai.common.none");
            }

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < lineup.Length; i++)
            {
                if (counts[i] <= 0)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append("、");
                }

                builder.Append(GetItemLabel(lineup[i]));
                builder.Append(" x");
                builder.Append(counts[i]);
            }

            return builder.Length > 0 ? builder.ToString() : UiTextCatalog.Get("stardewai.common.none");
        }

        private int GetBackpackCropCount()
        {
            int total = 0;
            for (int value = FirstCropItemType; value <= LastCropItemType; value++)
            {
                total += GetItemCount(_backpackSlots, (ItemType)value);
            }

            return total;
        }

        private int MoveAllCropsToShipping()
        {
            int moved = 0;
            for (int value = FirstCropItemType; value <= LastCropItemType; value++)
            {
                ItemType cropItem = (ItemType)value;
                int count = GetItemCount(_backpackSlots, cropItem);
                if (count > 0)
                {
                    moved += MoveItem(_backpackSlots, _shippingSlots, cropItem, count);
                }
            }

            return moved;
        }

        private static bool TryGetCropIndexFromSeedItem(ItemType seedItem, out int index)
        {
            index = (int)seedItem - FirstSeedItemType;
            return IsSeedItem(seedItem) && index >= 0 && index < CropTypeCount && index < CropDisplayNames.Length;
        }

        private static bool TryGetCropIndexFromCropItem(ItemType cropItem, out int index)
        {
            index = (int)cropItem - FirstCropItemType;
            return IsCropItem(cropItem) && index >= 0 && index < CropTypeCount && index < CropDisplayNames.Length;
        }

        private static Color GetCropAccentColor(int cropIndex, float saturation, float value)
        {
            float hue = CropTypeCount > 0 ? (cropIndex % CropTypeCount) / (float)CropTypeCount : 0f;
            Color color = Color.HSVToRGB(hue, saturation, value);
            color.a = 0.95f;
            return color;
        }

        private static int GetOccupiedSlotCount(ItemStack[] slots)
        {
            int count = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && !slots[i].IsEmpty)
                {
                    count++;
                }
            }

            return count;
        }

        private static int GetItemCount(ItemStack[] slots, ItemType itemType)
        {
            int count = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                ItemStack stack = slots[i];
                if (stack != null && stack.ItemType == itemType)
                {
                    count += stack.Count;
                }
            }

            return count;
        }

        private static int GetAvailableCapacity(ItemStack[] slots, ItemType itemType)
        {
            int capacity = 0;
            int maxStack = GetItemMaxStack(itemType);
            for (int i = 0; i < slots.Length; i++)
            {
                ItemStack stack = slots[i];
                if (stack == null)
                {
                    continue;
                }

                if (stack.IsEmpty)
                {
                    capacity += maxStack;
                }
                else if (stack.ItemType == itemType)
                {
                    capacity += maxStack - stack.Count;
                }
            }

            return capacity;
        }

        private static int AddItem(ItemStack[] slots, ItemType itemType, int count)
        {
            if (itemType == ItemType.None || count <= 0)
            {
                return 0;
            }

            int remaining = count;
            int maxStack = GetItemMaxStack(itemType);

            for (int i = 0; i < slots.Length && remaining > 0; i++)
            {
                ItemStack stack = slots[i];
                if (stack == null || stack.IsEmpty || stack.ItemType != itemType || stack.Count >= maxStack)
                {
                    continue;
                }

                int amount = Mathf.Min(maxStack - stack.Count, remaining);
                stack.Count += amount;
                remaining -= amount;
            }

            for (int i = 0; i < slots.Length && remaining > 0; i++)
            {
                ItemStack stack = slots[i];
                if (stack == null || !stack.IsEmpty)
                {
                    continue;
                }

                int amount = Mathf.Min(maxStack, remaining);
                stack.ItemType = itemType;
                stack.Count = amount;
                remaining -= amount;
            }

            return count - remaining;
        }

        private static int RemoveItem(ItemStack[] slots, ItemType itemType, int count)
        {
            if (itemType == ItemType.None || count <= 0)
            {
                return 0;
            }

            int remaining = count;
            for (int i = 0; i < slots.Length && remaining > 0; i++)
            {
                ItemStack stack = slots[i];
                if (stack == null || stack.ItemType != itemType || stack.Count <= 0)
                {
                    continue;
                }

                int amount = Mathf.Min(stack.Count, remaining);
                stack.Count -= amount;
                remaining -= amount;
                if (stack.Count <= 0)
                {
                    stack.Clear();
                }
            }

            return count - remaining;
        }

        private static int MoveItem(ItemStack[] source, ItemStack[] destination, ItemType itemType, int count)
        {
            int available = Mathf.Min(count, GetItemCount(source, itemType));
            if (available <= 0)
            {
                return 0;
            }

            int movable = Mathf.Min(available, GetAvailableCapacity(destination, itemType));
            if (movable <= 0)
            {
                return 0;
            }

            int removed = RemoveItem(source, itemType, movable);
            int added = AddItem(destination, itemType, removed);
            if (added < removed)
            {
                AddItem(source, itemType, removed - added);
            }

            return added;
        }

        private static void ClearContainer(ItemStack[] slots)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null)
                {
                    slots[i].Clear();
                }
            }
        }

        private static int GetContainerSellValue(ItemStack[] slots)
        {
            int total = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                ItemStack stack = slots[i];
                if (stack == null || stack.IsEmpty)
                {
                    continue;
                }

                total += stack.Count * GetItemSellPrice(stack.ItemType);
            }

            return total;
        }

        private static void SwapStacks(ItemStack first, ItemStack second)
        {
            ItemType tempType = first.ItemType;
            int tempCount = first.Count;
            first.ItemType = second.ItemType;
            first.Count = second.Count;
            second.ItemType = tempType;
            second.Count = tempCount;
        }

        private static Sprite GetTileSprite(FarmTileArt art)
        {
            Tile runtimeTile = FarmPixelArtFactory.GetTile(art) as Tile;
            return runtimeTile != null ? runtimeTile.sprite : null;
        }

        private static Color GetToolTint(ToolType tool)
        {
            switch (tool)
            {
                case ToolType.Hoe:
                    return new Color(0.91f, 0.73f, 0.46f, 0.98f);
                case ToolType.WateringCan:
                    return new Color(0.49f, 0.76f, 0.97f, 0.98f);
                case ToolType.Seeds:
                    return new Color(0.55f, 0.85f, 0.54f, 0.98f);
                case ToolType.Harvest:
                    return new Color(0.98f, 0.77f, 0.37f, 0.98f);
                default:
                    return Color.white;
            }
        }

        private Vector2Int DominantDirection(Vector2 delta)
        {
            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            {
                return delta.x >= 0f ? Vector2Int.right : Vector2Int.left;
            }

            if (Mathf.Abs(delta.y) > 0.001f)
            {
                return delta.y >= 0f ? Vector2Int.up : Vector2Int.down;
            }

            return _facing == Vector2Int.zero ? Vector2Int.up : _facing;
        }

        private bool TryGetTile(Vector2Int grid, out FarmTile tile)
        {
            if (grid.x >= 0 && grid.x < FieldWidth && grid.y >= 0 && grid.y < FieldHeight)
            {
                tile = _tiles[grid.x, grid.y];
                return true;
            }

            tile = null!;
            return false;
        }

        private static bool IsActionable(FarmTile tile)
        {
            return tile.IsTilled || tile.HasCrop || tile.IsWatered;
        }

        private static bool IsCropRipe(FarmTile tile)
        {
            return tile.HasCrop && tile.GrowthDays >= CropDaysToRipen;
        }

        private string DescribeTile(Vector2Int grid)
        {
            if (!TryGetTile(grid, out FarmTile tile))
            {
                return UiTextCatalog.Get("stardewai.world.off_farm");
            }

            if (!IsActionable(tile))
            {
                return UiTextCatalog.Get("stardewai.world.ridge");
            }

            if (tile.HasCrop)
            {
                if (IsCropRipe(tile))
                {
                    return UiTextCatalog.Format("stardewai.world.tile_ripe", GetItemLabel(tile.CropItemType));
                }

                return tile.IsWatered
                    ? UiTextCatalog.Get("stardewai.world.tile_growing_watered")
                    : UiTextCatalog.Get("stardewai.world.tile_growing");
            }

            return tile.IsWatered
                ? UiTextCatalog.Get("stardewai.world.tile_watered_empty")
                : UiTextCatalog.Get("stardewai.world.tile_tilled");
        }

        private Vector2 GridToWorld(Vector2Int grid)
        {
            return FieldOriginWorld + new Vector2(grid.x, grid.y);
        }

        private Vector2Int WorldToGrid(Vector2 worldPosition)
        {
            Vector2 local = worldPosition - FieldOriginWorld;
            return new Vector2Int(Mathf.RoundToInt(local.x), Mathf.RoundToInt(local.y));
        }

        private Vector2Int GetTargetGrid()
        {
            return WorldToGrid(_playerPosition) + _facing;
        }

        private bool TryGetPreviewTargetGrid(out Vector2Int grid)
        {
            if (_hasQueuedMouseAction && _queuedActionType == QueuedActionType.ToolOnTile)
            {
                grid = _queuedActionGrid;
                return true;
            }

            Vector2 frontPoint = _playerPosition + (Vector2)_facing;
            if (IsPointNearAnyNpc(frontPoint) ||
                IsPointNearRect(frontPoint, _shippingBinClickRect) ||
                IsPointNearRect(frontPoint, _seedChestClickRect))
            {
                grid = default;
                return false;
            }

            grid = GetTargetGrid();
            return true;
        }

        private string GetCurrentTargetLabel()
        {
            if (_hasQueuedMouseAction)
            {
                return _queuedActionType == QueuedActionType.ToolOnTile
                    ? DescribeTile(_queuedActionGrid)
                    : _queuedActionLabel;
            }

            if (IsPointNearRect(_playerPosition + (Vector2)_facing, _shippingBinClickRect))
            {
                return UiTextCatalog.Get("stardewai.shipping.label");
            }

            if (IsPointNearRect(_playerPosition + (Vector2)_facing, _seedChestClickRect))
            {
                return UiTextCatalog.Get("stardewai.seedChest.label");
            }

            if (TryGetNearbyNpc(_playerPosition + (Vector2)_facing, out NpcIdentity nearbyNpc))
            {
                return GetNpcDisplayName(nearbyNpc);
            }

            return DescribeTile(GetTargetGrid());
        }

        private void SetMessage(string message)
        {
            _lastMessage = message;
            _messageAge = 0f;
        }
    }
}
