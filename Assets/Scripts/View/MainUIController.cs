using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Networking;
using AssetLibrary.Core;
using AssetLibrary.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine.Rendering;

namespace AssetLibrary.View
{
    // 【核心入口】负责变量声明、初始化查找 (Q) 和事件绑定
    public partial class MainUIController : MonoBehaviour
    {
        #region --- 变量定义 (所有分部类共享) ---
        
        [Header("UI References")]
        public VisualTreeAsset ItemTemplate;
        public Texture2D DefaultIcon;
        
        [Header("Core References")]
        public ModelPhotoStudio PhotoStudio;    

        // === 设置相关 ===
        private const string PREF_LIB_PATH = "AssetLibraryPath";
        private string _currentLibraryPath;
        
        // --- UI 基础引用 ---
        private UIDocument _doc;
        private VisualElement _gridContainer;
        private ScrollView _assetScrollView;
        private VisualElement _welcomePanel;
        private TextField _searchField;
        
        // --- [新] 顶部导航 & 下拉菜单 ---
        private Button _navBtnGallery;
        private Button _navBtnSettings;
        
        private Button _btnIngestModel;   // 对应 UXML: BtnIngestModel
        private Button _btnIngestTexture; // 对应 UXML: BtnIngestTexture
        private Button _btnIngestSound;   // 对应 UXML: BtnIngestSound
        private Button _btnIngestVFX;     // 对应 UXML: BtnIngestVFX
        private Button _btnIngestVideo;   // 对应 UXML: BtnIngestVideo

        // --- 侧边栏筛选 ---
        private Button _btnModel;
        private Button _btnTexture;
        private Button _btnSound;
        private Button _btnScan; // 刷新按钮

        // --- Loading UI ---
        private VisualElement _loadingOverlay;  
        private VisualElement _loadingFill;  
        private Label _loadingLabel;          

        // --- 详情面板 (逻辑在 _Detail.cs) ---
        private VisualElement _detailPanel;
        private Image _detailImage; 
        private Button _btnCloseDetail;
        private Button _btnViewTextured;  
        private Button _btnViewClay;  
        private VisualElement _viewModeSwitch; 
        private Button _btnDetailFullscreen;  
        private Label _detailHintLabel;  

        private VisualElement _viewContainer;
        private Label _detailName;
        private Label _detailType;
        private Label _detailTags;
        private Label _detailDesc;
        private Label _detailPath;
        private Button _btnEdit;
        private Button _btnOpenInExplorer;
        private Button _btnDelete;

        // --- 编辑模式 (逻辑在 _Detail.cs) ---
        private VisualElement _editContainer;
        private TextField _editTagsField;
        private TextField _editDescField;
        private Button _btnCancelEdit;
        private Button _btnSaveEdit;

        // --- 删除/退出/设置 弹窗 ---
        private VisualElement _deleteConfirmOverlay;
        private Button _btnOverlayCancelDelete;
        private Button _btnOverlayConfirmDelete;
        private Label _deleteAssetNameLabel;

        private VisualElement _settingsPanel;
        private TextField _settingsPathField;
        private Button _btnBrowsePath;
        private Button _btnSaveSettings;
        private Button _btnCancelSettings;

        private Button _btnCloseApp;
        private VisualElement _quitConfirmDialog;
        private Button _btnCancelQuit;
        private Button _btnConfirmQuit;

        // --- 查看器 (Viewer) ---
        private VisualElement _imageViewerOverlay;
        private Image _fullSizeImage;
        private Button _btnCloseImageViewer;
        
        private VisualElement _modelViewerOverlay;  
        private Image _modelFullSizeImage;  
        private Button _btnCloseModelViewer;
        private Button _btnFullViewTextured;  
        private Button _btnFullViewClay; 

        // --- 状态变量 ---
        private Vector3 _imageScale = Vector3.one;
        private Vector3 _imagePosition = Vector3.zero;
        private bool _isDraggingImage = false;
        private Vector2 _lastMousePosition;
        private const float MIN_SCALE = 0.1f;
        private const float MAX_SCALE = 10.0f;
        private const float ZOOM_SENSITIVITY = 0.001f; 

        private bool _isTexturedMode = true; 
        private bool _isDetailInteracting = false;  
        private Vector3 _detailImageScale = Vector3.one;  
        private Vector3 _detailImagePos = Vector3.zero;  
        
        private bool _isModelViewerDragging = false;  
        private bool _isPreviewDragging = false;
        private Vector3 _lastInputMousePos;

        // 全局选中状态
        private AssetMetaData _selectedAsset;
        private AssetType? _activeTypeFilter = null;
        private string _lastLoadedModelPath = ""; 

