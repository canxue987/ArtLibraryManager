using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Networking;
using AssetLibrary.Core;
using AssetLibrary.Utilities;
using System.IO;
using System;

namespace AssetLibrary.View
{
    public partial class MainUIController
    {
        // 切换分类过滤器 (Model / Texture / Sound)
        private void ToggleFilter(AssetType type, Button clickedBtn)
        {
            if (_activeTypeFilter == type) 
                _activeTypeFilter = null; // 如果再次点击当前分类，则取消选择
            else 
                _activeTypeFilter = type;
            
            UpdateUIState();
        }

        // 更新主界面状态 (显示欢迎页 vs 显示列表)
        private void UpdateUIState()
        {
            // 更新按钮的高亮状态
            UpdateButtonState(_btnModel, _activeTypeFilter == AssetType.Model);
            UpdateButtonState(_btnTexture, _activeTypeFilter == AssetType.Texture);
            UpdateButtonState(_btnSound, _activeTypeFilter == AssetType.Sound);

            if (_activeTypeFilter == null)
            {
                // 没有选择分类时，显示欢迎面板
                _welcomePanel.style.display = DisplayStyle.Flex;
                _assetScrollView.style.display = DisplayStyle.None;
            }
            else
            {
                // 选择了分类，显示资源列表
                _welcomePanel.style.display = DisplayStyle.None;
                _assetScrollView.style.display = DisplayStyle.Flex;
                RefreshList(_searchField.value);
            }
        }

        // 辅助：更新按钮样式类
        private void UpdateButtonState(Button btn, bool isActive)
        {
            if (btn == null) return;
            if (isActive) btn.AddToClassList("active");
            else btn.RemoveFromClassList("active");
        }

        // 核心：刷新资源列表
        private void RefreshList(string filterText = "")
        {
            if (_gridContainer == null || ItemTemplate == null) return;
            if (_activeTypeFilter == null) return;

            _gridContainer.Clear();
            var allAssets = LibraryManager.Instance.Assets;
            
            foreach (var asset in allAssets)
            {
                // 1. 过滤类型
                if (asset.Type != _activeTypeFilter.Value) continue;
                
                // 2. 过滤搜索关键词 (不区分大小写)
                if (!string.IsNullOrEmpty(filterText) && !asset.Name.ToLower().Contains(filterText.ToLower())) continue;

                // 3. 实例化列表项
                TemplateContainer itemInstance = ItemTemplate.Instantiate();
                var iconEl = itemInstance.Q<VisualElement>("Icon");
                var nameEl = itemInstance.Q<Label>("Name");
                
                nameEl.text = asset.Name;
                
                // 加载缩略图
                string libRoot = LibraryManager.Instance.LibraryRoot;
                string fullThumbPath = Path.Combine(libRoot, asset.ThumbnailPath);
                // 修复路径分隔符问题，防止 url 解析出错
                fullThumbPath = fullThumbPath.Replace("\\", "/"); 

                if (File.Exists(fullThumbPath)) 
                {
                    StartCoroutine(LoadImageToBackground(fullThumbPath, iconEl)); 
                }
                else 
                {
                    // 没有缩略图则显示默认图标和类型颜色
                    if (DefaultIcon != null) iconEl.style.backgroundImage = new StyleBackground(DefaultIcon);
                    iconEl.style.backgroundColor = GetColorByType(asset.Type); 
                }

                // 绑定点击事件 -> 打开详情页
                itemInstance.RegisterCallback<ClickEvent>(evt => OnAssetClicked(asset));
                
                _gridContainer.Add(itemInstance);
            }
        }

        private void OnAssetClicked(AssetMetaData asset)
        {
            _selectedAsset = asset;
            ShowDetailForAsset(asset); // 调用 Detail 部分的方法
        }

        private void OpenCurrentAssetInExplorer()
        {
            if (_selectedAsset == null) return;
            string fullPath = Path.Combine(LibraryManager.Instance.LibraryRoot, _selectedAsset.RelativePath);
            fullPath = Path.GetFullPath(fullPath);
            try 
            { 
                // Windows 资源管理器定位选中文件
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{fullPath}\""); 
            }
            catch(Exception e) { Debug.LogError(e.Message); }
        }
    }
}
