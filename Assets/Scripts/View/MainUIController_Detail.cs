using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Networking;
using AssetLibrary.Core;
using AssetLibrary.Utilities;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using TriLibCore;

namespace AssetLibrary.View
{
    public partial class MainUIController
    {
        // ==================================================================================
        // 1. 详情页入口与生命周期
        // ==================================================================================
        
        private void ShowDetailForAsset(AssetMetaData asset)  
        {  
            _selectedAsset = asset; 
            _viewContainer.style.display = DisplayStyle.Flex;  
            _editContainer.style.display = DisplayStyle.None;  

            // 填充基础文本信息
            _detailName.text = asset.Name;  
            _detailType.text = $"Type: {asset.Type}";  
            _detailPath.text = asset.RelativePath;  
            string tags = (asset.Tags != null && asset.Tags.Count > 0) ? string.Join(", ", asset.Tags) : "None";  
            _detailTags.text = $"Tags: {tags}";  
            _detailDesc.text = string.IsNullOrEmpty(asset.Description) ? "No description provided." : asset.Description;  

            // 重置预览图变换
            _detailImageScale = Vector3.one;
            _detailImagePos = Vector3.zero;
            _detailImage.transform.scale = _detailImageScale;
            _detailImage.transform.position = _detailImagePos;

            string libRoot = LibraryManager.Instance.LibraryRoot;
            string fullPath = Path.Combine(libRoot, asset.RelativePath);

            // 隐藏 Tex/Clay 切换按钮 (默认)
            if (_viewModeSwitch != null) _viewModeSwitch.style.display = DisplayStyle.None;

            // --- 根据类型加载内容 ---
            if (asset.Type == AssetType.Model)
            {
                // 显示切换按钮
                if (_viewModeSwitch != null) _viewModeSwitch.style.display = DisplayStyle.Flex; 
                
                if (PhotoStudio != null)
                {
                    PhotoStudio.ResizeRenderTexture(960, 540);
                    _detailImage.image = PhotoStudio.GetRenderTexture();
                    _detailImage.scaleMode = ScaleMode.ScaleToFit;
                    
                    // [核心优化] 判断是否是同一个资源，决定是否重置状态
                    // 如果是从全屏返回，或者是同一个模型，保持当前的 _isTexturedMode 和模型实例
                    if (_lastLoadedModelPath == fullPath && PhotoStudio.HasModel())
                    {
                        // Do nothing, keep current model
                    }
                    else
                    {
                        // 加载新资源，重置为默认 Textured 状态
                        _isTexturedMode = true;
                        _lastLoadedModelPath = fullPath;
                        StartCoroutine(LoadModelForDetailView(fullPath, asset)); 
                    }
                }
                if(_detailHintLabel != null) _detailHintLabel.text = "LMB: Rotate | Scroll: Zoom";
            }
            else if (asset.Type == AssetType.Texture)
            {
                if (File.Exists(fullPath)) StartCoroutine(LoadImageToElement(fullPath, _detailImage));
                else _detailImage.image = DefaultIcon;
                
                if(_detailHintLabel != null) _detailHintLabel.text = "LMB: Pan | Scroll: Zoom";
            }
            else
            {
                // 其他类型显示缩略图
                string thumbPath = Path.Combine(libRoot, asset.ThumbnailPath);
                if (File.Exists(thumbPath)) StartCoroutine(LoadImageToElement(thumbPath, _detailImage));
                else _detailImage.image = DefaultIcon;
                
                if(_detailHintLabel != null) _detailHintLabel.text = "";
            }

            _detailPanel.style.display = DisplayStyle.Flex;  
            
            // [关键] 根据当前记录的状态刷新按钮样式
            UpdateViewModeButtons(_isTexturedMode);
        } 

        // 详情图点击事件 -> 进入全屏
        private void OnDetailImageClicked(ClickEvent evt)
        {
            if (_selectedAsset == null) return;
            if (_selectedAsset.Type == AssetType.Texture) OpenImageViewer();
            else if (_selectedAsset.Type == AssetType.Model) OpenModelViewer();
        }

        private void OnDetailFullscreenClicked()
        {
            if (_selectedAsset == null) return;
            if (_selectedAsset.Type == AssetType.Model) OpenModelViewer();
            else if (_selectedAsset.Type == AssetType.Texture) OpenImageViewer();
        }