        // --- 兼容旧代码的保留变量 (可留空，防止报错) ---
        private VisualElement _importPanel; // 旧导入面板引用
        // ... (如果 _Import.cs 报错缺少变量，可以在此补齐，但 logic 不再调用)

        #endregion

        void OnEnable()
        {
            _doc = GetComponent<UIDocument>();
            if (_doc == null) { enabled = false; return; }
            var root = _doc.rootVisualElement;

            // =========================================================
            // 1. 查找 UI 元素 (Q)
            // =========================================================
            
            // --- 导航栏 ---
            _navBtnGallery = root.Q<Button>("NavBtnGallery");
            _navBtnSettings = root.Q<Button>("NavBtnSettings");
            _btnCloseApp = root.Q<Button>("BtnCloseApp");

            // --- 顶部下拉菜单 (Ingest) ---
            _btnIngestModel = root.Q<Button>("BtnIngestModel");     // 新名字
            _btnIngestTexture = root.Q<Button>("BtnIngestTexture"); // 新名字
            _btnIngestSound = root.Q<Button>("BtnIngestSound");     // 新名字
            _btnIngestVFX = root.Q<Button>("BtnIngestVFX");
            _btnIngestVideo = root.Q<Button>("BtnIngestVideo");

            // --- 侧边栏 ---
            _searchField = root.Q<TextField>("SearchField");
            _btnScan = root.Q<Button>("BtnScan");
            _btnModel = root.Q<Button>("BtnModel");
            _btnTexture = root.Q<Button>("BtnTexture");
            _btnSound = root.Q<Button>("BtnSound");

            // --- 主内容区 ---
            _gridContainer = root.Q<VisualElement>("AssetGrid");
            _assetScrollView = root.Q<ScrollView>("AssetScrollView");
            _welcomePanel = root.Q<VisualElement>("WelcomePanel");

            // --- 详情面板 ---
            _detailPanel = root.Q<VisualElement>("DetailPanel");
            _detailImage = root.Q<Image>("DetailImage");
            _loadingOverlay = root.Q<VisualElement>("LoadingOverlay");
            _loadingFill = root.Q<VisualElement>("LoadingFill");
            _loadingLabel = root.Q<Label>("LoadingLabel");
            _btnCloseDetail = root.Q<Button>("BtnCloseDetail");
            _btnDetailFullscreen = root.Q<Button>("BtnDetailFullscreen");
            _detailHintLabel = root.Q<Label>("DetailHintLabel");
            _btnViewTextured = root.Q<Button>("BtnViewTextured");
            _btnViewClay = root.Q<Button>("BtnViewClay");
            _viewModeSwitch = root.Q<VisualElement>("ViewModeSwitch");

            _viewContainer = root.Q<VisualElement>("ViewContainer");
            _detailName = root.Q<Label>("DetailName");
            _detailType = root.Q<Label>("DetailType");
            _detailTags = root.Q<Label>("DetailTags");
            _detailDesc = root.Q<Label>("DetailDesc");
            _detailPath = root.Q<Label>("DetailPath");
            _btnEdit = root.Q<Button>("BtnEdit");
            _btnOpenInExplorer = root.Q<Button>("BtnOpenInExplorer");
            _btnDelete = root.Q<Button>("BtnDelete");

            _editContainer = root.Q<VisualElement>("EditContainer");
            _editTagsField = root.Q<TextField>("EditTagsField");
            _editDescField = root.Q<TextField>("EditDescField");
            _btnCancelEdit = root.Q<Button>("BtnCancelEdit");
            _btnSaveEdit = root.Q<Button>("BtnSaveEdit");

            // --- 弹窗 ---
            _deleteConfirmOverlay = root.Q<VisualElement>("DeleteConfirmOverlay");
            _btnOverlayCancelDelete = root.Q<Button>("BtnOverlayCancelDelete");
            _btnOverlayConfirmDelete = root.Q<Button>("BtnOverlayConfirmDelete");
            _deleteAssetNameLabel = root.Q<Label>("DeleteAssetName");

            _settingsPanel = root.Q<VisualElement>("SettingsPanel");
            _settingsPathField = root.Q<TextField>("SettingsPathField");
            _btnBrowsePath = root.Q<Button>("BtnBrowsePath");
            _btnSaveSettings = root.Q<Button>("BtnSaveSettings");
            _btnCancelSettings = root.Q<Button>("BtnCancelSettings");

            _quitConfirmDialog = root.Q<VisualElement>("QuitConfirmDialog");
            _btnCancelQuit = root.Q<Button>("BtnCancelQuit");
            _btnConfirmQuit = root.Q<Button>("BtnConfirmQuit");

            // --- 全屏查看器 ---
            _imageViewerOverlay = root.Q<VisualElement>("ImageViewerOverlay");
            _fullSizeImage = root.Q<Image>("FullSizeImage");
            _btnCloseImageViewer = root.Q<Button>("BtnCloseImageViewer");

            _modelViewerOverlay = root.Q<VisualElement>("ModelViewerOverlay");
            _modelFullSizeImage = root.Q<Image>("ModelFullSizeImage");
            _btnCloseModelViewer = root.Q<Button>("BtnCloseModelViewer");
            _btnFullViewTextured = root.Q<Button>("BtnFullViewTextured");
            _btnFullViewClay = root.Q<Button>("BtnFullViewClay");

            // --- 初始化其他分部类的 UI ---
            InitializeBatchImportUI(); // 定义在 _BatchImport.cs 中

            // =========================================================
            // 2. 绑定事件
            // =========================================================

            var btnTestBatch = root.Q<Button>("BtnTestBatchModel");  
            if (btnTestBatch != null)  
                btnTestBatch.clicked += () =>  
                {  
                    Debug.Log("[Test] Open Batch in Model mode");  
                    SwitchToBatchMode(0);  
                };  
            // 导航
            if (_navBtnGallery != null) _navBtnGallery.clicked += () => { 
                _activeTypeFilter = null; 
                UpdateUIState(); // 定义在 _Browser.cs
            };
            if (_navBtnSettings != null) _navBtnSettings.clicked += () => ShowSettings(true);

            // [核心] 下拉菜单点击 -> 打开批量导入并切换到对应模式
            // SwitchToBatchMode 是下面定义的一个 helper 方法，用来桥接 UI 和 Batch 逻辑
            if (_btnIngestModel != null)  
                _btnIngestModel.clicked += () =>  
                {  
                    Debug.Log("[Ingest] Model clicked");  
                    SwitchToBatchMode(0);  
                };  
            if (_btnIngestTexture != null)  
                _btnIngestTexture.clicked += () =>  
                {  
                    Debug.Log("[Ingest] Texture clicked");  
                    SwitchToBatchMode(1);  
                };  
            if (_btnIngestSound != null)  
                _btnIngestSound.clicked += () =>  
                {  
                    Debug.Log("[Ingest] Sound clicked");  
                    SwitchToBatchMode(2);  
                };  
            
            if (_btnIngestVFX != null) _btnIngestVFX.clicked += () => Debug.Log("VFX Import not implemented.");
            if (_btnIngestVideo != null) _btnIngestVideo.clicked += () => Debug.Log("Video Import not implemented.");

            // 侧边栏 & 列表 (Browser Logic)
            if (_btnScan != null) _btnScan.clicked += () => { LibraryManager.Instance.ScanLibrary(); UpdateUIState(); };
            if (_searchField != null) _searchField.RegisterValueChangedCallback(evt => RefreshList(evt.newValue)); // _Browser.cs

            if (_btnModel != null) _btnModel.clicked += () => ToggleFilter(AssetType.Model, _btnModel);     // _Browser.cs
            if (_btnTexture != null) _btnTexture.clicked += () => ToggleFilter(AssetType.Texture, _btnTexture); // _Browser.cs
            if (_btnSound != null) _btnSound.clicked += () => ToggleFilter(AssetType.Sound, _btnSound);     // _Browser.cs

            // 详情页 (Detail Logic)
            if (_btnCloseDetail != null) _btnCloseDetail.clicked += () => _detailPanel.style.display = DisplayStyle.None;
            if (_btnDetailFullscreen != null) _btnDetailFullscreen.clicked += OnDetailFullscreenClicked; // _Detail.cs

            // 视图切换
            if (_btnViewTextured != null) _btnViewTextured.clicked += () => SetDetailViewMode(true); // _Detail.cs
            if (_btnViewClay != null) _btnViewClay.clicked += () => SetDetailViewMode(false);
            if (_btnFullViewTextured != null) _btnFullViewTextured.clicked += () => SetDetailViewMode(true);
            if (_btnFullViewClay != null) _btnFullViewClay.clicked += () => SetDetailViewMode(false);

            if (_btnCloseModelViewer != null) _btnCloseModelViewer.clicked += CloseModelViewer; // _Detail.cs

            // 编辑与删除
            if (_btnOpenInExplorer != null) _btnOpenInExplorer.clicked += OpenCurrentAssetInExplorer;
            if (_btnEdit != null) _btnEdit.clicked += EnableEditMode;
            if (_btnDelete != null) _btnDelete.clicked += ShowDeleteConfirmationOverlay;
            if (_btnCancelEdit != null) _btnCancelEdit.clicked += CancelEditMode;
            if (_btnSaveEdit != null) _btnSaveEdit.clicked += SaveEdit;

            if (_btnOverlayCancelDelete != null) _btnOverlayCancelDelete.clicked += () => _deleteConfirmOverlay.style.display = DisplayStyle.None;
            if (_btnOverlayConfirmDelete != null) _btnOverlayConfirmDelete.clicked += () => {
                _deleteConfirmOverlay.style.display = DisplayStyle.None;
                ConfirmDeleteAsset(); // _Detail.cs
            };

            // 设置
            if (_btnBrowsePath != null) _btnBrowsePath.clicked += OnBrowseLibraryPath;
            if (_btnSaveSettings != null) _btnSaveSettings.clicked += OnSaveSettings;
            if (_btnCancelSettings != null) _btnCancelSettings.clicked += () => _settingsPanel.style.display = DisplayStyle.None;

            // 退出
            if (_btnCloseApp != null) _btnCloseApp.clicked += ShowQuitConfirm;
            if (_btnCancelQuit != null) _btnCancelQuit.clicked += HideQuitConfirm;
            if (_btnConfirmQuit != null) _btnConfirmQuit.clicked += Application.Quit;

            // 全屏交互注册
            if (_modelFullSizeImage != null)
            {
                _modelFullSizeImage.RegisterCallback<MouseDownEvent>(OnModelViewerMouseDown);
                _modelFullSizeImage.RegisterCallback<MouseUpEvent>(OnModelViewerMouseUp);
                _modelFullSizeImage.RegisterCallback<MouseMoveEvent>(OnModelViewerMouseMove);
                _modelFullSizeImage.RegisterCallback<WheelEvent>(OnModelViewerWheel);
            }
            if (_detailImage != null)
            {
                _detailImage.RegisterCallback<MouseDownEvent>(OnDetailMouseDown);
                _detailImage.RegisterCallback<MouseUpEvent>(OnDetailMouseUp);
                _detailImage.RegisterCallback<MouseMoveEvent>(OnDetailMouseMove);
                _detailImage.RegisterCallback<WheelEvent>(OnDetailWheel);
                _detailImage.RegisterCallback<ClickEvent>(OnDetailImageClicked);
            }
            if (_imageViewerOverlay != null)
            {
                _imageViewerOverlay.RegisterCallback<WheelEvent>(OnImageScroll);
                _imageViewerOverlay.RegisterCallback<MouseDownEvent>(OnImageMouseDown);
                _imageViewerOverlay.RegisterCallback<MouseUpEvent>(OnImageMouseUp);
                _imageViewerOverlay.RegisterCallback<MouseMoveEvent>(OnImageMouseMove);
            }
            if (_btnCloseImageViewer != null) _btnCloseImageViewer.clicked += CloseImageViewer;
        }

