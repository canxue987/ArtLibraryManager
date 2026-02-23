// using UnityEngine;
// using UnityEngine.UIElements;
// using AssetLibrary.Core;
// using AssetLibrary.Utilities;
// using System.IO;
// using System.Collections;
// using System.Collections.Generic;
// using System;
// using TriLibCore;
// using TriLibCore.SFB;

// namespace AssetLibrary.View
// {
//     public partial class MainUIController
//     {
//         // 点击 "+ Import Asset" 按钮
//         private void OnImportButtonClicked()
//         {
//             var extensions = new [] {
//                 new ExtensionFilter("All Files", "*" ),
//                 new ExtensionFilter("Textures", "png", "jpg", "jpeg", "tga", "psd"),
//                 new ExtensionFilter("3D Models", "fbx", "obj", "blend"),
//                 new ExtensionFilter("Sound Files", "wav", "mp3", "ogg")
//             };

//             var results = StandaloneFileBrowser.OpenFilePanel("Import Asset", "", extensions, false);
//             if (results != null && results.Count > 0 && !string.IsNullOrEmpty(results[0].Name))
//             {
//                 string selectedPath = results[0].Name;
//                 if (File.Exists(selectedPath)) ShowImportWindow(selectedPath);
//             }
//         }

//         // 显示导入窗口并初始化默认值
//         private void ShowImportWindow(string filePath)  
//         {  
//             _pendingImportPath = filePath;  
//             _importPathLabel.text = filePath;  
//             string ext = Path.GetExtension(filePath).ToLower();  
            
//             int defaultIndex = 0;  
//             AssetType detectedType = AssetType.Unknown;  
//             string typeDisplayName = "Unknown";  
            
//             if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".tga" || ext == ".psd" || ext == ".bmp") 
//             { defaultIndex = 1; detectedType = AssetType.Texture; typeDisplayName = "Texture"; }  
//             else if (ext == ".fbx" || ext == ".obj" || ext == ".blend") 
//             { defaultIndex = 0; detectedType = AssetType.Model; typeDisplayName = "Model"; }  
//             else if (ext == ".wav" || ext == ".mp3" || ext == ".ogg") 
//             { defaultIndex = 2; detectedType = AssetType.Sound; typeDisplayName = "Sound"; }  
            
//             _importTypeGroup.value = defaultIndex;  
//             _importTypeLabel.text = $"{typeDisplayName}";  
//             _importTagsField.value = "";  
//             _importDescField.value = "";  

//             _modelPreviewImage.image = null;
            
//             // 导入新资源时，必须清理掉旧的预览模型
//             if (PhotoStudio != null) 
//             {
//                 PhotoStudio.ClearPreviewModel();
//                 _lastLoadedModelPath = ""; 
//             }

//             if (detectedType == AssetType.Model)
//             {
//                 if (PhotoStudio != null) 
//                 {
//                     PhotoStudio.ResizeRenderTexture(960, 540);
//                     _modelPreviewImage.image = PhotoStudio.GetRenderTexture();
//                 }
//                 UpdateImportUIForAssetType(detectedType, filePath); 
//                 if(_importHintLabel != null) _importHintLabel.text = "Left Drag: Rotate | Scroll: Zoom";
//             }
//             else if (detectedType == AssetType.Texture)
//             {
//                 StartCoroutine(LoadImageToElement(filePath, _modelPreviewImage));
//                 UpdateImportUIForAssetType(detectedType, filePath);
//                 if(_importHintLabel != null) _importHintLabel.text = "Preview Only";
//             }
//             else
//             {
//                 _modelPreviewImage.image = DefaultIcon; 
//                 UpdateImportUIForAssetType(detectedType, filePath);
//                 if(_importHintLabel != null) _importHintLabel.text = "No Preview";
//             }

//             _importPanel.style.display = DisplayStyle.Flex; 
//         }  

//         private void UpdateImportUIForAssetType(AssetType assetType, string filePath)
//         {
//             var modelImportContainer = _importPanel.Q<VisualElement>("ModelImportContainer");
            