        // ==================================================================================
        // 2. 模型加载与视图控制 (Model View Logic)
        // ==================================================================================

        // 更新进度条 UI (定义在 Partial 中，供所有部分使用)
        private void UpdateLoadingProgress(float progress, string message)  
        {  
            if (_loadingOverlay == null) return;  
            if (_loadingFill != null) _loadingFill.style.width = new Length(progress * 100, LengthUnit.Percent);  
            if (_loadingLabel != null) _loadingLabel.text = $"{message} {Mathf.RoundToInt(progress * 100)}%";  
        } 

        // 加载模型到详情页预览
        private IEnumerator LoadModelForDetailView(string path, AssetMetaData assetData)  
        {  
            if (!File.Exists(path)) yield break;  
            if (PhotoStudio == null) yield break;  

            if (_loadingOverlay != null) _loadingOverlay.style.display = DisplayStyle.Flex;  
            UpdateLoadingProgress(0f, "Loading Geometry...");  

            PhotoStudio.ClearPreviewModel();  

            var options = AssetLoader.CreateDefaultLoaderOptions(true);  
            GameObject loadedGo = null;  
            bool isLoading = true;  

            AssetLoader.LoadModelFromFile(path,   
                onLoad: (ctx) => { loadedGo = ctx.RootGameObject; isLoading = false; },  
                onMaterialsLoad: (ctx) => { },  
                onProgress: (ctx, progress) => { UpdateLoadingProgress(progress * 0.6f, "Loading Geometry..."); },  
                onError: (e) => { Debug.LogError(e); isLoading = false; if (_loadingOverlay != null) _loadingOverlay.style.display = DisplayStyle.None; },  
                wrapperGameObject: null,  
                assetLoaderOptions: options  
            );  

            while (isLoading) yield return null;  

            if (loadedGo != null)  
            {  
                UpdateLoadingProgress(0.6f, "Loading Textures...");  
                PhotoStudio.LoadPreviewModel(loadedGo);  
                
                if (assetData != null)  
                {  
                    string libRoot = LibraryManager.Instance.LibraryRoot;  
                    bool texturesDone = false;  

                    // [修改]直接传递 assetData 整个对象  
                    PhotoStudio.ApplyMaterialsToPreview(assetData, libRoot, this, null, (matProgress) => {  
                        float totalProgress = 0.6f + (matProgress * 0.4f);  
                        UpdateLoadingProgress(totalProgress, "Loading Textures...");  
                        if (matProgress >= 0.99f) texturesDone = true;  
                    });  
                    while(!texturesDone) yield return null;  
                }  
            }  

            UpdateLoadingProgress(1.0f, "Done!");  
            yield return new WaitForSeconds(0.1f);  
            if (_loadingOverlay != null) _loadingOverlay.style.display = DisplayStyle.None;  
        }  

        // 切换 Tex/Clay 模式
        private void SetDetailViewMode(bool isTextured)  
        {  
            _isTexturedMode = isTextured; // 更新全局状态
            UpdateViewModeButtons(isTextured);  // 刷新按钮 UI

            if (_selectedAsset == null || _selectedAsset.Type != AssetType.Model || PhotoStudio == null) return;  

            if (isTextured)  
            {  
                // 尝试切回贴图模式，如果缓存失效则重新加载
                bool success = PhotoStudio.ShowTexturedMode();  
                if (!success)  
                {  
                    StartCoroutine(ReloadTexturesWithProgress());  
                }  
            }  
            else  
            {  
                PhotoStudio.ShowClayMode();  
            }  
        }  

        private IEnumerator ReloadTexturesWithProgress()  
        {  
            if (_loadingOverlay != null) _loadingOverlay.style.display = DisplayStyle.Flex;  
            UpdateLoadingProgress(0f, "Restoring Textures...");  
            string libRoot = LibraryManager.Instance.LibraryRoot;  
            bool done = false;  
            
            PhotoStudio.ApplyMaterialsToPreview(_selectedAsset, libRoot, this, null, (p) => {  
                UpdateLoadingProgress(p, "Restoring Textures...");  
                if (p >= 0.99f) done = true;  
            });  
            
            while(!done) yield return null;  
            yield return new WaitForSeconds(0.1f);  
            if (_loadingOverlay != null) _loadingOverlay.style.display = DisplayStyle.None;  
        } 