        // === 桥接方法：下拉菜单 -> 批量导入逻辑 ===
        private void SwitchToBatchMode(int modeIndex)
        {
            if (_batchPanel != null)
            {
                _batchPanel.style.display = DisplayStyle.Flex;
                
                // _batchModeSelector 定义在 _BatchImport.cs 中，因为是 partial class 所以可以直接访问
                if (_batchModeSelector != null)
                {
                    _batchModeSelector.value = modeIndex;
                    
                    // 手动触发一下刷新逻辑 (因为代码设置 value 可能不会触发 ChangeEvent，视 Unity 版本而定)
                    // ResetBatchImportUI 定义在 _BatchImport.cs
                    ResetBatchImportUI(); 
                    
                    // 根据模式更新 UI 显示 (Model 显示三栏，其他隐藏中间栏)
                    bool isModelMode = (modeIndex == 0);
                    if (_batchSlotContainer != null && _batchSlotContainer.parent != null)
                        _batchSlotContainer.parent.style.display = isModelMode ? DisplayStyle.Flex : DisplayStyle.None;
                    
                    if (_batchStatusLabel != null)
                        _batchStatusLabel.text = "Mode switched. Please select folder.";
                }
            }
        }

        void Start()
        {
            Application.targetFrameRate = 60;
            Application.runInBackground = true;
            OnDemandRendering.renderFrameInterval = 5;
            CheckLibraryPath();
            Debug.Log($"[UI] BtnIngestModel found? {_btnIngestModel != null}");  
            Debug.Log($"[UI] BtnIngestTexture found? {_btnIngestTexture != null}");  
            Debug.Log($"[UI] BtnIngestSound found? {_btnIngestSound != null}");  
        }

