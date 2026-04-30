using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using HuanYouYu.MiniGameHall;

namespace FarmPrototype
{
    public sealed partial class FarmPrototypeController : MonoBehaviour
    {
        private enum QueuedActionType
        {
            None,
            ToolOnTile,
            ShipCrops,
            BuySeeds,
            TalkNpc
        }

        private enum ToolType
        {
            Hoe,
            WateringCan,
            Seeds,
            Harvest
        }

        private enum ItemType
        {
            None,
            ParsnipSeeds,
            PotatoSeeds,
            CarrotSeeds,
            CabbageSeeds,
            TomatoSeeds,
            CucumberSeeds,
            PumpkinSeeds,
            CornSeeds,
            WheatSeeds,
            RiceSeeds,
            SoybeanSeeds,
            OnionSeeds,
            GarlicSeeds,
            PepperSeeds,
            EggplantSeeds,
            StrawberrySeeds,
            BlueberrySeeds,
            SpinachSeeds,
            LettuceSeeds,
            BroccoliSeeds,
            CauliflowerSeeds,
            RadishSeeds,
            TurnipSeeds,
            BeetSeeds,
            CelerySeeds,
            LeekSeeds,
            OkraSeeds,
            GreenBeanSeeds,
            PeaSeeds,
            ChickpeaSeeds,
            PeanutSeeds,
            RapeseedSeeds,
            SesameSeeds,
            SorghumSeeds,
            MilletSeeds,
            OatSeeds,
            BarleySeeds,
            RyeSeeds,
            SweetPotatoSeeds,
            YamSeeds,
            CassavaSeeds,
            TaroSeeds,
            GingerSeeds,
            TurmericSeeds,
            CottonSeeds,
            SunflowerSeeds,
            FlaxSeeds,
            BuckwheatSeeds,
            QuinoaSeeds,
            GrapeSeeds,
            WatermelonSeeds,
            MelonSeeds,
            ZucchiniSeeds,
            AsparagusSeeds,
            Parsnip,
            Potato,
            Carrot,
            Cabbage,
            Tomato,
            Cucumber,
            Pumpkin,
            Corn,
            Wheat,
            Rice,
            Soybean,
            Onion,
            Garlic,
            Pepper,
            Eggplant,
            Strawberry,
            Blueberry,
            Spinach,
            Lettuce,
            Broccoli,
            Cauliflower,
            Radish,
            Turnip,
            Beet,
            Celery,
            Leek,
            Okra,
            GreenBean,
            Pea,
            Chickpea,
            Peanut,
            Rapeseed,
            Sesame,
            Sorghum,
            Millet,
            Oat,
            Barley,
            Rye,
            SweetPotato,
            Yam,
            Cassava,
            Taro,
            Ginger,
            Turmeric,
            Cotton,
            Sunflower,
            Flax,
            Buckwheat,
            Quinoa,
            Grape,
            Watermelon,
            Melon,
            Zucchini,
            Asparagus
        }

        private enum TimePeriod
        {
            Morning,
            Noon,
            Evening,
            Night
        }

        private enum DailyEventType
        {
            NeighborVisit,
            DewMorning,
            SeedMarket,
            HarvestDay,
            VillageFestival
        }

        private enum NpcIdentity
        {
            Lumi,
            XiaoTuanzi,
            Qianran,
            HaiyinAwa,
            Azhai
        }

        private enum InfoCardTab
        {
            Overview,
            Event,
            Calendar,
            Backpack,
            Controls
        }

        private sealed class FarmTile
        {
            public Vector2Int Grid;
            public Vector3Int Cell;
            public bool IsTilled;
            public bool IsWatered;
            public bool HasCrop;
            public ItemType CropItemType;
            public int GrowthDays;
        }

        private sealed class ItemStack
        {
            public ItemType ItemType;
            public int Count;

            public bool IsEmpty => ItemType == ItemType.None || Count <= 0;

            public void Clear()
            {
                ItemType = ItemType.None;
                Count = 0;
            }
        }

        private sealed class InventorySlotUi
        {
            public RectTransform Rect = null!;
            public Image Background = null!;
            public Image Fill = null!;
            public Image Icon = null!;
            public TextMeshProUGUI Name = null!;
            public TextMeshProUGUI Count = null!;
            public FarmInventoryContainerType ContainerType;
            public int SlotIndex;
            public Color EmptyAccentColor;
            public Sprite EmptySprite = null!;
        }

        private sealed class DialogueState
        {
            public string Speaker = string.Empty;
            public readonly List<string> Lines = new List<string>();
            public int Index;

            public void Clear()
            {
                Speaker = string.Empty;
                Lines.Clear();
                Index = 0;
            }
        }

        private const int FieldWidth = 14;
        private const int FieldHeight = 10;
        private const int CropDaysToRipen = 3;
        private const float MoveSpeed = 4.25f;
        private const float AutoMoveArrivalDistance = 0.05f;
        private const float PlayerWalkAnimationRate = 8f;
        private const float PlayerIdleBobAmplitude = 0.02f;
        private const float PlayerWalkBobAmplitude = 0f;
        private const float PlayerSideWalkHipOffsetY = 0.38f;
        private const float PlayerSideWalkLegTilt = 14f;
        private const float PlayerSideWalkBodyTilt = 1.6f;
        private const float ToolActionDuration = 0.22f;
        private const float ToolHitEffectDuration = 0.26f;
        private const int ActorSortingBase = 120;
        private const float ButtonPressFeedbackDuration = 0.16f;
        private const float TileClickFeedbackDuration = 0.22f;
        private const float InventoryDragThreshold = 10f;
        private const float PlayerCollisionRadius = 0.24f;
        private const int StartingSeedInventory = 8;
        private const int SeedBundleSize = 4;
        private const int SeedBundleCost = 30;
        private const int CropSellPrice = 35;
        private const int BackpackSlotCount = 8;
        private const int ShippingSlotCount = 4;
        private const int TotalInventorySlotCount = BackpackSlotCount + ShippingSlotCount;
        private const int SeedMaxStack = 12;
        private const int CropMaxStack = 12;
        private const float DayStartMinutes = 6f * 60f;
        private const float DayEndMinutes = 22f * 60f;
        private const float GameMinutesPerSecond = 5f;
        private const float NpcMoveSpeed = 2.2f;
        private const float NpcInteractionDistance = 1.18f;
        private const int WalkMinX = -16;
        private const int WalkMaxX = 16;
        private const int WalkMinY = -10;
        private const int WalkMaxY = 12;
        private const int MerchantShopPageSize = 10;
        private const int FirstSeedItemType = (int)ItemType.ParsnipSeeds;
        private const int LastSeedItemType = (int)ItemType.AsparagusSeeds;
        private const int FirstCropItemType = (int)ItemType.Parsnip;
        private const int LastCropItemType = (int)ItemType.Asparagus;
        private const int CropTypeCount = LastCropItemType - FirstCropItemType + 1;
        private static string[] CropDisplayNames => UiTextCatalog.Get("stardewai.crop.names").Split(',');