//             if (modelImportContainer != null)
//             {
//                 if (assetType == AssetType.Model)
//                 {
//                     modelImportContainer.style.display = DisplayStyle.Flex;
//                     StartCoroutine(PreloadModelForPreview(filePath));
//                 }
//                 else
//                 {
//                     modelImportContainer.style.display = DisplayStyle.None;
//                     if (PhotoStudio != null) PhotoStudio.ClearPreviewModel();
//                 }
//             }
//         }

//         // 预加载模型用于导入窗口的预览
//         private IEnumerator PreloadModelForPreview(string filePath)
//         {
//             if (!File.Exists(filePath)) yield break;
//             if (PhotoStudio == null) yield break;
            
//             PhotoStudio.ClearPreviewModel();
            
//             bool isLoading = true;
//             GameObject loadedGo = null;

//             try
//             {
//                 var options = AssetLoader.CreateDefaultLoaderOptions(true);
//                 AssetLoader.LoadModelFromFile(filePath, 
//                     onLoad: (context) => {
//                         loadedGo = context.RootGameObject;
//                         isLoading = false;
//                     },
//                     onMaterialsLoad: (context) => { },
//                     onProgress: (context, progress) => { },
//                     onError: (contextualizedError) => { 
//                         Debug.LogError($"Preview Load Error: {contextualizedError.GetInnerException()}");
//                         isLoading = false; 
//                     },
//                     wrapperGameObject: null,
//                     assetLoaderOptions: options
//                 );
//             }
//             catch (Exception ex) { Debug.LogError($"Start Load Error: {ex.Message}"); isLoading = false; }

//             while (isLoading) yield return null;

//             if (loadedGo != null)
//             {
//                 PhotoStudio.LoadPreviewModel(loadedGo);
//                 if (_pendingMaterialBindings != null)
//                 {
//                     PhotoStudio.ApplyMaterialsToPreview(_pendingMaterialBindings, "", this);
//                 }
//             }
//         }

//         // 点击确定导入
//         private void PerformImport() 
//         { 
//             if (string.IsNullOrEmpty(_pendingImportPath)) return; 
//             StartCoroutine(ImportRoutine()); 
//         } 

//         private IEnumerator ImportRoutine()
//         {
//             string libRoot = LibraryManager.Instance.LibraryRoot;
//             string fileName = Path.GetFileName(_pendingImportPath);
//             string assetName = Path.GetFileNameWithoutExtension(fileName);
//             string extension = Path.GetExtension(fileName);

//             int typeIndex = _importTypeGroup.value;
//             string category = typeIndex == 0 ? "Models" : (typeIndex == 1 ? "Textures" : "Sounds");
//             string categoryFolder = Path.Combine(libRoot, category);
//             if (!Directory.Exists(categoryFolder)) Directory.CreateDirectory(categoryFolder);

//             // 处理重名
//             string finalAssetName = assetName;
//             string assetFolderPath = Path.Combine(categoryFolder, finalAssetName);
//             int counter = 1;
//             while (Directory.Exists(assetFolderPath)) {
//                 finalAssetName = $"{assetName}_{counter}";
//                 assetFolderPath = Path.Combine(categoryFolder, finalAssetName);
//                 counter++;
//             }
//             Directory.CreateDirectory(assetFolderPath);
//             yield return null;

//             // 复制主文件
//             string destFileName = finalAssetName + extension;
//             string destPath = Path.Combine(assetFolderPath, destFileName);
//             File.Copy(_pendingImportPath, destPath);

//             // 生成 Meta 数据
//             AssetMetaData meta = new AssetMetaData();
//             meta.Name = finalAssetName;
//             meta.Type = (AssetType)(typeIndex + 1); 
//             meta.RelativePath = PathUtility.GetRelativePath(destPath, libRoot);
//             meta.Tags = new List<string>(_importTagsField.value.Split(','));
//             meta.Description = _importDescField.value;