        void Update()
        {
            bool hasInput = (Input.mousePosition != _lastInputMousePos) || Input.anyKey || Input.GetMouseButton(0) || Input.mouseScrollDelta.y != 0;
            bool isLoading = _loadingOverlay != null && _loadingOverlay.style.display == DisplayStyle.Flex;

            OnDemandRendering.renderFrameInterval = (hasInput || isLoading) ? 1 : 5;
            _lastInputMousePos = Input.mousePosition;
        }

        // --- 简单的设置/路径逻辑 (保留在主文件) ---
        private void CheckLibraryPath()
        {
            _currentLibraryPath = PlayerPrefs.GetString(PREF_LIB_PATH, "");
            if (string.IsNullOrEmpty(_currentLibraryPath) || !Directory.Exists(_currentLibraryPath))
                ShowSettings(false);
            else
            {
                LibraryManager.Instance.SetLibraryRoot(_currentLibraryPath);
                LibraryManager.Instance.ScanLibrary();
                UpdateUIState(); // _Browser.cs
            }
        }

        private void ShowSettings(bool allowCancel)
        {
            if (_settingsPanel == null) return;
            _settingsPathField.value = string.IsNullOrEmpty(_settingsPathField.value) 
                ? Path.Combine(Application.persistentDataPath, "MyAssetsLibrary") 
                : _currentLibraryPath;
            
            _btnCancelSettings.style.display = allowCancel ? DisplayStyle.Flex : DisplayStyle.None;
            _settingsPanel.style.display = DisplayStyle.Flex;
        }

