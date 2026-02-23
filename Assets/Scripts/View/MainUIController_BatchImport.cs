using UnityEngine;
using UnityEngine.UIElements;
using AssetLibrary.Core;
using AssetLibrary.Utilities;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Collections;
using TriLibCore;
using TriLibCore.SFB;
using System;

namespace AssetLibrary.View
{
    public partial class MainUIController
    {
        // === 批量导入 UI 引用 ===
        private VisualElement _batchPanel;
        private Label _batchStatusLabel;
        private ScrollView _batchModelList;
        private VisualElement _batchSlotContainer;
        private ScrollView _batchTextureGallery; 
        private TextField _batchTextureSearch;

        private Button _btnBatchSelectFolder;
        private Button _btnBatchAutoMatch;
        private Button _btnBatchProcess;
        private Button _btnCloseBatch;
        
        // === 批量导入 数据状态 ===
        private List<ImportTask> _batchTasks = new List<ImportTask>();
        private List<string> _foundTextureFiles = new List<string>();
        private ImportTask _currentEditingTask = null; 

        // === 手动指定贴图的状态变量 ===  
        private VisualElement _activeSlotUI = null;      
        private Action<string> _activeSlotSetter = null; 
        private ImportTask _activeSlotTask = null;       

        // === 批量预览图引用 (左下角) ===  
        private Image _batchPreviewImage;   
        private VisualElement _batchPreviewBox;

        // === [修改] 虚拟化网格配置 (4列布局) ===
        private ListView _textureGridView; 
        private const int GRID_COLUMNS = 4; 
        private const int GRID_ROW_HEIGHT = 120; 
        private RadioButtonGroup _batchModeSelector; 
        // ==================================================================================
        // 初始化逻辑
        // ==================================================================================

        private void InitializeBatchImportUI()
        {
            var root = _doc.rootVisualElement;
            _batchPanel = root.Q<VisualElement>("BatchImportPanel");
            
            _batchStatusLabel = root.Q<Label>("BatchStatusLabel");
            _batchModelList = root.Q<ScrollView>("BatchModelList");
            _batchSlotContainer = root.Q<VisualElement>("BatchSlotContainer");
            _batchTextureGallery = root.Q<ScrollView>("BatchTextureGallery");
            _batchTextureSearch = root.Q<TextField>("BatchTextureSearch");
            _batchModeSelector = root.Q<RadioButtonGroup>("BatchModeSelector");

            _btnBatchSelectFolder = root.Q<Button>("BtnBatchSelectFolder");
            _btnBatchAutoMatch = root.Q<Button>("BtnBatchAutoMatch");
            _btnBatchProcess = root.Q<Button>("BtnBatchProcess");
            _btnCloseBatch = root.Q<Button>("BtnCloseBatch");

            // 1. 调整左侧布局 (3D 预览窗口)
            if (_batchModelList != null)  
            {  
                var leftColumn = _batchModelList.parent;  
                leftColumn.style.width = 280;   
                leftColumn.style.minWidth = 280;  
                leftColumn.style.backgroundColor = new Color(0.13f, 0.13f, 0.13f);  
                leftColumn.style.borderRightWidth = 1;  
                leftColumn.style.borderRightColor = new Color(1,1,1,0.1f);  

                _batchModelList.style.flexGrow = 1; 
                _batchModelList.style.marginBottom = 10;  
                _batchModelList.style.borderBottomWidth = 1;  
                _batchModelList.style.borderBottomColor = new Color(1,1,1,0.1f);  

                // 创建预览容器
                _batchPreviewBox = new VisualElement();  
                _batchPreviewBox.style.height = 260;   
                _batchPreviewBox.style.width = Length.Percent(100);  
                _batchPreviewBox.style.backgroundColor = Color.black;  
                _batchPreviewBox.style.marginTop = 5;  
                _batchPreviewBox.style.marginBottom = 10; 
                _batchPreviewBox.style.alignSelf = Align.Center;  
                _batchPreviewBox.style.borderTopWidth = 1; _batchPreviewBox.style.borderBottomWidth = 1;  
                _batchPreviewBox.style.borderLeftWidth = 1; _batchPreviewBox.style.borderRightWidth = 1;  
                Color borderColor = new Color(0.4f, 0.4f, 0.4f);  
                _batchPreviewBox.style.borderTopColor = borderColor; _batchPreviewBox.style.borderBottomColor = borderColor;  
                _batchPreviewBox.style.borderLeftColor = borderColor; _batchPreviewBox.style.borderRightColor = borderColor;  

                var previewTitle = new Label("3D PREVIEW");  
                previewTitle.style.position = Position.Absolute;  
                previewTitle.style.top = 5; previewTitle.style.left = 5;  
                previewTitle.style.fontSize = 10; previewTitle.style.color = new Color(1,1,1,0.5f);  
                previewTitle.style.backgroundColor = new Color(0,0,0,0.5f);  
                previewTitle.style.paddingLeft = 4; previewTitle.style.paddingRight = 4;  
                previewTitle.style.borderBottomRightRadius = 4;  

                _batchPreviewImage = new Image();  
                _batchPreviewImage.style.width = Length.Percent(100);  
                _batchPreviewImage.style.height = Length.Percent(100);  
                
                _batchPreviewImage.RegisterCallback<MouseDownEvent>(evt => { if(evt.button==0) { _isPreviewDragging=true; _batchPreviewImage.CaptureMouse(); } });  
                _batchPreviewImage.RegisterCallback<MouseUpEvent>(evt => { _isPreviewDragging=false; _batchPreviewImage.ReleaseMouse(); });  
                _batchPreviewImage.RegisterCallback<MouseMoveEvent>(evt => { if(_isPreviewDragging && PhotoStudio != null) PhotoStudio.RotatePreview(evt.mouseDelta.x, evt.mouseDelta.y); });  
                _batchPreviewImage.RegisterCallback<WheelEvent>(evt => { if(PhotoStudio!=null) { float zoom = (evt.delta.y > 0 ? -1 : 1) * 0.2f; PhotoStudio.ZoomPreview(zoom); } });  

                _batchPreviewBox.Add(_batchPreviewImage);  
                _batchPreviewBox.Add(previewTitle);  
                leftColumn.Add(_batchPreviewBox);  
                _batchPreviewBox.style.display = DisplayStyle.None;   
            }

            InitializeTextureGrid();
            if (_batchModeSelector != null)  
            {  
                _batchModeSelector.RegisterValueChangedCallback(evt =>   
                {  
                    // 切换模式时，重置 UI 并清空当前列表  
                    ResetBatchImportUI();  
                    
                    // 根据模式显示/隐藏 "中间栏" (材质设置只对 Model 有用)  
                    // 0=Model, 1=Texture, 2=Sound  
                    bool isModelMode = evt.newValue == 0;  
                    var slotContainerParent = _batchSlotContainer.parent; // 中间栏的父容器  
                    if (slotContainerParent != null)   
                        slotContainerParent.style.display = isModelMode ? DisplayStyle.Flex : DisplayStyle.None;  

                    // 更新状态文字  
                    _batchStatusLabel.text = "Mode changed. Please select folder again.";  
                });  
            }  
            if (_btnBatchSelectFolder != null) _btnBatchSelectFolder.clicked += OnBatchSelectFolderClicked;
            if (_btnCloseBatch != null)     
            {   
                _btnCloseBatch.clicked += () =>     
                {   
                    _batchPanel.style.display = DisplayStyle.None;   
                    if (PhotoStudio != null) PhotoStudio.ClearPreviewModel(); 
                    if (_batchPreviewBox != null) _batchPreviewBox.style.display = DisplayStyle.None;
                };   
            }   
            if (_btnBatchAutoMatch != null) _btnBatchAutoMatch.clicked += RunBatchAutoMatch;
            if (_btnBatchProcess != null) _btnBatchProcess.clicked += StartBatchImportProcess;
            
            var btnOpenBatch = root.Q<Button>("BtnOpenBatchImport");
            if(btnOpenBatch != null) btnOpenBatch.clicked += () => _batchPanel.style.display = DisplayStyle.Flex;
            
            if (_batchTextureSearch != null)
                _batchTextureSearch.RegisterValueChangedCallback(evt => RefreshBatchTextureGallery());
        }