        private static readonly int[] CropSellPrices =
        {
            35, 42, 38, 55, 60, 50, 85, 72, 46, 58, 62, 52, 64, 70, 68, 88, 92,
            44, 36, 57, 59, 41, 43, 47, 45, 48, 63, 54, 52, 58, 66, 61, 64, 56,
            49, 53, 55, 60, 69, 67, 71, 62, 74, 76, 80, 78, 65, 51, 68, 86, 90,
            83, 72, 84
        };

        private static readonly int[] CropDaysToRipenByType =
        {
            3, 4, 4, 5, 5, 4, 6, 5, 4, 5, 5, 4, 4, 5, 5, 6, 6,
            4, 4, 5, 5, 3, 4, 4, 4, 5, 5, 5, 4, 5, 5, 5, 5, 5,
            4, 5, 5, 5, 6, 6, 7, 6, 6, 7, 7, 6, 6, 5, 6, 7, 7,
            6, 5, 6
        };

        private readonly FarmTile[,] _tiles = new FarmTile[FieldWidth, FieldHeight];
        private readonly Vector3Int _fieldOriginCell = new Vector3Int(-7, -3, 0);
        private readonly RectTransform[] _toolButtonRects = new RectTransform[4];
        private readonly Image[] _toolButtonImages = new Image[4];
        private readonly TextMeshProUGUI[] _toolButtonTexts = new TextMeshProUGUI[4];
        private readonly RectTransform[] _infoTabButtonRects = new RectTransform[5];
        private readonly Image[] _infoTabButtonImages = new Image[5];
        private readonly TextMeshProUGUI[] _infoTabButtonTexts = new TextMeshProUGUI[5];
        private readonly InventorySlotUi[] _inventorySlotUis = new InventorySlotUi[TotalInventorySlotCount];
        private readonly ItemStack[] _backpackSlots = new ItemStack[BackpackSlotCount];
        private readonly ItemStack[] _shippingSlots = new ItemStack[ShippingSlotCount];
        private readonly float[] _toolButtonPressTimers = new float[4];
        private float _advanceDayButtonPressTimer;
        private readonly List<Rect> _blockedWorldRects = new List<Rect>();
        private readonly HashSet<Vector2Int> _blockedWalkCells = new HashSet<Vector2Int>();
        private readonly List<Vector2Int> _queuedPathCells = new List<Vector2Int>();
        private readonly Vector2Int[] _walkNeighbors =
        {
            Vector2Int.left,
            Vector2Int.right,
            Vector2Int.up,
            Vector2Int.down
        };

        private Camera _mainCamera = null!;
        private TMP_FontAsset _fontAsset = null!;
        private Grid _worldGrid = null!;
        private Tilemap _terrainTilemap = null!;
        private Tilemap _fieldTilemap = null!;
        private Tilemap _detailTilemap = null!;
        private Tilemap _cropTilemap = null!;
        private Transform _sceneryRoot = null!;
        private Transform _actorsRoot = null!;
        private SpriteRenderer _playerShadow = null!;
        private SpriteRenderer _playerRenderer = null!;
        private Transform _playerSideFrontLegRoot = null!;
        private Transform _playerSideBackLegRoot = null!;
        private SpriteRenderer _playerSideFrontLegRenderer = null!;
        private SpriteRenderer _playerSideBackLegRenderer = null!;
        private SpriteRenderer _playerToolRenderer = null!;
        private SpriteRenderer _npcShadow = null!;
        private SpriteRenderer _npcRenderer = null!;
        private SpriteRenderer _wandererNpcShadow = null!;
        private SpriteRenderer _wandererNpcRenderer = null!;
        private SpriteRenderer _merchantNpcShadow = null!;
        private SpriteRenderer _merchantNpcRenderer = null!;
        private SpriteRenderer _fisherNpcShadow = null!;
        private SpriteRenderer _fisherNpcRenderer = null!;
        private SpriteRenderer _troubleNpcShadow = null!;
        private SpriteRenderer _troubleNpcRenderer = null!;
        private SpriteRenderer _targetIndicator = null!;
        private SpriteRenderer _clickFeedbackRenderer = null!;
        private SpriteRenderer _toolHitEffectRenderer = null!;
        private AudioClip _uiClickClip = null!;
        private AudioClip _tileClickClip = null!;
        private AudioClip _blockedClip = null!;
        private AudioClip _hoeHitClip = null!;
        private AudioClip _waterHitClip = null!;
        private AudioClip _seedHitClip = null!;
        private AudioClip _harvestHitClip = null!;
        private RectTransform _hudCanvasRect = null!;
        private FarmHudView _hudView = null!;
        private RectTransform _infoCardPanel = null!;
        private RectTransform _inventoryPanel = null!;
        private RectTransform _merchantShopPanel = null!;
        private RectTransform _infoCloseButtonRect = null!;
        private RectTransform _inventoryCloseButtonRect = null!;
        private RectTransform _merchantShopCloseButtonRect = null!;
        private RectTransform _merchantShopPrevButtonRect = null!;
        private RectTransform _merchantShopNextButtonRect = null!;
        private RectTransform _advanceDayButtonRect = null!;
        private Image _advanceDayButtonImage = null!;
        private TextMeshProUGUI _advanceDayButtonText = null!;
        private readonly RectTransform[] _merchantShopItemButtonRects = new RectTransform[MerchantShopPageSize];
        private readonly Image[] _merchantShopItemButtonImages = new Image[MerchantShopPageSize];
        private readonly TextMeshProUGUI[] _merchantShopItemButtonTexts = new TextMeshProUGUI[MerchantShopPageSize];
        private TextMeshProUGUI _statusText = null!;
        private TextMeshProUGUI _infoCardTitleText = null!;
        private TextMeshProUGUI _messageText = null!;
        private TextMeshProUGUI _controlsText = null!;
        private TextMeshProUGUI _inventoryTitleText = null!;
        private TextMeshProUGUI _merchantShopTitleText = null!;
        private TextMeshProUGUI _merchantShopPageText = null!;
        private TextMeshProUGUI _merchantShopHintText = null!;
        private RectTransform _dialoguePanel = null!;
        private TextMeshProUGUI _dialogueSpeakerText = null!;
        private TextMeshProUGUI _dialogueBodyText = null!;
        private RectTransform _dragGhostRect = null!;
        private Image _dragGhostBackground = null!;
        private Image _dragGhostIcon = null!;
        private TextMeshProUGUI _dragGhostCountText = null!;
        private RectTransform _inventoryTooltipPanel = null!;
        private TextMeshProUGUI _inventoryTooltipTitleText = null!;
        private TextMeshProUGUI _inventoryTooltipBodyText = null!;