        // 更新按钮外观 (同时更新详情页和全屏页)
        private void UpdateViewModeButtons(bool isTextured)
        {
            var activeColor = new StyleColor(new Color(46/255f, 134/255f, 222/255f));
            var inactiveColor = new StyleColor(Color.clear);
            var activeText = new StyleColor(Color.white);
            var inactiveText = new StyleColor(new Color(180/255f, 180/255f, 180/255f));

            void ApplyStyle(Button btn, bool isActive)
            {
                if (btn == null) return;
                btn.style.backgroundColor = isActive ? activeColor : inactiveColor;
                btn.style.color = isActive ? activeText : inactiveText;
            }

            // 详情页
            ApplyStyle(_btnViewTextured, isTextured);
            ApplyStyle(_btnViewClay, !isTextured);

            // 全屏页
            ApplyStyle(_btnFullViewTextured, isTextured);
            ApplyStyle(_btnFullViewClay, !isTextured);
        }

        // ==================================================================================
        // 3. 全屏查看器 (Full Screen Viewers)
        // ==================================================================================

        // --- 3D 模型全屏 ---
        private void OpenModelViewer()  
        {  
            if (_selectedAsset == null || PhotoStudio == null) return;  
            
            _modelViewerOverlay.style.display = DisplayStyle.Flex;  

            // 调整渲染分辨率适应全屏 (保持长宽比)
            int maxRes = 1920;   
            int width = Mathf.Min(Screen.width, maxRes);  
            int height = Mathf.Min(Screen.height, (int)(maxRes * ((float)Screen.height / Screen.width)));  
            
            PhotoStudio.ResizeRenderTexture(width, height);  
            _modelFullSizeImage.image = PhotoStudio.GetRenderTexture();  
            _modelFullSizeImage.style.opacity = 1;

            // 检查是否需要重新加载模型 (防止在详情页没加载完就点了全屏)
            string fullPath = Path.Combine(LibraryManager.Instance.LibraryRoot, _selectedAsset.RelativePath);
            if (_lastLoadedModelPath != fullPath || !PhotoStudio.HasModel())
            {
                StartCoroutine(LoadModelForViewer(fullPath)); 
            }
            
            // 确保按钮状态同步
            UpdateViewModeButtons(_isTexturedMode);
        }  

        private IEnumerator LoadModelForViewer(string path)  
        {  
            if (!File.Exists(path)) yield break;  
            var options = AssetLoader.CreateDefaultLoaderOptions(true);  
            GameObject loadedGo = null;  
            bool isLoading = true;  
            
            AssetLoader.LoadModelFromFile(path, 
                onLoad: (ctx) => { loadedGo = ctx.RootGameObject; isLoading = false; }, 
                onMaterialsLoad: (ctx) => { }, 
                onProgress: (ctx, p) => { }, 
                onError: (ctxError) => { isLoading = false; }, 
                wrapperGameObject: null, assetLoaderOptions: options 
            );  
            
            while (isLoading) yield return null;  
            
            if (loadedGo != null && PhotoStudio != null)  
            {  
                PhotoStudio.LoadPreviewModel(loadedGo);  
                if (_selectedAsset.Materials != null)  
                {  
                    string libRoot = LibraryManager.Instance.LibraryRoot;  
                    List<Coroutine> tasks = new List<Coroutine>();
                    PhotoStudio.ApplyMaterialsToPreview(_selectedAsset, libRoot, this, tasks);
                    foreach(var t in tasks) yield return t;
                }  
            }  
            _modelFullSizeImage.style.opacity = 1;  
        }  

        private void CloseModelViewer()
        {
            _modelViewerOverlay.style.display = DisplayStyle.None;
            // 恢复小分辨率给详情页
            if (PhotoStudio != null) PhotoStudio.ResizeRenderTexture(960, 540);
            _modelFullSizeImage.image = null;
            
            // 如果详情页还开着，刷新一下显示
            if (_detailPanel.style.display == DisplayStyle.Flex && _selectedAsset != null) 
                ShowDetailForAsset(_selectedAsset);
        }