        // ==================================================================================
        // [核心] 虚拟化网格逻辑
        // ==================================================================================

        private void InitializeTextureGrid()
        {
            if (_batchTextureGallery == null) return;

            _textureGridView = new ListView();
            _textureGridView.style.flexGrow = 1; 
            _textureGridView.itemHeight = GRID_ROW_HEIGHT; 
            _textureGridView.showBorder = false;
            _textureGridView.selectionType = SelectionType.None; 
            
            _textureGridView.makeItem = () =>
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row; 
                row.style.justifyContent = Justify.SpaceAround; 
                row.style.paddingLeft = 2; 
                row.style.paddingRight = 2;
                
                for (int i = 0; i < GRID_COLUMNS; i++)
                {
                    var card = CreateTextureCardTemplate(); 
                    card.name = $"Slot_{i}"; 
                    row.Add(card);
                }
                return row;
            };

            _textureGridView.bindItem = (element, index) =>
            {
                var rowsData = _textureGridView.itemsSource as List<List<string>>;
                if (rowsData == null || index >= rowsData.Count) return;

                List<string> rowPaths = rowsData[index];

                for (int i = 0; i < GRID_COLUMNS; i++)
                {
                    var card = element.Q($"Slot_{i}");
                    
                    if (i < rowPaths.Count)
                    {
                        string path = rowPaths[i];
                        card.style.visibility = Visibility.Visible; 
                        card.style.display = DisplayStyle.Flex; 
                        BindTextureCardData(card, path); 
                    }
                    else
                    {
                        card.style.visibility = Visibility.Hidden; 
                    }
                }
            };

            _batchTextureGallery.Clear();
            _batchTextureGallery.Add(_textureGridView);
        }