        private Vector2 _playerPosition = new Vector2(-3.5f, -3.45f);
        private Vector2 _npcPosition = new Vector2(-2.2f, -3.85f);
        private Vector2 _npcTargetPosition = new Vector2(-2.2f, -3.85f);
        private Vector2 _wandererNpcPosition = new Vector2(5.8f, -3.25f);
        private Vector2 _wandererNpcTargetPosition = new Vector2(5.8f, -3.25f);
        private Vector2 _merchantNpcPosition = new Vector2(-6.9f, -3.35f);
        private Vector2 _merchantNpcTargetPosition = new Vector2(-6.9f, -3.35f);
        private Vector2 _fisherNpcPosition = new Vector2(8.6f, -0.3f);
        private Vector2 _fisherNpcTargetPosition = new Vector2(8.6f, -0.3f);
        private Vector2 _troubleNpcPosition = new Vector2(1.3f, -3.6f);
        private Vector2 _troubleNpcTargetPosition = new Vector2(1.3f, -3.6f);
        private readonly Vector2 _shippingBinPosition = new Vector2(-6.9f, -4.2f);
        private readonly Vector2 _seedChestPosition = new Vector2(-8.3f, -4.2f);
        private readonly Rect _shippingBinClickRect = new Rect(-7.55f, -4.2f, 1.3f, 1.15f);
        private readonly Rect _seedChestClickRect = new Rect(-8.95f, -4.2f, 1.3f, 1.1f);
        private readonly Vector2Int _shippingBinWalkCell = new Vector2Int(-7, -5);
        private readonly Vector2Int _seedChestWalkCell = new Vector2Int(-9, -5);
        private readonly DialogueState _dialogueState = new DialogueState();
        private Vector2Int _facing = Vector2Int.up;
        private ToolType _activeTool;
        private ToolType _queuedActionTool;
        private QueuedActionType _queuedActionType;
        private Color _clickFeedbackColor = Color.white;
        private int _selectedInventorySlotUiIndex;
        private int _pressedInventorySlotIndex = -1;
        private int _draggedInventorySlotIndex = -1;
        private int _hoveredInfoTabIndex = -1;
        private int _hoveredToolIndex = -1;
        private int _hoveredInventorySlotIndex = -1;
        private int _queuedPathIndex;
        private int _day = 1;
        private readonly VillageCalendar _villageCalendar = VillageCalendar.CreateDefault();
        private VillageDate _calendarDate;
        private bool _hasFestivalToday;
        private VillageFestival _todayFestival = null!;
        private int _harvestedCrops;
        private int _sabotagedCrops;
        private int _gold = 80;
        private int _lastShipmentGold;
        private VillageSeason _calendarViewSeason;
        private float _timeOfDayMinutes = DayStartMinutes;
        private float _toolActionTimer;
        private float _toolHitEffectTimer;
        private float _cropSabotageTimer = 16f;
        private bool _hasQueuedMouseAction;
        private bool _isDraggingInventory;
        private bool _isDraggingInventoryPanel;
        private bool _isDialogueOpen;
        private bool _isMerchantShopOpen;
        private bool _dailyGiftClaimed;
        private float _clickFeedbackTimer;
        private Vector2 _lastPlayerMovementDelta;
        private Vector2 _inventoryPressScreenPosition;
        private Vector2 _inventoryPanelDragOffset;
        private Vector2 _queuedMoveTargetWorld;
        private Vector2 _toolHitEffectWorldPosition;
        private Vector2Int _queuedActionGrid;
        private Vector2Int _toolActionFacing = Vector2Int.down;
        private string _queuedActionLabel = string.Empty;
        private string _lastMessage = string.Empty;
        private TimePeriod _lastTimePeriod;
        private DailyEventType _dailyEvent;
        private InfoCardTab _activeInfoTab = InfoCardTab.Overview;
        private NpcIdentity _queuedTalkNpc = NpcIdentity.Lumi;
        private NpcIdentity _activeDialogueNpc = NpcIdentity.Lumi;
        private float _messageAge;
        private int _merchantShopPage;
        private ToolType _toolActionTool;
        private ToolType _toolHitEffectTool;
        private bool _isInitialized;
        private bool _isBootstrapped;
        private int _lastHudScreenWidth = -1;
        private int _lastHudScreenHeight = -1;
        private Rect _lastHudSafeArea = new Rect(-1f, -1f, -1f, -1f);
        private Transform _worldRoot = null!;

        public int Day => _day;

        public int TotalMoney => _gold;

        public int TileCount => FieldWidth * FieldHeight;

        public string ActiveToolLabel => GetToolLabel(_activeTool);

        public string TimeLabel => FormatTime(_timeOfDayMinutes);

        public string CalendarDateLabel => _calendarDate.ToDisplayLabel();

        public string DailyEventLabel => GetDailyEventLabel(_dailyEvent);

        public string NpcName => GetNpcDisplayName(NpcIdentity.Lumi);

        public bool HasNpc => _npcRenderer != null;

        private Vector2 FieldOriginWorld => new Vector2(_fieldOriginCell.x + 0.5f, _fieldOriginCell.y + 0.5f);

        internal void Initialize(FarmHudView hudView, Transform worldRoot, Transform overlayRoot)
        {
            if (hudView == null)
            {
                throw new System.ArgumentNullException(nameof(hudView));
            }

            if (worldRoot == null)
            {
                throw new System.ArgumentNullException(nameof(worldRoot));
            }

            if (overlayRoot == null)
            {
                throw new System.ArgumentNullException(nameof(overlayRoot));
            }

            _worldRoot = worldRoot;
            BindHud(hudView, overlayRoot);
            _isInitialized = true;
            TryInitialize();
        }

        private void Awake()
        {
            name = "FarmPrototypeController";
            _fontAsset = MiniGameFontProvider.DefaultFont;
            TryInitialize();
        }

        private void Update()
        {
            if (!_isBootstrapped)
            {
                return;
            }

            RefreshHudLayout();
            HandleToolSelection();
            HandleInventoryDrag();
            HandleMouseInput();

            Vector2 moveInput = (_isDialogueOpen || _isMerchantShopOpen) ? Vector2.zero : ReadMovement();
            UpdatePlayerMovement(moveInput);
            UpdateTimeOfDay();
            UpdateNpcMovement();
            UpdatePlayerVisual(_lastPlayerMovementDelta);
            UpdateToolActionAnimation();
            UpdateToolHitEffect();

            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (_isDialogueOpen)
                {
                    AdvanceDialogue();
                }
                else if (!_isMerchantShopOpen)
                {
                    UseActiveTool();
                }
            }

