using System;
using System.Collections.Generic;
using System.IO;
using AssetLibrary.Utilities;
using UnityEngine;
using System.Linq; // 确保引用 Linq 以防万一

namespace AssetLibrary.Core
{
    public class LibraryManager
    {
        private static LibraryManager _instance;
        public static LibraryManager Instance => _instance ??= new LibraryManager();
        private LibraryManager() { }

        public string LibraryRoot { get; private set; }
        private readonly List<AssetMetaData> _assets = new();
        public IReadOnlyList<AssetMetaData> Assets => _assets;

        public void SetLibraryRoot(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            
            LibraryRoot = path;
            
            try
            {
                if (!Directory.Exists(LibraryRoot))
                {
                    Directory.CreateDirectory(LibraryRoot);
                    Debug.Log($"[LibraryManager] Created directory: {LibraryRoot}");
                }
                Debug.Log($"[LibraryManager] Root set to: {LibraryRoot}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LibraryManager] Failed to set root path: {path}. Error: {ex.Message}");
            }
        }

        public void ScanLibrary()
        {
            _assets.Clear();
            if (string.IsNullOrEmpty(LibraryRoot) || !Directory.Exists(LibraryRoot))
            {
                Debug.LogError($"[LibraryManager] Cannot scan. Root path is invalid or does not exist: {LibraryRoot}");
                return;
            }

            Debug.Log("[LibraryManager] Start Scanning...");
            try 
            {
                var files = Directory.GetFiles(LibraryRoot, "*.*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    // 排除元数据文件、系统文件、以及缩略图文件
                    if (file.Contains(".Textures") ||
                        file.EndsWith(".meta") || 
                        file.EndsWith(".json") || 
                        file.EndsWith(".DS_Store") || 
                        file.EndsWith(".thumb.jpg")) 
                    {
                        continue;
                    }

                    var meta = LoadOrGenerateMeta(file);
                    if (meta != null) 
                    {
                        _assets.Add(meta);
                    }
                }
                Debug.Log($"[LibraryManager] Scan complete. Found {_assets.Count} assets.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LibraryManager] Error during scanning: {ex.Message}");
            }
        }

        private AssetMetaData LoadOrGenerateMeta(string filePath)
        {
            try
            {
                string dir = Path.GetDirectoryName(filePath);
                
                // 【修改点 1】使用 GetFileNameWithoutExtension
                // 这样 Lion.fbx 对应的就是 Lion.meta.json，而不是 Lion.fbx.meta.json
                string fileNameNoExt = Path.GetFileNameWithoutExtension(filePath);
                
                string metaPath = Path.Combine(dir, fileNameNoExt + ".meta.json");

                // 定义缩略图路径：Lion.thumb.jpg (去掉原后缀，与 Import 逻辑保持一致)
                string thumbName = fileNameNoExt + ".thumb.jpg";
                string thumbPath = Path.Combine(dir, thumbName);

                AssetMetaData meta = null;
                if (File.Exists(metaPath))
                {
                    string json = File.ReadAllText(metaPath);
                    meta = JsonUtilityEx.FromJson<AssetMetaData>(json);
                }

                if (meta == null)
                {
                    // 如果没有 Meta，说明是新文件（或者旧的命名不对），新建一个
                    meta = new AssetMetaData();
                    meta.Name = fileNameNoExt; // Name 也不带后缀，好看一点
                    meta.RelativePath = PathUtility.GetRelativePath(filePath, LibraryRoot);
                    meta.Type = DetermineTypeByExtension(filePath);
                    
                    // 记录缩略图路径
                    meta.ThumbnailPath = PathUtility.GetRelativePath(thumbPath, LibraryRoot);
                    
                    File.WriteAllText(metaPath, JsonUtilityEx.ToJson(meta, true));
                }
                else
                {
                    // 即使 Meta 存在，也更新一下 RelativePath，防止文件夹移动后路径失效
                    meta.RelativePath = PathUtility.GetRelativePath(filePath, LibraryRoot);
                    
                    // 如果旧 Meta 里没有缩略图路径，补上
                    if (string.IsNullOrEmpty(meta.ThumbnailPath))
                    {
                        meta.ThumbnailPath = PathUtility.GetRelativePath(thumbPath, LibraryRoot);
                        File.WriteAllText(metaPath, JsonUtilityEx.ToJson(meta, true));
                    }
                }

                // 【修改点 2】只针对图片类型自动生成缩略图
                // 这样如果是 Model 类型，扫描器就不会覆盖掉你用 PhotoStudio 拍的高级照片了
                if (meta.Type == AssetType.Texture && !File.Exists(thumbPath))
                {
                     ThumbnailGenerator.GenerateThumbnail(filePath, thumbPath);
                }

                return meta;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error processing file {filePath}: {e.Message}");
                return null;
            }
        }

        private AssetType DetermineTypeByExtension(string path)
        {
            string ext = Path.GetExtension(path).ToLower();
            if (ext == ".fbx" || ext == ".obj" || ext == ".blend") return AssetType.Model;
            if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".tga" || ext == ".bmp" || ext == ".psd") return AssetType.Texture;
            if (ext == ".wav" || ext == ".mp3" || ext == ".ogg") return AssetType.Sound;
            if (ext == ".prefab" || ext == ".mat") return AssetType.VFX;
            return AssetType.Unknown;
        }
    }
}