//             // 处理模型材质和贴图
//             if (meta.Type == AssetType.Model && _pendingMaterialBindings != null)
//             {
//                 MaterialBindings finalBindings = new MaterialBindings();
//                 finalBindings.Workflow = _pendingMaterialBindings.Workflow;

//                 // 内部函数：复制贴图到 .Textures 隐藏文件夹
//                 string ProcessTexture(string srcPath, string suffix) 
//                 {
//                     if (string.IsNullOrEmpty(srcPath) || !File.Exists(srcPath)) return "";
//                     string hiddenTexFolder = Path.Combine(assetFolderPath, ".Textures");
//                     if (!Directory.Exists(hiddenTexFolder)) Directory.CreateDirectory(hiddenTexFolder);
//                     string texName = Path.GetFileNameWithoutExtension(srcPath) + suffix + Path.GetExtension(srcPath);
//                     string destTexPath = Path.Combine(hiddenTexFolder, $"{finalAssetName}_{texName}");
//                     if (!File.Exists(destTexPath)) File.Copy(srcPath, destTexPath);
//                     return PathUtility.GetRelativePath(destTexPath, libRoot);
//                 }

//                 finalBindings.BaseMapPath = ProcessTexture(_pendingMaterialBindings.BaseMapPath, "_Base");
//                 finalBindings.NormalMapPath = ProcessTexture(_pendingMaterialBindings.NormalMapPath, "_Normal");
//                 finalBindings.HeightMapPath = ProcessTexture(_pendingMaterialBindings.HeightMapPath, "_Height");
//                 finalBindings.OcclusionMapPath = ProcessTexture(_pendingMaterialBindings.OcclusionMapPath, "_AO");
//                 finalBindings.EmissionMapPath = ProcessTexture(_pendingMaterialBindings.EmissionMapPath, "_Emit");

//                 // 处理 Metallic/Smoothness 合并
//                 string mainMapPath = _pendingMaterialBindings.MetallicMapPath;
//                 string smoothMapPath = _pendingMaterialBindings.SmoothnessMapPath;

//                 if (!string.IsNullOrEmpty(mainMapPath) && File.Exists(mainMapPath))
//                 {
//                     string hiddenTexFolder = Path.Combine(assetFolderPath, ".Textures");
//                     if (!Directory.Exists(hiddenTexFolder)) Directory.CreateDirectory(hiddenTexFolder);

//                     string suffix = (finalBindings.Workflow == WorkflowMode.Metallic) ? "_MetalSmooth" : "_SpecSmooth";
//                     string mergedFileName = $"{finalAssetName}{suffix}.tga"; 
//                     string mergedPath = Path.Combine(hiddenTexFolder, mergedFileName);

//                     bool needMerge = !string.IsNullOrEmpty(smoothMapPath) && smoothMapPath != mainMapPath && File.Exists(smoothMapPath);

//                     if (needMerge)
//                     {
//                         yield return null; 
//                         TextureUtility.MergeChannelsToTGA(mainMapPath, smoothMapPath, mergedPath);
//                     }
//                     else
//                     {
//                         string ext = Path.GetExtension(mainMapPath);
//                         string destCopy = Path.ChangeExtension(mergedPath, ext);
//                         if (!File.Exists(destCopy)) File.Copy(mainMapPath, destCopy);
//                         mergedPath = destCopy;
//                     }

//                     finalBindings.MetallicMapPath = PathUtility.GetRelativePath(mergedPath, libRoot);
//                     finalBindings.SmoothnessMapPath = ""; 
//                 }

//                 meta.Materials = finalBindings;
//             }

//             // 生成缩略图
//             string thumbName = finalAssetName + ".thumb.jpg";
//             string thumbPath = Path.Combine(assetFolderPath, thumbName);
//             meta.ThumbnailPath = PathUtility.GetRelativePath(thumbPath, libRoot);
//             File.WriteAllText(Path.Combine(assetFolderPath, finalAssetName + ".meta.json"), JsonUtilityEx.ToJson(meta, true));