            if (_isMerchantShopOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                PlayFeedbackClip(_uiClickClip, 0.9f);
                CloseMerchantShop();
            }
            else if (!_isMerchantShopOpen && (Input.GetKeyDown(KeyCode.N) || Input.GetKeyDown(KeyCode.Return)))
            {
                CancelQueuedMouseAction();
                AdvanceDay();
            }
            else if (_isMerchantShopOpen && (Input.GetKeyDown(KeyCode.N) || Input.GetKeyDown(KeyCode.Return)))
            {
                SetMessage(UiTextCatalog.Get("stardewai.msg.close_shop_first"));
            }

            _messageAge += Time.deltaTime;
            UpdateClickFeedback();
            UpdateHoveredTool();
            UpdateHoveredInfoTab();
            UpdateHoveredInventorySlot();
            UpdateInventoryTooltip();
            UpdateTargetIndicator();
            UpdateToolButtons();
            UpdateAdvanceDayButton();
            UpdateInfoTabButtons();
            UpdateInventorySlots();
            UpdateDialoguePanel();
            UpdateHud();
        }

        private void LateUpdate()
        {
            if (!_isBootstrapped || _mainCamera == null)
            {
                return;
            }

            Vector3 target = GetClampedCameraPosition();
            _mainCamera.transform.position = Vector3.Lerp(_mainCamera.transform.position, target, 8f * Time.deltaTime);
        }

        private void TryInitialize()
        {
            if (_isBootstrapped || !_isInitialized || _worldRoot == null || _hudCanvasRect == null)
            {
                return;
            }

            _isBootstrapped = true;
            InitializeInventory();
            InitializeDayState();
            BuildWorld();
            ApplyDailyEventAtDayStart();
            BuildFeedbackSystem();
            ConfigureCamera();
            UpdateAllTileVisuals();
            UpdatePlayerVisual(Vector2.zero);
            UpdateNpcVisual();
            UpdateClickFeedback();
            UpdateTargetIndicator();
            UpdateToolButtons();
            UpdateAdvanceDayButton();
            UpdateInventorySlots();
            UpdateDialoguePanel();
            SetMessage(UiTextCatalog.Format(
                "stardewai.msg.day_start",
                1,
                GetNpcDisplayName(NpcIdentity.Lumi),
                GetNpcDisplayName(NpcIdentity.XiaoTuanzi),
                GetNpcDisplayName(NpcIdentity.Qianran),
                GetNpcDisplayName(NpcIdentity.HaiyinAwa),
                GetNpcDisplayName(NpcIdentity.Azhai)));
        }

        private void BindHud(FarmHudView hudView, Transform overlayRoot)
        {
            _hudView = hudView;
            _hudCanvasRect = overlayRoot as RectTransform;
            _infoCardPanel = hudView.InfoCardPanel;
            _inventoryPanel = hudView.InventoryPanel;
            _merchantShopPanel = hudView.MerchantShopPanel;
            _infoCloseButtonRect = hudView.InfoCloseButtonRect;
            _inventoryCloseButtonRect = hudView.InventoryCloseButtonRect;
            _merchantShopCloseButtonRect = hudView.MerchantShopCloseButtonRect;
            _merchantShopPrevButtonRect = hudView.MerchantShopPrevButtonRect;
            _merchantShopNextButtonRect = hudView.MerchantShopNextButtonRect;
            _advanceDayButtonRect = hudView.AdvanceDayButtonRect;
            _advanceDayButtonImage = _advanceDayButtonRect != null ? _advanceDayButtonRect.GetComponent<Image>() : null;
            _advanceDayButtonText = _advanceDayButtonRect != null ? _advanceDayButtonRect.Find("Label")?.GetComponent<TextMeshProUGUI>() : null;
            _statusText = hudView.StatusText;
            _infoCardTitleText = hudView.InfoCardTitleText;
            _messageText = hudView.MessageText;
            _controlsText = hudView.ControlsText;
            _inventoryTitleText = hudView.InventoryTitleText;
            _merchantShopTitleText = hudView.MerchantShopTitleText;
            _merchantShopPageText = hudView.MerchantShopPageText;
            _merchantShopHintText = hudView.MerchantShopHintText;
            _dialoguePanel = hudView.DialoguePanel;
            _dialogueSpeakerText = hudView.DialogueSpeakerText;
            _dialogueBodyText = hudView.DialogueBodyText;
            _dragGhostRect = hudView.DragGhostRect;
            _dragGhostBackground = hudView.DragGhostBackground;
            _dragGhostIcon = hudView.DragGhostIcon;
            _dragGhostCountText = hudView.DragGhostCountText;
            _inventoryTooltipPanel = hudView.InventoryTooltipPanel;
            _inventoryTooltipTitleText = hudView.InventoryTooltipTitleText;
            _inventoryTooltipBodyText = hudView.InventoryTooltipBodyText;

            for (int i = 0; i < _toolButtonRects.Length; i++)
            {
                _toolButtonRects[i] = hudView.ToolButtonRects[i];
                _toolButtonImages[i] = hudView.ToolButtonImages[i];
                _toolButtonTexts[i] = hudView.ToolButtonTexts[i];
            }

            for (int i = 0; i < _infoTabButtonRects.Length; i++)
            {
                _infoTabButtonRects[i] = hudView.InfoTabButtonRects[i];
                _infoTabButtonImages[i] = hudView.InfoTabButtonImages[i];
                _infoTabButtonTexts[i] = hudView.InfoTabButtonTexts[i];
            }

            for (int i = 0; i < _merchantShopItemButtonRects.Length; i++)
            {
                _merchantShopItemButtonRects[i] = hudView.MerchantShopItemButtonRects[i];
                _merchantShopItemButtonImages[i] = hudView.MerchantShopItemButtonImages[i];
                _merchantShopItemButtonTexts[i] = hudView.MerchantShopItemButtonTexts[i];
            }

            for (int i = 0; i < _inventorySlotUis.Length; i++)
            {
                _inventorySlotUis[i] = null;
                if (i >= hudView.InventorySlotBindings.Length)
                {
                    continue;
                }

                FarmInventorySlotBinding binding = hudView.InventorySlotBindings[i];
                if (binding == null)
                {
                    continue;
                }

                _inventorySlotUis[i] = new InventorySlotUi
                {
                    Rect = binding.Rect,
                    Background = binding.Background,
                    Fill = binding.Fill,
                    Icon = binding.Icon,
                    Name = binding.Name,
                    Count = binding.Count,
                    ContainerType = binding.ContainerType,
                    SlotIndex = binding.SlotIndex,
                    EmptyAccentColor = binding.EmptyAccentColor,
                    EmptySprite = binding.EmptySprite
                };
            }

            RefreshHudLayout();
        }