        private VisualElement CreateTextureCardTemplate()
        {
            var item = new VisualElement();
            item.style.width = 88;  
            item.style.height = 110; 
            item.style.marginBottom = 5;
            item.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
            item.style.borderTopWidth = 1; item.style.borderBottomWidth = 1;
            item.style.borderLeftWidth = 1; item.style.borderRightWidth = 1;
            item.style.borderTopColor = new Color(0,0,0,0.5f); item.style.borderBottomColor = new Color(0,0,0,0.5f);
            item.style.borderLeftColor = new Color(0,0,0,0.5f); item.style.borderRightColor = new Color(0,0,0,0.5f);
            item.style.borderTopLeftRadius = 4; item.style.borderTopRightRadius = 4;
            item.style.borderBottomLeftRadius = 4; item.style.borderBottomRightRadius = 4;

            var img = new Image();
            img.name = "Thumb";
            img.style.width = Length.Percent(100);
            img.style.height = 88; 
            img.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f);
            img.scaleMode = ScaleMode.ScaleToFit;
            item.Add(img);

            var lbl = new Label();
            lbl.name = "Name";
            lbl.style.height = 22;
            lbl.style.fontSize = 9; 
            lbl.style.color = new Color(0.9f, 0.9f, 0.9f);
            lbl.style.unityTextAlign = TextAnchor.MiddleCenter;
            lbl.style.overflow = Overflow.Hidden;
            lbl.style.whiteSpace = WhiteSpace.NoWrap;
            lbl.style.backgroundColor = new Color(0, 0, 0, 0.4f);
            item.Add(lbl);

            return item;
        }

        private void BindTextureCardData(VisualElement card, string path)
        {
            var img = card.Q<Image>("Thumb");
            var lbl = card.Q<Label>("Name");
            string fileName = Path.GetFileName(path);

            lbl.text = fileName;
            img.image = null; 
            
            StartCoroutine(LoadImageToElement(path, img));

            card.UnregisterCallback<ClickEvent>(OnTextureCardClicked);
            card.userData = path; 
            card.RegisterCallback<ClickEvent>(OnTextureCardClicked);
        }

        private void OnTextureCardClicked(ClickEvent evt)
        {
            var card = evt.currentTarget as VisualElement;
            string path = card.userData as string;
            if (string.IsNullOrEmpty(path)) return;

            string fileName = Path.GetFileName(path);

            if (_activeSlotSetter != null && _activeSlotTask != null)
            {
                Debug.Log($"[Batch] Assigned {fileName} to slot.");
                _activeSlotSetter.Invoke(path);
                
                var targetTask = _activeSlotTask; 
                RenderTaskSlots(targetTask);
                RefreshPreviewMaterials(targetTask);
            }
            else
            {
                Debug.Log("[Batch] Texture clicked (No slot selected).");
            }
        }

        private void RefreshBatchTextureGallery()
        {
            List<string> filteredPaths = new List<string>();
            string filter = _batchTextureSearch.value;
            bool hasFilter = !string.IsNullOrEmpty(filter);

            foreach (var path in _foundTextureFiles)
            {
                if (hasFilter)
                {
                    string fileName = Path.GetFileName(path);
                    if (!fileName.ToLower().Contains(filter.ToLower())) continue;
                }
                filteredPaths.Add(path);
            }

            List<List<string>> gridData = new List<List<string>>();
            for (int i = 0; i < filteredPaths.Count; i += GRID_COLUMNS)
            {
                int count = Mathf.Min(GRID_COLUMNS, filteredPaths.Count - i);
                List<string> row = filteredPaths.GetRange(i, count);
                gridData.Add(row);
            }

            if (_textureGridView != null)
            {
                _textureGridView.itemsSource = gridData;
                _textureGridView.Rebuild(); 
            }
        }

        // ==================================================================================
        // [修复1] 文件夹扫描与数据逻辑 - 包含重置
        // ==================================================================================

        private void OnBatchSelectFolderClicked()  
        {  
            var paths = StandaloneFileBrowser.OpenFolderPanel("Select Folder with Models & Textures", "", false);  
            if (paths != null && paths.Count > 0 && paths[0] != null)  
            {  
                string selectedPath = paths[0].Name;  
                if (!string.IsNullOrEmpty(selectedPath))  
                {  
                    StartCoroutine(ScanFolderRoutine(selectedPath));  
                }  
                else  
                {  
                    _batchStatusLabel.text = "Error: Path is empty.";  
                }  
            }  
        }  

        // [新增] 清空/重置批量导入界面的所有状态
        private void ResetBatchImportUI()
        {
            _batchTasks.Clear();
            _foundTextureFiles.Clear();
            _currentEditingTask = null;
            _activeSlotUI = null;
            _activeSlotSetter = null;
            _activeSlotTask = null;

            _batchModelList.Clear();
            _batchSlotContainer.Clear();
            if (_textureGridView != null) _textureGridView.itemsSource = new List<List<string>>();
            
            if (PhotoStudio != null) PhotoStudio.ClearPreviewModel();
            if (_batchPreviewBox != null) _batchPreviewBox.style.display = DisplayStyle.None;
            
            _batchStatusLabel.text = "Waiting for folder...";
        }