        // --- 2D 图片全屏 ---
        private void OpenImageViewer()
        {
            if (_selectedAsset == null || _detailImage.image == null) return;
            _fullSizeImage.image = _detailImage.image;
            ResetImageView();
            _imageViewerOverlay.style.display = DisplayStyle.Flex;
        }

        private void CloseImageViewer()
        {
            _imageViewerOverlay.style.display = DisplayStyle.None;
            _fullSizeImage.image = null;
        }

        private void ResetImageView()
        {
            _imageScale = Vector3.one;
            _imagePosition = Vector3.zero;
            ApplyImageTransform();
        }

        private void ApplyImageTransform()
        {
            _fullSizeImage.transform.scale = _imageScale;
            _fullSizeImage.transform.position = _imagePosition;
        }

        // ==================================================================================
        // 4. 交互事件处理 (Mouse & Scroll)
        // ==================================================================================

        // --- 详情页小窗口交互 ---
        private void OnDetailMouseDown(MouseDownEvent evt) { if (evt.button == 0) { _isDetailInteracting = true; _detailImage.CaptureMouse(); _lastMousePosition = evt.mousePosition; } }
        private void OnDetailMouseUp(MouseUpEvent evt) { _isDetailInteracting = false; _detailImage.ReleaseMouse(); }
        private void OnDetailMouseMove(MouseMoveEvent evt) 
        { 
            if (!_isDetailInteracting) return; 
            Vector2 delta = evt.mousePosition - _lastMousePosition; 
            _lastMousePosition = evt.mousePosition; 
            
            if (_selectedAsset.Type == AssetType.Model && PhotoStudio != null) 
                PhotoStudio.RotatePreview(delta.x * 0.5f, delta.y * 0.5f); 
            else if (_selectedAsset.Type == AssetType.Texture) 
            { 
                _detailImagePos.x += delta.x; _detailImagePos.y += delta.y; 
                _detailImage.transform.position = _detailImagePos; 
            } 
        }
        private void OnDetailWheel(WheelEvent evt) 
        { 
            if (_selectedAsset == null) return; 
            if (_selectedAsset.Type == AssetType.Model && PhotoStudio != null) 
            { 
                float zoomAmount = (evt.delta.y > 0 ? 1 : -1) * 0.1f; 
                PhotoStudio.ZoomPreview(zoomAmount); 
            } 
            else if (_selectedAsset.Type == AssetType.Texture) 
            { 
                float zoomDelta = (evt.delta.y < 0 ? 0.1f : -0.1f); 
                float newScale = Mathf.Clamp(_detailImageScale.x + zoomDelta, 0.1f, 5.0f); 
                _detailImageScale = new Vector3(newScale, newScale, 1f); 
                _detailImage.transform.scale = _detailImageScale; 
            } 
            evt.StopPropagation(); 
        }

        // --- 全屏模型交互 ---
        private void OnModelViewerMouseDown(MouseDownEvent evt) { if (evt.button == 0) { _isModelViewerDragging = true; _modelFullSizeImage.CaptureMouse(); } }
        private void OnModelViewerMouseUp(MouseUpEvent evt) { _isModelViewerDragging = false; _modelFullSizeImage.ReleaseMouse(); }
        private void OnModelViewerMouseMove(MouseMoveEvent evt) { if (_isModelViewerDragging && PhotoStudio != null) { PhotoStudio.RotatePreview(evt.mouseDelta.x * 0.5f, evt.mouseDelta.y * 0.5f); } }
        private void OnModelViewerWheel(WheelEvent evt) { if (PhotoStudio != null) { float zoomAmount = (evt.delta.y > 0 ? -1 : 1) * 0.2f; PhotoStudio.ZoomPreview(zoomAmount); evt.StopPropagation(); } }