        private void RefreshHudLayout()
        {
            if (_hudView == null)
            {
                return;
            }

            int screenWidth = Screen.width;
            int screenHeight = Screen.height;
            Rect safeArea = Screen.safeArea;

            if (screenWidth <= 0 || screenHeight <= 0)
            {
                return;
            }

            if (_lastHudScreenWidth == screenWidth &&
                _lastHudScreenHeight == screenHeight &&
                AreRectsApproximatelyEqual(_lastHudSafeArea, safeArea))
            {
                return;
            }

            _lastHudScreenWidth = screenWidth;
            _lastHudScreenHeight = screenHeight;
            _lastHudSafeArea = safeArea;
            _hudView.ApplyLayout(safeArea, new Vector2Int(screenWidth, screenHeight));
        }

        private static bool AreRectsApproximatelyEqual(Rect a, Rect b)
        {
            return Mathf.Abs(a.xMin - b.xMin) < 0.5f &&
                Mathf.Abs(a.yMin - b.yMin) < 0.5f &&
                Mathf.Abs(a.width - b.width) < 0.5f &&
                Mathf.Abs(a.height - b.height) < 0.5f;
        }

        private void InitializeInventory()
        {
            for (int i = 0; i < _backpackSlots.Length; i++)
            {
                _backpackSlots[i] = new ItemStack();
            }

            for (int i = 0; i < _shippingSlots.Length; i++)
            {
                _shippingSlots[i] = new ItemStack();
            }

            ItemType[] starterLineup = GetSeedPurchaseLineup();
            int[] starterCounts = BuildSeedBundleCounts(starterLineup, StartingSeedInventory);
            for (int i = 0; i < starterLineup.Length; i++)
            {
                if (starterCounts[i] > 0)
                {
                    AddItem(_backpackSlots, starterLineup[i], starterCounts[i]);
                }
            }
            _selectedInventorySlotUiIndex = 0;
        }

        private void InitializeDayState()
        {
            RefreshCalendarState();
            _calendarViewSeason = _calendarDate.Season;
            _dailyEvent = GetDailyEventForDay(_day);
            _timeOfDayMinutes = DayStartMinutes;
            _dailyGiftClaimed = false;
            _lastTimePeriod = GetCurrentTimePeriod();
            _npcPosition = GetNpcScheduledPosition(_lastTimePeriod, NpcIdentity.Lumi);
            _npcTargetPosition = _npcPosition;
            _wandererNpcPosition = GetNpcScheduledPosition(_lastTimePeriod, NpcIdentity.XiaoTuanzi);
            _wandererNpcTargetPosition = _wandererNpcPosition;
            _merchantNpcPosition = GetNpcScheduledPosition(_lastTimePeriod, NpcIdentity.Qianran);
            _merchantNpcTargetPosition = _merchantNpcPosition;
            _fisherNpcPosition = GetNpcScheduledPosition(_lastTimePeriod, NpcIdentity.HaiyinAwa);
            _fisherNpcTargetPosition = _fisherNpcPosition;
            _troubleNpcPosition = GetNpcScheduledPosition(_lastTimePeriod, NpcIdentity.Azhai);
            _troubleNpcTargetPosition = _troubleNpcPosition;
            _cropSabotageTimer = 14f;
        }

        private void BuildWorld()
        {
            Transform worldParent = _worldRoot != null ? _worldRoot : transform;
            _worldGrid = new GameObject("WorldGrid", typeof(Grid)).GetComponent<Grid>();
            _worldGrid.transform.SetParent(worldParent, false);
            _worldGrid.cellSize = Vector3.one;

            _terrainTilemap = CreateTilemap("TerrainTilemap", _worldGrid.transform, 0);
            _fieldTilemap = CreateTilemap("FieldTilemap", _worldGrid.transform, 2);
            _detailTilemap = CreateTilemap("DetailTilemap", _worldGrid.transform, 3);
            _cropTilemap = CreateTilemap("CropTilemap", _worldGrid.transform, 4);

            _sceneryRoot = CreateGroup("Scenery", worldParent);
            _actorsRoot = CreateGroup("Actors", worldParent);

            PaintTerrain();
            PaintFieldBoundary();
            BuildScenery();
            BuildMovementMap();
            EnsurePlayerSpawnPosition();
            BuildFarmTiles();
            BuildPlayer();
        }