private IEnumerator ScanFolderRoutine(string rootPath)
{
    if (File.Exists(rootPath) && !Directory.Exists(rootPath))
        rootPath = Path.GetDirectoryName(rootPath);

    ResetBatchImportUI();
    _batchStatusLabel.text = "Scanning files...";
    
    // 获取当前模式: 0=Model, 1=Texture, 2=Sound
    int mode = _batchModeSelector != null ? _batchModeSelector.value : 0;

    yield return null; 

    if (Directory.Exists(rootPath))
    {
        string[] allFiles = new string[0];
        try
        {
            allFiles = Directory.GetFiles(rootPath, "*.*", SearchOption.AllDirectories);
        }
        catch { /* 忽略权限错误 */ }
        
        foreach (var file in allFiles)
        {
            string ext = Path.GetExtension(file).ToLower();
            bool isMatch = false;

            // === 根据模式筛选文件 ===
            if (mode == 0) // Models
            {
                if (ext == ".fbx" || ext == ".obj" || ext == ".blend") isMatch = true;
                // 在 Model 模式下，我们同时也收集贴图用于材质匹配
                if (IsTextureFile(ext)) _foundTextureFiles.Add(file);
            }
            else if (mode == 1) // Textures
            {
                if (IsTextureFile(ext)) isMatch = true;
            }
            else if (mode == 2) // Sounds
            {
                if (ext == ".wav" || ext == ".mp3" || ext == ".ogg") isMatch = true;
            }

            // 如果匹配主类型，添加到任务列表
            if (isMatch)
            {
                var task = new ImportTask()
                {
                    SourceFilePath = file,
                    FileName = Path.GetFileNameWithoutExtension(file),
                    TargetName = Path.GetFileNameWithoutExtension(file),
                    IsSelected = true
                };
                _batchTasks.Add(task);
            }
        }
    }
    else
    {
        _batchStatusLabel.text = "Error: Directory not found.";
    }

    string typeName = mode == 0 ? "models" : (mode == 1 ? "textures" : "sounds");
    _batchStatusLabel.text = $"Found {_batchTasks.Count} {typeName}.";
    
    RefreshBatchModelList();
    
    // 如果是 Model 模式，才刷新右侧贴图库
    if (mode == 0) RefreshBatchTextureGallery(); 
}