        // --- 全屏图片交互 ---
        private void OnImageScroll(WheelEvent evt) { if (_imageViewerOverlay.style.display == DisplayStyle.None) return; float delta = -evt.delta.y * ZOOM_SENSITIVITY * 100f; float newScaleVal = Mathf.Clamp(_imageScale.x + delta, MIN_SCALE, MAX_SCALE); _imageScale = new Vector3(newScaleVal, newScaleVal, 1f); ApplyImageTransform(); evt.StopPropagation(); }
        private void OnImageMouseDown(MouseDownEvent evt) { if (evt.target == _btnCloseImageViewer) return; if (evt.button == 0) { _isDraggingImage = true; _lastMousePosition = evt.mousePosition; _imageViewerOverlay.CaptureMouse(); } }
        private void OnImageMouseUp(MouseUpEvent evt) { if (_isDraggingImage) { _isDraggingImage = false; _imageViewerOverlay.ReleaseMouse(); } }
        private void OnImageMouseMove(MouseMoveEvent evt) { if (_isDraggingImage && _imageViewerOverlay.style.display == DisplayStyle.Flex) { Vector2 currentMousePos = evt.mousePosition; Vector2 delta = currentMousePos - _lastMousePosition; _imagePosition += (Vector3)delta; _lastMousePosition = currentMousePos; ApplyImageTransform(); } }

        // ==================================================================================
        // 5. 编辑与删除 (Edit & Delete)
        // ==================================================================================
        
        private void EnableEditMode() 
        { 
            if (_selectedAsset == null) return; 
            string tags = (_selectedAsset.Tags != null) ? string.Join(", ", _selectedAsset.Tags) : ""; 
            _editTagsField.value = tags; 
            _editDescField.value = _selectedAsset.Description; 
            _viewContainer.style.display = DisplayStyle.None; 
            _editContainer.style.display = DisplayStyle.Flex; 
        }
        
        private void CancelEditMode() { _viewContainer.style.display = DisplayStyle.Flex; _editContainer.style.display = DisplayStyle.None; }
        
        private void SaveEdit() 
        { 
            if (_selectedAsset == null) return; 
            try { 
                string tagStr = _editTagsField.value; 
                _selectedAsset.Tags = tagStr.Split(new char[] {',', ' '}, StringSplitOptions.RemoveEmptyEntries).ToList(); 
                _selectedAsset.Description = _editDescField.value; 
                string fullPath = Path.Combine(LibraryManager.Instance.LibraryRoot, _selectedAsset.RelativePath); 
                string metaPath = fullPath + ".meta.json"; 
                if (File.Exists(metaPath)) File.WriteAllText(metaPath, JsonUtilityEx.ToJson(_selectedAsset, true)); 
                ShowDetailForAsset(_selectedAsset); 
                RefreshList(_searchField.value); 
            } catch (Exception e) { Debug.LogError($"Failed to save: {e.Message}"); } 
        }

        private void ShowDeleteConfirmationOverlay() 
        { 
            if (_selectedAsset != null) { 
                if (_deleteAssetNameLabel != null) _deleteAssetNameLabel.text = _selectedAsset.Name; 
                if (_deleteConfirmOverlay != null) _deleteConfirmOverlay.style.display = DisplayStyle.Flex; 
            } 
        }
        
        private void ConfirmDeleteAsset() 
        { 
            if (_selectedAsset == null) return; 
            try { 
                string libRoot = LibraryManager.Instance.LibraryRoot; 
                string fullAssetPath = Path.Combine(libRoot, _selectedAsset.RelativePath); 
                string assetDirectory = Path.GetDirectoryName(fullAssetPath); 
                string categoryDir = Path.GetDirectoryName(assetDirectory); 
                
                if (Directory.Exists(assetDirectory) && categoryDir != null && categoryDir.StartsWith(libRoot)) 
                    Directory.Delete(assetDirectory, true); 
                else { 
                    string metaPath = fullAssetPath + ".meta.json"; 
                    string thumbPath = Path.Combine(libRoot, _selectedAsset.ThumbnailPath); 
                    if (File.Exists(fullAssetPath)) File.Delete(fullAssetPath); 
                    if (File.Exists(metaPath)) File.Delete(metaPath); 
                    if (File.Exists(thumbPath)) File.Delete(thumbPath); 
                } 
                
                if (fullAssetPath == _lastLoadedModelPath) { 
                    _lastLoadedModelPath = ""; 
                    if(PhotoStudio != null) PhotoStudio.ClearPreviewModel(); 
                } 
                
                LibraryManager.Instance.ScanLibrary(); 
                _detailPanel.style.display = DisplayStyle.None; 
                _deleteConfirmOverlay.style.display = DisplayStyle.None; 
                _selectedAsset = null; 
                RefreshList(_searchField.value); 
            } catch (Exception e) { Debug.LogError($"Failed to delete: {e.Message}"); } 
        }
    }
}