       private void OnBrowseLibraryPath()  
        {  
            var results = TriLibCore.SFB.StandaloneFileBrowser.OpenFolderPanel("Select Library", "", false);  
            // 修改点：将 .Length 改为 .Count，并增加了空值检查  
            if (results != null && results.Count > 0 && !string.IsNullOrEmpty(results[0].Name))   
            {  
                _settingsPathField.value = results[0].Name;  
            }  
        }  

        private void OnSaveSettings()
        {
            string newPath = _settingsPathField.value;
            if (string.IsNullOrEmpty(newPath)) return;
            if (!Directory.Exists(newPath)) Directory.CreateDirectory(newPath);
            
            _currentLibraryPath = newPath;
            PlayerPrefs.SetString(PREF_LIB_PATH, _currentLibraryPath);
            PlayerPrefs.Save();
            
            LibraryManager.Instance.SetLibraryRoot(_currentLibraryPath);
            LibraryManager.Instance.ScanLibrary();
            _settingsPanel.style.display = DisplayStyle.None;
            UpdateUIState();
        }

        private void ShowQuitConfirm() => _quitConfirmDialog.style.display = DisplayStyle.Flex;
        private void HideQuitConfirm() => _quitConfirmDialog.style.display = DisplayStyle.None;

        // --- 辅助：加载图片协程 ---
        private System.Collections.IEnumerator LoadImageToBackground(string path, VisualElement target)
        {
            string url = new System.Uri(path).AbsoluteUri;
            using (UnityWebRequest uwr = UnityWebRequestTexture.GetTexture(url))
            {
                yield return uwr.SendWebRequest();
                if (uwr.result == UnityWebRequest.Result.Success)
                {
                    Texture2D tex = DownloadHandlerTexture.GetContent(uwr);
                    target.style.backgroundImage = new StyleBackground(tex);
                    target.style.backgroundColor = Color.white; 
                }
            }
        }
        
        protected System.Collections.IEnumerator LoadImageToElement(string path, Image targetImage)
        {
            string url = new System.Uri(path).AbsoluteUri;
            using (UnityWebRequest uwr = UnityWebRequestTexture.GetTexture(url))
            {
                yield return uwr.SendWebRequest();
                if (uwr.result == UnityWebRequest.Result.Success)
                {
                    Texture2D tex = DownloadHandlerTexture.GetContent(uwr);
                    targetImage.image = tex;
                    targetImage.scaleMode = ScaleMode.ScaleToFit; 
                }
            }
        }

        private Color GetColorByType(AssetType type)
        {
            switch (type)
            {
                case AssetType.Model: return new Color(0.3f, 0.5f, 0.8f); 
                case AssetType.Texture: return new Color(0.8f, 0.3f, 0.3f); 
                case AssetType.Sound: return new Color(0.3f, 0.8f, 0.3f); 
                default: return Color.gray;
            }
        }
    }
}