// 辅助函数
private bool IsTextureFile(string ext)
{
    return ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".tga" || ext == ".tif" || ext == ".tiff" || ext == ".psd" || ext == ".bmp";
}


        // ==================================================================================
        // [修复2] 模型列表与选中逻辑 (修复 Checkbox 穿透问题)
        // ==================================================================================

        private void RefreshBatchModelList()
        {
            _batchModelList.Clear();
            foreach (var task in _batchTasks)
            {
                var item = new VisualElement();
                item.style.flexDirection = FlexDirection.Row;
                item.style.paddingLeft = 5; item.style.paddingRight = 5;
                item.style.paddingTop = 5; item.style.paddingBottom = 5;
                item.style.borderBottomWidth = 1;
                item.style.borderBottomColor = new Color(1,1,1,0.1f);
                item.style.alignItems = Align.Center; // 垂直居中

                var toggle = new Toggle();
                toggle.value = task.IsSelected;
                
                // [核心修复] 阻止点击事件向上传递到 item
                toggle.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
                toggle.RegisterValueChangedCallback(evt => task.IsSelected = evt.newValue);
                
                var label = new Label(task.FileName);
                label.style.marginLeft = 5;
                label.style.color = Color.white;
                // [优化] 字体稍微调大一点点
                label.style.fontSize = 12; 
                
                // 点击整行 -> 编辑模型
                item.RegisterCallback<ClickEvent>(evt => SelectTaskForEditing(task));

                item.Add(toggle);
                item.Add(label);
                _batchModelList.Add(item);
            }
        }

        private void SelectTaskForEditing(ImportTask task)  
        {  
            _currentEditingTask = task;  

            if (_batchPreviewBox != null)  
            {  
                _batchPreviewBox.style.display = DisplayStyle.Flex;  
                if (PhotoStudio != null)  
                {  
                    PhotoStudio.ResizeRenderTexture(512, 512);   
                    if (_batchPreviewImage != null) _batchPreviewImage.image = PhotoStudio.GetRenderTexture();  
                }  
            }  

            StartCoroutine(LoadAndScanBatchModel(task));  
        }  

        private IEnumerator ScanTaskMaterials(ImportTask task)
        {
            var options = AssetLoader.CreateDefaultLoaderOptions();
            bool done = false;

            AssetLoader.LoadModelFromFile(task.SourceFilePath,
                onLoad: (ctx) => 
                { 
                    var loadedGo = ctx.RootGameObject;
                    if (loadedGo != null)
                    {
                        var renderers = loadedGo.GetComponentsInChildren<Renderer>();
                        var matNames = new HashSet<string>();
                        foreach (var r in renderers)
                        {
                            foreach (var m in r.sharedMaterials) if (m != null) matNames.Add(m.name);
                        }

                        task.Slots.Clear();
                        foreach (var name in matNames)
                        {
                            task.Slots.Add(new MaterialSlotConfig { SlotName = name });
                        }
                        task.IsScanned = true;
                        Destroy(loadedGo); 
                    }
                    done = true; 
                },
                onMaterialsLoad: (ctx) => {},
                onProgress: (ctx, p) => {},
                onError: (e) => { done = true; Debug.LogError(e); },
                wrapperGameObject: null, assetLoaderOptions: options
            );

            while (!done) yield return null;
        }

        private IEnumerator LoadAndScanBatchModel(ImportTask task)
        {
            _batchSlotContainer.Clear();
            var loadingLabel = new Label($"Loading {task.FileName}...") { 
                style = { color = new Color(1, 0.8f, 0.2f), fontSize = 14, unityFontStyleAndWeight = FontStyle.Bold } 
            };
            _batchSlotContainer.Add(loadingLabel);

            var options = AssetLoader.CreateDefaultLoaderOptions();
            GameObject loadedGo = null;
            bool done = false;

            AssetLoader.LoadModelFromFile(task.SourceFilePath,
                onLoad: (ctx) => { loadedGo = ctx.RootGameObject; done = true; },
                onMaterialsLoad: (ctx) => {},
                onProgress: (ctx, p) => {},
                onError: (e) => { done = true; Debug.LogError(e); },
                wrapperGameObject: null, assetLoaderOptions: options
            );

            while (!done) yield return null;

            if (loadedGo != null)
            {
                if (PhotoStudio != null) PhotoStudio.LoadPreviewModel(loadedGo);
                else Destroy(loadedGo);

                if (!task.IsScanned || task.Slots.Count == 0)
                {
                    var renderers = loadedGo.GetComponentsInChildren<Renderer>();
                    var matNames = new HashSet<string>();
                    foreach (var r in renderers)
                    {
                        foreach (var m in r.sharedMaterials) if (m != null) matNames.Add(m.name);
                    }

                    task.Slots.Clear();
                    foreach (var name in matNames)
                    {
                        task.Slots.Add(new MaterialSlotConfig { SlotName = name });
                    }
                    task.IsScanned = true;
                    
                    foreach(var slot in task.Slots) 
                        SmartMaterialMatcher.AutoMatch(task.FileName, _foundTextureFiles, slot);
                }

                RefreshPreviewMaterials(task);
                RenderTaskSlots(task);
            }
            else
            {
                _batchSlotContainer.Clear();
                _batchSlotContainer.Add(new Label("Failed to load model."){ style = { color = Color.red } });
            }
        }

        private void RefreshPreviewMaterials(ImportTask task)
        {
            if (PhotoStudio == null || !PhotoStudio.HasModel()) return;

            AssetMetaData tempMeta = new AssetMetaData();
            foreach(var slot in task.Slots)
            {
                var setting = new AssetMaterialSetting();
                setting.MaterialName = slot.SlotName;
                
                setting.Bindings.Workflow = slot.Workflow;
                setting.Bindings.BaseMapPath = slot.BaseMapPath;
                setting.Bindings.NormalMapPath = slot.NormalMapPath;
                setting.Bindings.MetallicMapPath = slot.MetallicMapPath;
                setting.Bindings.SmoothnessMapPath = slot.SmoothnessMapPath;
                setting.Bindings.OcclusionMapPath = slot.OcclusionMapPath;
                setting.Bindings.EmissionMapPath = slot.EmissionMapPath;
                
                tempMeta.MultiMaterials.Add(setting);
            }
            
            PhotoStudio.ApplyMaterialsToPreview(tempMeta, "", this);
        }

        // ==================================================================================
        // [修复3] 中间栏渲染 (Slots) - 增大字号
        // ==================================================================================

        private void RenderTaskSlots(ImportTask task)
        {
            _batchSlotContainer.Clear();
            
            _activeSlotUI = null;
            _activeSlotSetter = null;
            _activeSlotTask = null;

            var header = new Label($"Editing: {task.FileName}");
            header.style.fontSize = 18; // [增大]
            header.style.marginBottom = 5;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.color = new Color(0.9f, 0.9f, 0.9f);
            _batchSlotContainer.Add(header);
            
            var helpInfo = new Label("Select a material slot below, then click a texture on the right.");
            helpInfo.style.fontSize = 11; // [增大]
            helpInfo.style.color = Color.gray;
            helpInfo.style.marginBottom = 10;
            helpInfo.style.whiteSpace = WhiteSpace.Normal;
            _batchSlotContainer.Add(helpInfo);

            var subHeader = new Label($"Materials Found: {task.Slots.Count}");
            subHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            subHeader.style.fontSize = 14; // [增大]
            subHeader.style.marginBottom = 5;
            subHeader.style.borderBottomWidth = 1;
            subHeader.style.borderBottomColor = new Color(1,1,1,0.1f);
            _batchSlotContainer.Add(subHeader);

            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            _batchSlotContainer.Add(scrollView);

            if (task.Slots.Count == 0)
            {
                var noMatLabel = new Label("No materials found on this model.");
                noMatLabel.style.color = new Color(1, 0.5f, 0.5f);
                noMatLabel.style.marginTop = 10;
                scrollView.Add(noMatLabel);
                return;
            }

            foreach (var slot in task.Slots)
            {
                var box = new VisualElement();
                box.style.marginTop = 10;
                box.style.backgroundColor = new Color(0, 0, 0, 0.2f);
                box.style.paddingLeft = 5; box.style.paddingRight = 5;
                box.style.paddingTop = 5; box.style.paddingBottom = 5;

                box.style.borderTopWidth = 1; box.style.borderBottomWidth = 1; 
                box.style.borderLeftWidth = 1; box.style.borderRightWidth = 1;
                Color borderColor = new Color(1, 1, 1, 0.1f);
                box.style.borderTopColor = borderColor; box.style.borderBottomColor = borderColor;
                box.style.borderLeftColor = borderColor; box.style.borderRightColor = borderColor;

                var matName = new Label($"Mat: {slot.SlotName}");
                matName.style.color = new Color(0.3f, 0.8f, 1f);
                matName.style.fontSize = 14; // [增大]
                matName.style.marginBottom = 5;
                matName.style.unityFontStyleAndWeight = FontStyle.Bold;
                box.Add(matName);

                CreateSlotRow(box, "Albedo", slot.BaseMapPath, task, (val) => slot.BaseMapPath = val);
                CreateSlotRow(box, "Normal", slot.NormalMapPath, task, (val) => slot.NormalMapPath = val);
                CreateSlotRow(box, "Metallic", slot.MetallicMapPath, task, (val) => slot.MetallicMapPath = val);
                CreateSlotRow(box, "Smoothness", slot.SmoothnessMapPath, task, (val) => slot.SmoothnessMapPath = val);
                CreateSlotRow(box, "Occlusion", slot.OcclusionMapPath, task, (val) => slot.OcclusionMapPath = val);
                CreateSlotRow(box, "Emission", slot.EmissionMapPath, task, (val) => slot.EmissionMapPath = val);

                scrollView.Add(box);
            }
        }

        private void CreateSlotRow(VisualElement parent, string label, string currentVal, ImportTask task, System.Action<string> setter)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginTop = 2;
            row.style.paddingLeft = 4;
            row.style.height = 28; // [增大] 行高从 22 -> 28，方便点击
            row.style.alignItems = Align.Center;

            row.style.borderTopWidth = 1; row.style.borderBottomWidth = 1;
            row.style.borderLeftWidth = 1; row.style.borderRightWidth = 1;
            row.style.borderTopColor = Color.clear; row.style.borderBottomColor = Color.clear;
            row.style.borderLeftColor = Color.clear; row.style.borderRightColor = Color.clear;
            row.style.borderTopLeftRadius = 3; row.style.borderTopRightRadius = 3;
            row.style.borderBottomLeftRadius = 3; row.style.borderBottomRightRadius = 3;

            // [增大] 字体 10 -> 12
            var lbl = new Label(label) { style = { width = 80, fontSize = 12, color = new Color(0.7f, 0.7f, 0.7f) } };
            
            string displayVal = string.IsNullOrEmpty(currentVal) ? "-- Empty --" : Path.GetFileName(currentVal);
            var val = new Label(displayVal);
            val.style.color = string.IsNullOrEmpty(currentVal) ? new Color(0.5f, 0.5f, 0.5f) : Color.white;
            val.style.fontSize = 12; // [增大] 字体 10 -> 12
            val.style.flexGrow = 1;
            val.style.overflow = Overflow.Hidden;

            row.Add(lbl);
            row.Add(val);

            row.RegisterCallback<ClickEvent>(evt => 
            {
                if (_activeSlotUI != null) 
                {
                    _activeSlotUI.style.backgroundColor = Color.clear;
                    _activeSlotUI.style.borderTopColor = Color.clear; _activeSlotUI.style.borderBottomColor = Color.clear;
                    _activeSlotUI.style.borderLeftColor = Color.clear; _activeSlotUI.style.borderRightColor = Color.clear;
                }

                _activeSlotUI = row;
                _activeSlotSetter = setter;
                _activeSlotTask = task;

                row.style.backgroundColor = new Color(1, 1, 1, 0.05f);
                Color highlightColor = new Color(0.2f, 0.6f, 1f);
                row.style.borderTopColor = highlightColor; row.style.borderBottomColor = highlightColor;
                row.style.borderLeftColor = highlightColor; row.style.borderRightColor = highlightColor;
            });

            parent.Add(row);
        }

        // ==================================================================================
        // 自动匹配与执行导入
        // ==================================================================================

        private void RunBatchAutoMatch()
        {
            StartCoroutine(AutoMatchAllRoutine());
        }

        private IEnumerator AutoMatchAllRoutine()
        {
            _batchStatusLabel.text = "Auto matching...";
            foreach (var task in _batchTasks)
            {
                if (!task.IsSelected) continue;
                
                if (!task.IsScanned) yield return StartCoroutine(ScanTaskMaterials(task));
                
                foreach(var slot in task.Slots)
                {
                    SmartMaterialMatcher.AutoMatch(task.FileName, _foundTextureFiles, slot);
                }
            }
            _batchStatusLabel.text = "Match complete.";
            if (_currentEditingTask != null) RenderTaskSlots(_currentEditingTask);
        }

        private void StartBatchImportProcess()
        {
            if (_batchTasks.Count == 0) return;
            StartCoroutine(BatchImportRoutine());
        }

        private IEnumerator BatchImportRoutine()
        {
            _btnBatchProcess.SetEnabled(false);
            if (_batchPreviewBox != null) _batchPreviewBox.style.visibility = Visibility.Hidden;

            string libRoot = LibraryManager.Instance.LibraryRoot;
            
            // 获取当前模式: 0=Model, 1=Texture, 2=Sound
            int mode = _batchModeSelector != null ? _batchModeSelector.value : 0;

            // 1. 确定目标根目录 (Models, Textures, Sounds)
            string category = mode == 0 ? "Models" : (mode == 1 ? "Textures" : "Sounds");
            string categoryRoot = Path.Combine(libRoot, category);
            if (!Directory.Exists(categoryRoot)) Directory.CreateDirectory(categoryRoot);

            var selectedTasks = _batchTasks.Where(t => t.IsSelected).ToList();
            int total = selectedTasks.Count;
            int current = 0;
            int successCount = 0;

            foreach (var task in selectedTasks)
            {
                current++;
                _batchStatusLabel.text = $"Processing {current}/{total}: {task.FileName} ...";
                // 暂停一帧以刷新 UI 文字
                yield return null;

                string safeAssetName = task.TargetName;
                string ext = Path.GetExtension(task.SourceFilePath);

                // ==============================================================================
                // 逻辑分支 A: 模型导入 (Mode 0) - 复杂流程 (创建文件夹、处理材质)
                // ==============================================================================
                if (mode == 0)
                {
                    // A1. 创建资产专用文件夹 e.g. Library/Models/Hero_01/
                    string assetFolder = Path.Combine(categoryRoot, safeAssetName);
                    int counter = 1;
                    while (Directory.Exists(assetFolder))
                    {
                        safeAssetName = $"{task.TargetName}_{counter}";
                        assetFolder = Path.Combine(categoryRoot, safeAssetName);
                        counter++;
                    }
                    Directory.CreateDirectory(assetFolder);
                    
                    // A2. 创建贴图隐藏文件夹
                    string texFolder = Path.Combine(assetFolder, ".Textures");
                    Directory.CreateDirectory(texFolder);

                    // A3. 复制模型主文件
                    string destModelPath = Path.Combine(assetFolder, safeAssetName + ext);
                    File.Copy(task.SourceFilePath, destModelPath);

                    // A4. 初始化 Meta
                    AssetMetaData meta = new AssetMetaData();
                    meta.Name = safeAssetName;
                    meta.Type = AssetType.Model;
                    meta.RelativePath = PathUtility.GetRelativePath(destModelPath, libRoot);
                    meta.MultiMaterials = new List<AssetMaterialSetting>();

                    // 辅助函数：处理贴图复制与重命名
                    string ProcessTexture(string srcPath, string typeSuffix, string matName)
                    {
                        if (string.IsNullOrEmpty(srcPath) || !File.Exists(srcPath)) return "";
                        string originalExt = Path.GetExtension(srcPath);
                        string newFileName = $"{safeAssetName}_{matName}_{typeSuffix}{originalExt}";
                        // 移除非法字符
                        foreach (char c in Path.GetInvalidFileNameChars()) newFileName = newFileName.Replace(c, '_');
                        
                        string destPath = Path.Combine(texFolder, newFileName);
                        if(!File.Exists(destPath)) File.Copy(srcPath, destPath, true);
                        return PathUtility.GetRelativePath(destPath, libRoot);
                    }

                    // A5. 遍历并处理材质槽 (包含通道合并逻辑)
                    foreach (var slot in task.Slots)
                    {
                        AssetMaterialSetting setting = new AssetMaterialSetting();
                        setting.MaterialName = slot.SlotName;
                        setting.Bindings.Workflow = slot.Workflow;

                        // 处理常规贴图
                        setting.Bindings.BaseMapPath = ProcessTexture(slot.BaseMapPath, "Base", slot.SlotName);
                        setting.Bindings.NormalMapPath = ProcessTexture(slot.NormalMapPath, "Normal", slot.SlotName);
                        setting.Bindings.OcclusionMapPath = ProcessTexture(slot.OcclusionMapPath, "AO", slot.SlotName);
                        setting.Bindings.EmissionMapPath = ProcessTexture(slot.EmissionMapPath, "Emit", slot.SlotName);
                        
                        // 处理金属度/光滑度通道合并
                        string metalPath = slot.MetallicMapPath;
                        string smoothPath = slot.SmoothnessMapPath;
                        
                        bool needMerge = !string.IsNullOrEmpty(metalPath) && File.Exists(metalPath) &&
                                        !string.IsNullOrEmpty(smoothPath) && File.Exists(smoothPath);

                        if (needMerge)
                        {
                            string suffix = (slot.Workflow == WorkflowMode.Metallic) ? "MetalSmooth" : "SpecSmooth";
                            string newFileName = $"{safeAssetName}_{slot.SlotName}_{suffix}.tga";
                            foreach (char c in Path.GetInvalidFileNameChars()) newFileName = newFileName.Replace(c, '_');
                            
                            string mergedDestPath = Path.Combine(texFolder, newFileName);

                            yield return null; // 合并可能耗时，让出主线程一帧
                            
                            TextureUtility.MergeChannelsToTGA(metalPath, smoothPath, mergedDestPath);

                            setting.Bindings.MetallicMapPath = PathUtility.GetRelativePath(mergedDestPath, libRoot);
                            setting.Bindings.SmoothnessMapPath = ""; // 合并后不需要单独的光滑度路径
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(metalPath))
                                setting.Bindings.MetallicMapPath = ProcessTexture(metalPath, "Metal", slot.SlotName);
                            
                            if (!string.IsNullOrEmpty(smoothPath))
                                setting.Bindings.SmoothnessMapPath = ProcessTexture(smoothPath, "Smooth", slot.SlotName);
                        }

                        meta.MultiMaterials.Add(setting);
                    }

                    // A6. 生成 3D 缩略图
                    if (PhotoStudio != null)
                    {
                        _batchStatusLabel.text = $"Generating thumbnail: {task.FileName} ...";
                        
                        GameObject tempGo = null;
                        bool loadDone = false;
                        var loadOptions = AssetLoader.CreateDefaultLoaderOptions();
                        // 临时加载模型用于拍照
                        AssetLoader.LoadModelFromFile(task.SourceFilePath, 
                            onLoad: ctx => { tempGo = ctx.RootGameObject; loadDone = true; },
                            onMaterialsLoad: ctx => {},
                            onProgress: (ctx, p) => {},
                            onError: ctx => { loadDone = true; },
                            wrapperGameObject: null, assetLoaderOptions: loadOptions
                        );

                        while (!loadDone) yield return null;

                        if (tempGo != null)
                        {
                            PhotoStudio.LoadPreviewModel(tempGo);

                            // 应用材质
                            bool matApplyDone = false;
                            PhotoStudio.ApplyMaterialsToPreview(meta, libRoot, this, null, (p) => 
                            {
                                if(p >= 0.99f) matApplyDone = true;
                            });
                            
                            float timeout = 0;
                            while(!matApplyDone && timeout < 2.0f) { timeout += Time.deltaTime; yield return null; }

                            string thumbName = safeAssetName + ".thumb.jpg";
                            string thumbPath = Path.Combine(assetFolder, thumbName);
                            
                            bool snapDone = false;
                            yield return StartCoroutine(PhotoStudio.CaptureSnapshotAsync(tempGo, thumbPath, () => snapDone = true));
                            while(!snapDone) yield return null;

                            meta.ThumbnailPath = PathUtility.GetRelativePath(thumbPath, libRoot);
                        }
                    }

                    // A7. 保存 Meta
                    string metaPath = Path.Combine(assetFolder, safeAssetName + ".meta.json");
                    File.WriteAllText(metaPath, JsonUtilityEx.ToJson(meta, true));
                }
                // ==============================================================================
                // 逻辑分支 B: 贴图或音频导入 (Mode 1, 2) - 简单流程 (直接复制)
                // ==============================================================================
                else
                {
                    // B1. 处理文件名冲突 (直接在 Category 根目录下)
                    string destPath = Path.Combine(categoryRoot, safeAssetName + ext);
                    int counter = 1;
                    while (File.Exists(destPath))
                    {
                        safeAssetName = $"{task.TargetName}_{counter}";
                        destPath = Path.Combine(categoryRoot, safeAssetName + ext);
                        counter++;
                    }

                    // B2. 复制文件
                    File.Copy(task.SourceFilePath, destPath);

                    // B3. 初始化 Meta
                    AssetMetaData meta = new AssetMetaData();
                    meta.Name = safeAssetName;
                    meta.Type = mode == 1 ? AssetType.Texture : AssetType.Sound;
                    meta.RelativePath = PathUtility.GetRelativePath(destPath, libRoot);
                    
                    // B4. 如果是贴图，生成 2D 缩略图
                    if (mode == 1)
                    {
                        string thumbName = safeAssetName + ".thumb.jpg";
                        string thumbPath = Path.Combine(categoryRoot, thumbName);
                        ThumbnailGenerator.GenerateThumbnail(destPath, thumbPath);
                        meta.ThumbnailPath = PathUtility.GetRelativePath(thumbPath, libRoot);
                    }
                    // 音频暂时不需要缩略图，UI 会显示默认图标

                    // B5. 保存 Meta
                    string metaPath = Path.Combine(categoryRoot, safeAssetName + ".meta.json");
                    File.WriteAllText(metaPath, JsonUtilityEx.ToJson(meta, true));
                }

                successCount++;
            }

            // === 结束处理 ===
            _batchStatusLabel.text = $"Batch Finished! Imported {successCount} assets.";
            _btnBatchProcess.SetEnabled(true);
            
            // 恢复预览框
            if (_batchPreviewBox != null) 
            {
                _batchPreviewBox.style.visibility = Visibility.Visible;
                if(PhotoStudio != null) PhotoStudio.ClearPreviewModel();
            }

            // 刷新库
            LibraryManager.Instance.ScanLibrary();
            RefreshList();
            
            // 清空界面状态
            ResetBatchImportUI();

            // 延迟一秒后自动关闭面板
            yield return new WaitForSeconds(1.5f);
            _batchPanel.style.display = DisplayStyle.None;
        }

    }
}