//             if (meta.Type == AssetType.Texture)  
//             {  
//                 ThumbnailGenerator.GenerateThumbnail(destPath, thumbPath);  
//             }  
//             else if (meta.Type == AssetType.Model && PhotoStudio != null)  
//             {  
//                 GameObject previewModel = PhotoStudio.GetCurrentPreviewModel();  
//                 if (previewModel != null)  
//                 {  
//                     yield return null;  
//                     yield return StartCoroutine(PhotoStudio.CaptureSnapshotAsync(previewModel, thumbPath, null));  
//                 }  
//                 else  
//                 {  
//                     Debug.LogWarning("No preview model found for snapshot.");  
//                 }  
//             }  

//             // 完成
//             LibraryManager.Instance.ScanLibrary();  
//             ResetImportPanel();   
//             _importPanel.style.display = DisplayStyle.None;  
//             RefreshList();  
//             Debug.Log("Import Complete!");  
//         }  

//         private void ResetImportPanel()
//         {
//             _pendingMaterialBindings = new MaterialBindings();
            
//             if (_labelBaseMapPath != null) _labelBaseMapPath.text = "";
//             if (_labelMetallicMapPath != null) _labelMetallicMapPath.text = "";
//             if (_labelSmoothnessMapPath != null) _labelSmoothnessMapPath.text = "";
//             if (_labelNormalMapPath != null) _labelNormalMapPath.text = "";
//             if (_labelHeightMapPath != null) _labelHeightMapPath.text = "";
//             if (_labelOcclusionMapPath != null) _labelOcclusionMapPath.text = "";
//             if (_labelEmissionMapPath != null) _labelEmissionMapPath.text = "";

//             if (PhotoStudio != null) PhotoStudio.ClearPreviewModel();

//             _pendingImportPath = null;
//         }

//         // 绑定贴图文件选择
//         private void OnSelectTextureFile(string mapType, Label displayLabel)  
//         {  
//             var extensions = new[] { new ExtensionFilter("Image Files", "png", "jpg", "jpeg", "tga", "tif") };  
//             var results = StandaloneFileBrowser.OpenFilePanel("Select Texture", "", extensions, false);  
            
//             if (results != null && results.Count > 0 && !string.IsNullOrEmpty(results[0].Name))  
//             {  
//                 string selectedTexturePath = results[0].Name; 
//                 if (displayLabel != null) displayLabel.text = Path.GetFileName(selectedTexturePath);  
//                 UpdateMaterialBinding(mapType, selectedTexturePath);  

//                 if (_importTypeGroup.value == 0 && PhotoStudio != null)  
//                 {  
//                     PhotoStudio.ApplyMaterialsToPreview(_pendingMaterialBindings,"", this);  
//                 }  
//             }  
//         } 

//         private void UpdateMaterialBinding(string mapType, string relativePath)
//         {
//             if (_pendingMaterialBindings == null)
//                 _pendingMaterialBindings = new MaterialBindings();

//             switch (mapType)
//             {
//                 case "BaseMap": _pendingMaterialBindings.BaseMapPath = relativePath; break;
//                 case "MetallicMap": _pendingMaterialBindings.MetallicMapPath = relativePath; break;
//                 case "SmoothnessMap": _pendingMaterialBindings.SmoothnessMapPath = relativePath; break;
//                 case "NormalMap": _pendingMaterialBindings.NormalMapPath = relativePath; break;
//                 case "HeightMap": _pendingMaterialBindings.HeightMapPath = relativePath; break;
//                 case "OcclusionMap": _pendingMaterialBindings.OcclusionMapPath = relativePath; break;
//                 case "EmissionMap": _pendingMaterialBindings.EmissionMapPath = relativePath; break;
//             }
//         }
        
//         private void UpdateWorkflowUI(WorkflowMode mode)  
//         {  
//             if (_labelMetallicTitle != null)  
//                 _labelMetallicTitle.text = (mode == WorkflowMode.Metallic) ? "Metallic" : "Specular";  
//         }  
//     }
// }