        private void ConfigureCamera()
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                _mainCamera = cameraObject.AddComponent<Camera>();
            }

            _mainCamera.orthographic = true;
            _mainCamera.orthographicSize = 10f;
            _mainCamera.backgroundColor = new Color(0.55f, 0.8f, 0.97f);
            _mainCamera.transform.position = GetClampedCameraPosition();

            if (_mainCamera.GetComponent<AudioListener>() == null && FindFirstObjectByType<AudioListener>() == null)
            {
                _mainCamera.gameObject.AddComponent<AudioListener>();
            }
        }

        private void PaintTerrain()
        {
            for (int y = WalkMinY; y <= WalkMaxY; y++)
            {
                for (int x = WalkMinX; x <= WalkMaxX; x++)
                {
                    _terrainTilemap.SetTile(new Vector3Int(x, y, 0), PickGrassTile(x, y));
                }
            }

            PaintRect(_terrainTilemap, _fieldOriginCell.x - 1, _fieldOriginCell.y - 1, FieldWidth + 2, FieldHeight + 2, FarmPixelArtFactory.GetTile(FarmTileArt.FieldBase));
            PaintFarmPaths();
            PaintFieldFlowers();

            PaintPond(new Vector2Int(13, 2), 4, 2);
            PaintPondFlowers(new Vector2Int(13, 2));
        }

        private void PaintFarmPaths()
        {
            PaintRect(_terrainTilemap, -13, -6, 18, 1, FarmPixelArtFactory.GetTile(FarmTileArt.Path));
            PaintRect(_terrainTilemap, -12, -6, 2, 10, FarmPixelArtFactory.GetTile(FarmTileArt.Path));
            PaintRect(_terrainTilemap, 3, -6, 8, 2, FarmPixelArtFactory.GetTile(FarmTileArt.Path));
            PaintRect(_terrainTilemap, -9, -5, 4, 1, FarmPixelArtFactory.GetTile(FarmTileArt.Path));
            PaintRect(_terrainTilemap, -3, -5, 8, 1, FarmPixelArtFactory.GetTile(FarmTileArt.Path));

            PaintPathEdge(-13, -7, 18, true);
            PaintPathEdge(-13, -5, 18, false);
            PaintPathEdge(3, -7, 8, true);
            PaintPathEdge(3, -4, 8, false);
            PaintVerticalPathEdge(-13, -5, 9, true);
            PaintVerticalPathEdge(-10, -5, 9, false);

            _detailTilemap.SetTile(new Vector3Int(-13, -6, 0), FarmPixelArtFactory.GetTile(FarmTileArt.FlowerYellow));
            _detailTilemap.SetTile(new Vector3Int(-4, -5, 0), FarmPixelArtFactory.GetTile(FarmTileArt.FlowerOrange));
            _detailTilemap.SetTile(new Vector3Int(6, -4, 0), FarmPixelArtFactory.GetTile(FarmTileArt.FlowerYellow));
            _detailTilemap.SetTile(new Vector3Int(10, -6, 0), FarmPixelArtFactory.GetTile(FarmTileArt.FlowerOrange));
        }

        private void PaintFieldFlowers()
        {
            PaintFlowerPatch(new Vector2Int(-8, 8), true);
            PaintFlowerPatch(new Vector2Int(-3, 8), false);
            PaintFlowerPatch(new Vector2Int(2, 8), true);
            PaintFlowerPatch(new Vector2Int(6, 8), false);
            PaintFlowerPatch(new Vector2Int(-10, 1), true);
            PaintFlowerPatch(new Vector2Int(8, 0), false);
        }

        private void PaintPondFlowers(Vector2Int center)
        {
            _detailTilemap.SetTile(new Vector3Int(center.x - 4, center.y + 2, 0), FarmPixelArtFactory.GetTile(FarmTileArt.FlowerYellow));
            _detailTilemap.SetTile(new Vector3Int(center.x - 3, center.y - 3, 0), FarmPixelArtFactory.GetTile(FarmTileArt.FlowerOrange));
            _detailTilemap.SetTile(new Vector3Int(center.x + 3, center.y + 3, 0), FarmPixelArtFactory.GetTile(FarmTileArt.FlowerYellow));
            _detailTilemap.SetTile(new Vector3Int(center.x + 5, center.y - 1, 0), FarmPixelArtFactory.GetTile(FarmTileArt.FlowerOrange));
        }

        private void PaintFieldBoundary()
        {
            int fenceLeft = _fieldOriginCell.x - 1;
            int fenceRight = _fieldOriginCell.x + FieldWidth;
            int fenceBottom = _fieldOriginCell.y - 1;
            int fenceTop = _fieldOriginCell.y + FieldHeight;

            for (int x = fenceLeft; x <= fenceRight; x++)
            {
                if (x < -1 || x > 1)
                {
                    _detailTilemap.SetTile(new Vector3Int(x, fenceBottom, 0), FarmPixelArtFactory.GetTile(FarmTileArt.Fence));
                }

                _detailTilemap.SetTile(new Vector3Int(x, fenceTop, 0), FarmPixelArtFactory.GetTile(FarmTileArt.Fence));
            }

            for (int y = _fieldOriginCell.y; y < _fieldOriginCell.y + FieldHeight; y++)
            {
                _detailTilemap.SetTile(new Vector3Int(fenceLeft, y, 0), FarmPixelArtFactory.GetTile(FarmTileArt.Fence));
                _detailTilemap.SetTile(new Vector3Int(fenceRight, y, 0), FarmPixelArtFactory.GetTile(FarmTileArt.Fence));
            }
        }

        private void PaintPond(Vector2Int center, int radiusX, int radiusY)
        {
            for (int y = center.y - radiusY - 1; y <= center.y + radiusY + 1; y++)
            {
                for (int x = center.x - radiusX - 1; x <= center.x + radiusX + 1; x++)
                {
                    float normalizedX = (x - center.x) / (float)radiusX;
                    float normalizedY = (y - center.y) / (float)radiusY;
                    float distance = normalizedX * normalizedX + normalizedY * normalizedY;

                    if (distance <= 1f)
                    {
                        _terrainTilemap.SetTile(new Vector3Int(x, y, 0), FarmPixelArtFactory.GetTile(FarmTileArt.Water));
                    }
                    else if (distance <= 1.35f)
                    {
                        _terrainTilemap.SetTile(new Vector3Int(x, y, 0), FarmPixelArtFactory.GetTile(FarmTileArt.Path));
                    }
                }
            }
        }

        private void PaintFlowerPatch(Vector2Int origin, bool startWithOrange)
        {
            for (int i = 0; i < 4; i++)
            {
                TileBase flower = (i % 2 == 0) == startWithOrange
                    ? FarmPixelArtFactory.GetTile(FarmTileArt.FlowerOrange)
                    : FarmPixelArtFactory.GetTile(FarmTileArt.FlowerYellow);
                _detailTilemap.SetTile(new Vector3Int(origin.x + i, origin.y + (i % 2), 0), flower);
            }
        }

        private void PaintPathEdge(int xMin, int y, int width, bool startWithGrassA)
        {
            for (int x = xMin; x < xMin + width; x++)
            {
                if (Mathf.Abs((x * 13) + (y * 7)) % 5 == 0)
                {
                    _terrainTilemap.SetTile(new Vector3Int(x, y, 0), FarmPixelArtFactory.GetTile(FarmTileArt.Path));
                    continue;
                }

                bool useGrassA = (((x + y) & 1) == 0) == startWithGrassA;
                _terrainTilemap.SetTile(new Vector3Int(x, y, 0), FarmPixelArtFactory.GetTile(useGrassA ? FarmTileArt.GrassA : FarmTileArt.GrassB));
            }
        }

        private void PaintVerticalPathEdge(int x, int yMin, int height, bool startWithGrassA)
        {
            for (int y = yMin; y < yMin + height; y++)
            {
                if (Mathf.Abs((x * 11) + (y * 19)) % 5 == 0)
                {
                    _terrainTilemap.SetTile(new Vector3Int(x, y, 0), FarmPixelArtFactory.GetTile(FarmTileArt.Path));
                    continue;
                }

                bool useGrassA = (((x + y) & 1) == 0) == startWithGrassA;
                _terrainTilemap.SetTile(new Vector3Int(x, y, 0), FarmPixelArtFactory.GetTile(useGrassA ? FarmTileArt.GrassA : FarmTileArt.GrassB));
            }
        }

        private void BuildScenery()
        {
            CreateSceneryProp("Cabin", new Vector2(-13.2f, 6.1f), FarmSpriteArt.Cabin, 1.18f);
            CreateSceneryProp("TreeTall_NorthWest", new Vector2(-15.1f, 8.9f), FarmSpriteArt.TreeTall, 1.12f);
            CreateSceneryProp("TreeRound_NorthWest", new Vector2(-12.7f, 9.2f), FarmSpriteArt.TreeRound, 1.04f);
            CreateSceneryProp("TreeTall_NorthEast", new Vector2(12.9f, 9.4f), FarmSpriteArt.TreeTall, 1.08f);
            CreateSceneryProp("TreeRound_NorthEast", new Vector2(15.0f, 7.9f), FarmSpriteArt.TreeRound, 1f);
            CreateSceneryProp("TreeRound_SouthEast", new Vector2(14.1f, -7.2f), FarmSpriteArt.TreeRound, 0.96f);
            CreateSceneryProp("TreeTall_SouthWest", new Vector2(-15.0f, -7.5f), FarmSpriteArt.TreeTall, 1.03f);
            CreateSceneryProp("Bush_FieldLeft", new Vector2(-10.4f, 5.2f), FarmSpriteArt.Bush, 0.92f);
            CreateSceneryProp("Bush_FieldRight", new Vector2(8.4f, 6.2f), FarmSpriteArt.Bush, 0.86f);
            CreateSceneryProp("Bush_Pond", new Vector2(8.9f, 1.7f), FarmSpriteArt.Bush, 0.82f);
            CreateSceneryProp("Bush_PathCorner", new Vector2(-13.7f, -4.0f), FarmSpriteArt.Bush, 0.8f);
            CreateWorldSprite("SeedChest", _sceneryRoot, _seedChestPosition, FarmPixelArtFactory.GetSprite(FarmSpriteArt.SeedChest), 10);
            CreateWorldSprite("ShippingBin", _sceneryRoot, _shippingBinPosition, FarmPixelArtFactory.GetSprite(FarmSpriteArt.ShippingBin), 10);
        }

        private void BuildFarmTiles()
        {
            for (int y = 0; y < FieldHeight; y++)
            {
                for (int x = 0; x < FieldWidth; x++)
                {
                    _tiles[x, y] = new FarmTile
                    {
                        Grid = new Vector2Int(x, y),
                        Cell = new Vector3Int(_fieldOriginCell.x + x, _fieldOriginCell.y + y, 0)
                    };
                }
            }
        }

        private void BuildPlayer()
        {
            _playerShadow = CreateWorldSprite(
                "PlayerShadow",
                _actorsRoot,
                _playerPosition + new Vector2(0f, -0.34f),
                FarmPixelArtFactory.GetSprite(FarmSpriteArt.Shadow),
                29);

            _playerRenderer = CreateWorldSprite(
                "Player",
                _actorsRoot,
                _playerPosition,
                FarmPixelArtFactory.GetSprite(FarmSpriteArt.PlayerUp),
                30);

            _playerSideBackLegRoot = CreateGroup("PlayerSideBackLegBone", _actorsRoot);
            _playerSideBackLegRenderer = CreateWorldSprite(
                "PlayerSideBackLeg",
                _playerSideBackLegRoot,
                Vector2.zero,
                FarmPixelArtFactory.GetSprite(FarmSpriteArt.PlayerSideBackLeg),
                29);
            _playerSideBackLegRenderer.transform.localPosition = new Vector3(0f, -PlayerSideWalkHipOffsetY, 0f);
            _playerSideBackLegRenderer.enabled = false;

            _playerSideFrontLegRoot = CreateGroup("PlayerSideFrontLegBone", _actorsRoot);
            _playerSideFrontLegRenderer = CreateWorldSprite(
                "PlayerSideFrontLeg",
                _playerSideFrontLegRoot,
                Vector2.zero,
                FarmPixelArtFactory.GetSprite(FarmSpriteArt.PlayerSideLeg),
                31);
            _playerSideFrontLegRenderer.transform.localPosition = new Vector3(0f, -PlayerSideWalkHipOffsetY, 0f);
            _playerSideFrontLegRenderer.enabled = false;

            _playerToolRenderer = CreateWorldSprite(
                "PlayerTool",
                _actorsRoot,
                _playerPosition,
                FarmPixelArtFactory.GetSprite(FarmSpriteArt.ToolHoe),
                31);
            _playerToolRenderer.enabled = false;

            _targetIndicator = CreateWorldSprite(
                "TargetIndicator",
                _actorsRoot,
                GridToWorld(new Vector2Int(0, 0)),
                FarmPixelArtFactory.GetSprite(FarmSpriteArt.TargetOutline),
                26);

            _targetIndicator.color = new Color(1f, 1f, 1f, 0.28f);

            _npcShadow = CreateWorldSprite(
                "NpcShadow",
                _actorsRoot,
                _npcPosition + new Vector2(0f, -0.34f),
                FarmPixelArtFactory.GetSprite(FarmSpriteArt.Shadow),
                31);

            _npcRenderer = CreateWorldSprite(
                "Npc_Lumi",
                _actorsRoot,
                _npcPosition,
                FarmPixelArtFactory.GetSprite(FarmSpriteArt.NpcLumi),
                32);

            _wandererNpcShadow = CreateWorldSprite(
                "NpcWandererShadow",
                _actorsRoot,
                _wandererNpcPosition + new Vector2(0f, -0.34f),
                FarmPixelArtFactory.GetSprite(FarmSpriteArt.Shadow),
                31);

            _wandererNpcRenderer = CreateWorldSprite(
                "Npc_XiaoTuanzi",
                _actorsRoot,
                _wandererNpcPosition,
                FarmPixelArtFactory.GetSprite(FarmSpriteArt.NpcXiaoTuanzi),
                32);

            _merchantNpcShadow = CreateWorldSprite(
                "NpcMerchantShadow",
                _actorsRoot,
                _merchantNpcPosition + new Vector2(0f, -0.34f),
                FarmPixelArtFactory.GetSprite(FarmSpriteArt.Shadow),
                31);

            _merchantNpcRenderer = CreateWorldSprite(
                "Npc_Qianran",
                _actorsRoot,
                _merchantNpcPosition,
                FarmPixelArtFactory.GetSprite(FarmSpriteArt.NpcQianran),
                32);

            _fisherNpcShadow = CreateWorldSprite(
                "NpcFisherShadow",
                _actorsRoot,
                _fisherNpcPosition + new Vector2(0f, -0.34f),
                FarmPixelArtFactory.GetSprite(FarmSpriteArt.Shadow),
                31);

            _fisherNpcRenderer = CreateWorldSprite(
                "Npc_HaiyinAwa",
                _actorsRoot,
                _fisherNpcPosition,
                FarmPixelArtFactory.GetSprite(FarmSpriteArt.NpcHaiyinAwa),
                32);

            _troubleNpcShadow = CreateWorldSprite(
                "NpcTroubleShadow",
                _actorsRoot,
                _troubleNpcPosition + new Vector2(0f, -0.34f),
                FarmPixelArtFactory.GetSprite(FarmSpriteArt.Shadow),
                31);

            _troubleNpcRenderer = CreateWorldSprite(
                "Npc_Azhai",
                _actorsRoot,
                _troubleNpcPosition,
                FarmPixelArtFactory.GetSprite(FarmSpriteArt.NpcAzhai),
                32);
        }

        private void BuildFeedbackSystem()
        {
            _uiClickClip = FarmRuntimeAudioFactory.CreateUiClickClip();
            _tileClickClip = FarmRuntimeAudioFactory.CreateTileClickClip();
            _blockedClip = FarmRuntimeAudioFactory.CreateBlockedClip();
            _hoeHitClip = FarmRuntimeAudioFactory.CreateHoeHitClip();
            _waterHitClip = FarmRuntimeAudioFactory.CreateWaterHitClip();
            _seedHitClip = FarmRuntimeAudioFactory.CreateSeedHitClip();
            _harvestHitClip = FarmRuntimeAudioFactory.CreateHarvestHitClip();

            _clickFeedbackRenderer = CreateWorldSprite(
                "ClickFeedback",
                _actorsRoot,
                GridToWorld(new Vector2Int(0, 0)),
                FarmPixelArtFactory.GetSprite(FarmSpriteArt.TargetOutline),
                27);
            _clickFeedbackRenderer.enabled = false;

            _toolHitEffectRenderer = CreateWorldSprite(
                "ToolHitEffect",
                _actorsRoot,
                GridToWorld(new Vector2Int(0, 0)),
                FarmPixelArtFactory.GetSprite(FarmSpriteArt.EffectHoeHit),
                28);
            _toolHitEffectRenderer.enabled = false;
        }

        private void BuildMovementMap()
        {
            _blockedWorldRects.Clear();
            _blockedWalkCells.Clear();

            int fenceLeft = _fieldOriginCell.x - 1;
            int fenceRight = _fieldOriginCell.x + FieldWidth;
            int fenceBottom = _fieldOriginCell.y - 1;
            int fenceTop = _fieldOriginCell.y + FieldHeight;

            AddFenceCollisionBlocks(fenceLeft, fenceRight, fenceBottom, fenceTop);

            AddBlockedRect(-8.85f, -3.25f, 1.35f, 0.72f);
            AddBlockedRect(-13.95f, 5.3f, 1.5f, 1.1f);
            AddBlockedRect(-15.6f, 8.1f, 1.0f, 1.0f);
            AddBlockedRect(-13.25f, 8.45f, 1.1f, 0.95f);
            AddBlockedRect(12.4f, 8.6f, 1.0f, 1.0f);
            AddBlockedRect(14.45f, 7.15f, 1.1f, 0.95f);
            AddBlockedRect(13.55f, -7.95f, 1.1f, 0.95f);
            AddBlockedRect(-15.5f, -8.3f, 1.0f, 1.0f);
            AddBlockedRect(-10.85f, 4.9f, 0.9f, 0.62f);
            AddBlockedRect(7.95f, 5.9f, 0.9f, 0.62f);
            AddBlockedRect(8.5f, 1.4f, 0.8f, 0.58f);
            AddBlockedRect(-14.1f, -4.3f, 0.8f, 0.58f);

            AddWaterCollisionBlocks();

            for (int y = WalkMinY; y <= WalkMaxY; y++)
            {
                for (int x = WalkMinX; x <= WalkMaxX; x++)
                {
                    Vector2Int walkCell = new Vector2Int(x, y);
                    if (IsBlockedAt(WalkCellToWorld(walkCell)))
                    {
                        _blockedWalkCells.Add(walkCell);
                    }
                }
            }
        }

        private SpriteRenderer CreateSceneryProp(string objectName, Vector2 position, FarmSpriteArt spriteArt, float scale)
        {
            SpriteRenderer renderer = CreateWorldSprite(
                objectName,
                _sceneryRoot,
                position,
                FarmPixelArtFactory.GetSprite(spriteArt),
                GetActorSortBase(position.y));
            renderer.transform.localScale = Vector3.one * scale;
            return renderer;
        }


        private static Tilemap CreateTilemap(string objectName, Transform parent, int sortingOrder)
        {
            GameObject tilemapObject = new GameObject(objectName, typeof(Tilemap), typeof(TilemapRenderer));
            tilemapObject.transform.SetParent(parent, false);

            TilemapRenderer renderer = tilemapObject.GetComponent<TilemapRenderer>();
            renderer.mode = TilemapRenderer.Mode.Individual;
            renderer.sortOrder = TilemapRenderer.SortOrder.TopLeft;
            renderer.sortingOrder = sortingOrder;

            return tilemapObject.GetComponent<Tilemap>();
        }

        private static void PaintRect(Tilemap tilemap, int xMin, int yMin, int width, int height, TileBase tile)
        {
            for (int y = yMin; y < yMin + height; y++)
            {
                for (int x = xMin; x < xMin + width; x++)
                {
                    tilemap.SetTile(new Vector3Int(x, y, 0), tile);
                }
            }
        }

        private static TileBase PickGrassTile(int x, int y)
        {
            int pattern = Mathf.Abs(x * 17 + y * 31) % 7;
            if (pattern == 0)
            {
                return FarmPixelArtFactory.GetTile(FarmTileArt.GrassFlowers);
            }

            return (pattern % 2 == 0)
                ? FarmPixelArtFactory.GetTile(FarmTileArt.GrassA)
                : FarmPixelArtFactory.GetTile(FarmTileArt.GrassB);
        }

        private static Transform CreateGroup(string objectName, Transform parent)
        {
            GameObject group = new GameObject(objectName);
            group.transform.SetParent(parent, false);
            return group.transform;
        }

        private static SpriteRenderer CreateWorldSprite(string objectName, Transform parent, Vector2 position, Sprite sprite, int sortingOrder)
        {
            GameObject spriteObject = new GameObject(objectName, typeof(SpriteRenderer));
            spriteObject.transform.SetParent(parent, false);
            spriteObject.transform.position = position;

            SpriteRenderer renderer = spriteObject.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private TextMeshProUGUI CreateText(
            Transform parent,
            string objectName,
            float maxFontSize,
            float minFontSize,
            TextAlignmentOptions alignment)
        {
            RectTransform rect = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(16f, 16f);
            rect.offsetMax = new Vector2(-16f, -16f);

            TextMeshProUGUI text = rect.GetComponent<TextMeshProUGUI>();
            text.text = string.Empty;
            text.fontSize = maxFontSize;
            text.fontSizeMin = minFontSize;
            text.enableAutoSizing = true;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.alignment = alignment;
            text.color = new Color(0.95f, 0.94f, 0.91f);
            return text;
        }

    }
}
