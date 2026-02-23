// 一、目录规划（同一 Unity 工程内模拟 客户端 + 服务器）
// 在 Assets/ 下：

// text
// Assets/
//   ├── Scripts/
//   │   ├── Core/                         # 核心数据结构 & 本地Library逻辑（单机/服务器共用）
//   │   │   ├── AssetType.cs
//   │   │   ├── AssetMetaData.cs
//   │   │   ├── AppConfig.cs
//   │   │   └── LibraryManager.cs         # 本地库管理器（单机版/服务器端共用逻辑）
//   │   ├── Services/                     # 逻辑服务层（主要在客户端使用本地模式）
//   │   │   ├── ImportService.cs
//   │   │   ├── ExportService.cs
//   │   │   ├── PreviewService.cs
//   │   │   ├── ThumbnailGenerator.cs
//   │   │   └── HashService.cs
//   │   ├── Network/                      # 客户端网络数据访问层
//   │   │   ├── IAssetRepository.cs       # 数据访问接口（本地/远程都实现）
//   │   │   ├── LocalAssetRepository.cs   # 单机模式实现（直接调用 LibraryManager）
//   │   │   ├── RemoteAssetRepository.cs  # 服务器模式实现（通过HTTP调用）
//   │   │   ├── NetworkManager.cs         # 统一封装UnityWebRequest
//   │   │   └── ServerApiModel.cs         # API请求/响应模型（DTO）
//   │   ├── UI/                           # 客户端UI（Unity UI Toolkit）
//   │   │   ├── Controllers/
//   │   │   │   ├── MainViewController.cs
//   │   │   │   ├── AssetListController.cs
//   │   │   │   ├── AssetDetailController.cs
//   │   │   │   └── UploadDialogController.cs
//   │   │   ├── Views/
//   │   │   │   ├── MainWindow.uxml
//   │   │   │   ├── AssetItem.uxml
//   │   │   │   ├── AssetDetailPanel.uxml
//   │   │   │   ├── UploadDialog.uxml
//   │   │   │   └── MainStyles.uss
//   │   │   └── UIConstants.cs
//   │   ├── Preview/                      # 客户端预览模块
//   │   │   ├── PreviewSceneManager.cs
//   │   │   ├── ModelPreviewController.cs
//   │   │   ├── TexturePreviewController.cs
//   │   │   └── AudioPreviewController.cs
//   │   ├── ThirdParty/                   # 第三方库封装（如TriLib2）
//   │   │   └── RuntimeModelLoader.cs
//   │   ├── Server/                       # 服务器端模拟（Unity内）
//   │   │   ├── HttpServer.cs             # 简易HTTP服务（监听+路由）
//   │   │   ├── ServerLibraryManager.cs   # 服务器端Library封装（内部用 LibraryManager）
//   │   │   ├── ServerMain.cs             # 服务器启动/控制入口
//   │   │   └── Controllers/
//   │   │       └── AssetController.cs    # REST API控制器（处理 /api/assets...）
//   │   └── Utilities/                    # 通用工具
//   │       ├── PathUtility.cs
//   │       ├── Logger.cs
//   │       └── JsonUtilityEx.cs
//   ├── Resources/
//   │   ├── Icons/
//   │   │   ├── Icon_Model.png
//   │   │   ├── Icon_Texture.png
//   │   │   ├── Icon_Sound.png
//   │   │   └── Icon_VFX.png
//   │   └── DefaultThumbnail.png
//   └── StreamingAssets/
//       └── server_config.json            # 可选：服务器配置模板
// 二、核心枚举 & 数据结构骨架
// 1. AssetType：按你指定的四类
// csharp
// // Assets/Scripts/Core/AssetType.cs
// namespace AssetLibrary.Core
// {
//     public enum AssetType
//     {
//         Model,    // 模型（支持贴图、动画）
//         Texture,  // 图片/贴图
//         Sound,    // 声音
//         VFX       // 特效 / Prefab
//     }
// }
// 2. AssetVersionInfo & AssetMetaData
// csharp
// // Assets/Scripts/Core/AssetMetaData.cs
// using System;
// using System.Collections.Generic;

// namespace AssetLibrary.Core
// {
//     [Serializable]
//     public class AssetVersionInfo
//     {
//         public int VersionNumber;
//         public string FilePath;      // 相对 LibraryRoot 路径
//         public DateTime Time;
//         public string Comment;
//     }

//     [Serializable]
//     public class AssetMetaData
//     {
//         public string Id;            // GUID
//         public string Name;
//         public AssetType Type;

//         public string RelativePath;  // 当前主文件相对路径
//         public string ThumbnailPath; // 缩略图相对路径

//         public List<string> Tags = new();
//         public string Description;

//         public DateTime CreatedTime;
//         public DateTime ModifiedTime;

//         public string FileHash;      // 重复检测用

//         // ===== 模型特有 =====
//         public int TriangleCount;
//         public bool HasAnimation;
//         public List<string> AnimationClips = new();

//         // 模型与贴图关联（可选）
//         public List<string> LinkedTextureIds = new();

//         // ===== 声音特有 =====
//         public float LengthSeconds;

//         // ===== 版本管理 =====
//         public int CurrentVersion;
//         public List<AssetVersionInfo> Versions = new();
//     }
// }
// 3. AppConfig：客户端配置
// csharp
// // Assets/Scripts/Core/AppConfig.cs
// using System;

// namespace AssetLibrary.Core
// {
//     [Serializable]
//     public class AppConfig
//     {
//         public string LocalLibraryRoot;
//         public bool UseRemoteServer = false;
//         public string ServerBaseUrl = "http://127.0.0.1:8080"; // 默认

//         public string Language = "zh-CN";
//         public bool CheckDuplicateOnImport = true;
//     }
// }
// 三、核心 LibraryManager（本地库逻辑，可供单机和服务器使用）
// csharp
// // Assets/Scripts/Core/LibraryManager.cs
// using System;
// using System.Collections.Generic;
// using System.IO;
// using System.Linq;
// using AssetLibrary.Utilities;
// using UnityEngine;

// namespace AssetLibrary.Core
// {
//     /// <summary>
//     /// 本地Library管理器：
//     /// - 仅关心磁盘上的LibraryRoot目录
//     /// - 支持扫描、增删改资源、维护索引
//     /// - 不关心网络和UI
//     /// </summary>
//     public class LibraryManager
//     {
//         public string LibraryRoot { get; private set; }

//         private readonly List<AssetMetaData> _assets = new();
//         private readonly Dictionary<string, AssetMetaData> _assetsById = new();
//         private readonly Dictionary<string, List<AssetMetaData>> _assetsByTag = new(StringComparer.OrdinalIgnoreCase);
//         private readonly Dictionary<string, List<AssetMetaData>> _assetsByHash = new(StringComparer.OrdinalIgnoreCase);

//         public IReadOnlyList<AssetMetaData> Assets => _assets;

//         #region Singleton（客户端/服务器可各自实例化，或静态单例视情况使用）
//         private static LibraryManager _instance;
//         public static LibraryManager Instance => _instance ??= new LibraryManager();
//         public static void ResetSingleton() => _instance = null;
//         private LibraryManager() { }
//         #endregion

//         #region 基础初始化

//         public void SetLibraryRoot(string path)
//         {
//             if (string.IsNullOrWhiteSpace(path))
//             {
//                 Debug.LogError("Library root is null or empty.");
//                 return;
//             }

//             LibraryRoot = path;
//             if (!Directory.Exists(LibraryRoot))
//                 Directory.CreateDirectory(LibraryRoot);

//             EnsureTypeFolders();
//         }

//         private void EnsureTypeFolders()
//         {
//             Directory.CreateDirectory(Path.Combine(LibraryRoot, "Models"));
//             Directory.CreateDirectory(Path.Combine(LibraryRoot, "Textures"));
//             Directory.CreateDirectory(Path.Combine(LibraryRoot, "Sounds"));
//             Directory.CreateDirectory(Path.Combine(LibraryRoot, "VFX"));
//         }

//         #endregion

//         #region 扫描与索引

//         public void ScanLibrary()
//         {
//             _assets.Clear();
//             _assetsById.Clear();
//             _assetsByTag.Clear();
//             _assetsByHash.Clear();

//             if (string.IsNullOrEmpty(LibraryRoot) || !Directory.Exists(LibraryRoot))
//             {
//                 Debug.LogWarning("Library root not set or not exists.");
//                 return;
//             }

//             ScanFolderForMeta(Path.Combine(LibraryRoot, "Models"));
//             ScanFolderForMeta(Path.Combine(LibraryRoot, "Textures"));
//             ScanFolderForMeta(Path.Combine(LibraryRoot, "Sounds"));
//             ScanFolderForMeta(Path.Combine(LibraryRoot, "VFX"));

//             Debug.Log($"[LibraryManager] Scan complete, loaded {_assets.Count} assets.");
//         }

//         private void ScanFolderForMeta(string folderPath)
//         {
//             if (!Directory.Exists(folderPath))
//                 return;

//             var metaFiles = Directory.GetFiles(folderPath, "*_meta.json", SearchOption.AllDirectories);
//             foreach (var metaFile in metaFiles)
//             {
//                 try
//                 {
//                     var json = File.ReadAllText(metaFile);
//                     var meta = JsonUtilityEx.FromJson<AssetMetaData>(json);
//                     if (meta == null || string.IsNullOrEmpty(meta.Id))
//                         continue;

//                     RegisterAsset(meta);
//                 }
//                 catch (Exception ex)
//                 {
//                     Debug.LogError($"Failed to load meta file: {metaFile}\n{ex}");
//                 }
//             }
//         }

//         private void RegisterAsset(AssetMetaData meta)
//         {
//             _assets.Add(meta);
//             _assetsById[meta.Id] = meta;

//             if (meta.Tags != null)
//             {
//                 foreach (var tag in meta.Tags)
//                 {
//                     if (string.IsNullOrWhiteSpace(tag)) continue;
//                     if (!_assetsByTag.TryGetValue(tag, out var list))
//                     {
//                         list = new List<AssetMetaData>();
//                         _assetsByTag[tag] = list;
//                     }
//                     list.Add(meta);
//                 }
//             }

//             if (!string.IsNullOrEmpty(meta.FileHash))
//             {
//                 if (!_assetsByHash.TryGetValue(meta.FileHash, out var list))
//                 {
//                     list = new List<AssetMetaData>();
//                     _assetsByHash[meta.FileHash] = list;
//                 }
//                 list.Add(meta);
//             }
//         }

//         #endregion

//         #region 查询

//         public AssetMetaData GetAssetById(string id)
//         {
//             if (string.IsNullOrEmpty(id)) return null;
//             _assetsById.TryGetValue(id, out var asset);
//             return asset;
//         }

//         public IEnumerable<AssetMetaData> QueryAssets(
//             AssetType? type = null,
//             string nameKeyword = null,
//             IEnumerable<string> tags = null)
//         {
//             IEnumerable<AssetMetaData> query = _assets;

//             if (type.HasValue)
//                 query = query.Where(a => a.Type == type.Value);

//             if (!string.IsNullOrWhiteSpace(nameKeyword))
//             {
//                 var kw = nameKeyword.ToLowerInvariant();
//                 query = query.Where(a => (a.Name ?? "").ToLowerInvariant().Contains(kw));
//             }

//             if (tags != null)
//             {
//                 var tagList = tags
//                     .Where(t => !string.IsNullOrWhiteSpace(t))
//                     .Select(t => t.ToLowerInvariant())
//                     .ToList();

//                 if (tagList.Count > 0)
//                 {
//                     query = query.Where(a =>
//                         a.Tags != null &&
//                         tagList.All(t => a.Tags.Any(at => at.Equals(t, StringComparison.OrdinalIgnoreCase))));
//                 }
//             }

//             return query;
//         }

//         public IEnumerable<AssetMetaData> GetAssetsByHash(string hash)
//         {
//             if (string.IsNullOrWhiteSpace(hash)) return Enumerable.Empty<AssetMetaData>();
//             return _assetsByHash.TryGetValue(hash, out var list) ? list : Enumerable.Empty<AssetMetaData>();
//         }

//         #endregion

//         #region 新建 / 更新 / 删除骨架

//         public AssetMetaData CreateNewAsset(
//             string sourceFilePath,
//             AssetType type,
//             List<string> tags,
//             string displayName = null,
//             string hash = null)
//         {
//             if (!File.Exists(sourceFilePath))
//             {
//                 Debug.LogError($"Source file not found: {sourceFilePath}");
//                 return null;
//             }

//             var id = Guid.NewGuid().ToString("N");
//             var name = string.IsNullOrWhiteSpace(displayName)
//                 ? Path.GetFileNameWithoutExtension(sourceFilePath)
//                 : displayName;

//             var typeFolder = GetTypeFolderName(type);
//             var assetFolderName = name; // 也可用 id
//             var assetFolderPath = Path.Combine(LibraryRoot, typeFolder, assetFolderName);
//             Directory.CreateDirectory(assetFolderPath);

//             var ext = Path.GetExtension(sourceFilePath);
//             var targetFileName = $"{name}{ext}";
//             var targetFilePath = Path.Combine(assetFolderPath, targetFileName);
//             File.Copy(sourceFilePath, targetFilePath, overwrite: true);

//             var relativePath = PathUtility.GetRelativePath(targetFilePath, LibraryRoot);
//             var now = DateTime.Now;

//             var meta = new AssetMetaData
//             {
//                 Id = id,
//                 Name = name,
//                 Type = type,
//                 RelativePath = relativePath,
//                 ThumbnailPath = null,
//                 Tags = tags ?? new List<string>(),
//                 Description = "",
//                 CreatedTime = now,
//                 ModifiedTime = now,
//                 FileHash = hash,
//                 TriangleCount = 0,
//                 HasAnimation = false,
//                 AnimationClips = new List<string>(),
//                 LinkedTextureIds = new List<string>(),
//                 LengthSeconds = 0f,
//                 CurrentVersion = 1,
//                 Versions = new List<AssetVersionInfo>
//                 {
//                     new AssetVersionInfo
//                     {
//                         VersionNumber = 1,
//                         FilePath = relativePath,
//                         Time = now,
//                         Comment = "Initial import"
//                     }
//                 }
//             };

//             SaveMeta(meta);
//             RegisterAsset(meta);

//             return meta;
//         }

//         public void SaveMeta(AssetMetaData meta)
//         {
//             if (meta == null) return;

//             var abs = Path.Combine(LibraryRoot, meta.RelativePath);
//             var folder = Path.GetDirectoryName(abs);
//             if (folder == null) return;

//             var metaPath = Path.Combine(folder, $"{meta.Name}_meta.json");
//             var json = JsonUtilityEx.ToJson(meta, true);
//             File.WriteAllText(metaPath, json);
//         }

//         public void UpdateAssetFile(
//             AssetMetaData meta,
//             string newSourceFilePath,
//             bool createNewVersion,
//             string comment = null)
//         {
//             // TODO:
//             // 1. 计算并更新 Hash
//             // 2. 若 createNewVersion:
//             //    - 移动当前文件到 Versions 子目录
//             //    - 记录旧版本到 meta.Versions
//             // 3. 拷贝新文件作为当前版本文件
//             // 4. 更新 RelativePath、CurrentVersion、ModifiedTime
//             // 5. SaveMeta(meta);
//         }

//         public void RemoveAsset(AssetMetaData meta, bool deleteAllVersions)
//         {
//             // TODO:
//             // 1. 从 _assets / _assetsById / _assetsByTag / _assetsByHash 中移除
//             // 2. 删除对应文件夹下的主文件/缩略图/meta
//             // 3. 如 deleteAllVersions == true，删除 Versions 子目录
//         }

//         private string GetTypeFolderName(AssetType type) =>
//             type switch
//             {
//                 AssetType.Model => "Models",
//                 AssetType.Texture => "Textures",
//                 AssetType.Sound => "Sounds",
//                 AssetType.VFX => "VFX",
//                 _ => "Others"
//             };

//         #endregion
//     }
// }
// 四、客户端数据访问层：IAssetRepository + Local/Remote
// 1. IAssetRepository（UI 只依赖这个接口）
// csharp
// // Assets/Scripts/Network/IAssetRepository.cs
// using System.Collections.Generic;
// using AssetLibrary.Core;

// namespace AssetLibrary.Network
// {
//     public interface IAssetRepository
//     {
//         // 单机模式需要，远程模式可忽略实现
//         void SetLibraryRoot(string path);
//         void ScanLibrary();

//         IEnumerable<AssetMetaData> GetAssets(
//             AssetType? type,
//             string nameKeyword,
//             IEnumerable<string> tags,
//             int page,
//             int pageSize,
//             out int totalCount);

//         AssetMetaData GetAssetById(string id);

//         AssetMetaData CreateAsset(
//             string sourceFilePath,
//             AssetType type,
//             List<string> tags,
//             string displayName = null);

//         void UpdateAssetInfo(AssetMetaData asset);

//         void UpdateAssetFile(
//             AssetMetaData asset,
//             string newSourceFilePath,
//             bool createNewVersion,
//             string comment = null);

//         void RemoveAsset(AssetMetaData asset, bool deleteAllVersions);

//         // 下载文件/缩略图到本地（远程模式使用）
//         void DownloadAssetFile(AssetMetaData asset, string localTargetPath);
//         void DownloadThumbnail(AssetMetaData asset, string localTargetPath);
//     }
// }
// 2. LocalAssetRepository：单机实现
// csharp
// // Assets/Scripts/Network/LocalAssetRepository.cs
// using System.Collections.Generic;
// using System.Linq;
// using AssetLibrary.Core;

// namespace AssetLibrary.Network
// {
//     public class LocalAssetRepository : IAssetRepository
//     {
//         private readonly LibraryManager _libraryManager;

//         public LocalAssetRepository()
//         {
//             _libraryManager = LibraryManager.Instance;
//         }

//         public void SetLibraryRoot(string path)
//         {
//             _libraryManager.SetLibraryRoot(path);
//         }

//         public void ScanLibrary()
//         {
//             _libraryManager.ScanLibrary();
//         }

//         public IEnumerable<AssetMetaData> GetAssets(
//             AssetType? type,
//             string nameKeyword,
//             IEnumerable<string> tags,
//             int page,
//             int pageSize,
//             out int totalCount)
//         {
//             var query = _libraryManager.QueryAssets(type, nameKeyword, tags);
//             var list = query.ToList();
//             totalCount = list.Count;
//             return list.Skip((page - 1) * pageSize).Take(pageSize);
//         }

//         public AssetMetaData GetAssetById(string id) => _libraryManager.GetAssetById(id);

//         public AssetMetaData CreateAsset(
//             string sourceFilePath,
//             AssetType type,
//             List<string> tags,
//             string displayName = null)
//         {
//             // Local 模式下，可以先计算 hash
//             var hash = Services.HashService.CalculateFileHash(sourceFilePath);
//             return _libraryManager.CreateNewAsset(sourceFilePath, type, tags, displayName, hash);
//         }

//         public void UpdateAssetInfo(AssetMetaData asset)
//         {
//             asset.ModifiedTime = System.DateTime.Now;
//             _libraryManager.SaveMeta(asset);
//         }

//         public void UpdateAssetFile(
//             AssetMetaData asset,
//             string newSourceFilePath,
//             bool createNewVersion,
//             string comment = null)
//         {
//             _libraryManager.UpdateAssetFile(asset, newSourceFilePath, createNewVersion, comment);
//         }

//         public void RemoveAsset(AssetMetaData asset, bool deleteAllVersions)
//         {
//             _libraryManager.RemoveAsset(asset, deleteAllVersions);
//         }

//         public void DownloadAssetFile(AssetMetaData asset, string localTargetPath)
//         {
//             // 本地模式：直接从 LibraryRoot 拷贝即可
//             var src = System.IO.Path.Combine(_libraryManager.LibraryRoot, asset.RelativePath);
//             System.IO.File.Copy(src, localTargetPath, overwrite: true);
//         }

//         public void DownloadThumbnail(AssetMetaData asset, string localTargetPath)
//         {
//             if (string.IsNullOrEmpty(asset.ThumbnailPath)) return;
//             var src = System.IO.Path.Combine(_libraryManager.LibraryRoot, asset.ThumbnailPath);
//             if (System.IO.File.Exists(src))
//                 System.IO.File.Copy(src, localTargetPath, overwrite: true);
//         }
//     }
// }
// 3. NetworkManager（客户端 HTTP 封装）
// csharp
// // Assets/Scripts/Network/NetworkManager.cs
// using System;
// using System.Collections;
// using System.Collections.Generic;
// using AssetLibrary.Core;
// using AssetLibrary.Utilities;
// using UnityEngine;
// using UnityEngine.Networking;

// namespace AssetLibrary.Network
// {
//     /// <summary>
//     /// 客户端网络管理，封装UnityWebRequest调用服务器REST API。
//     /// 实际项目中应支持异步和错误处理，这里给出同步骨架/协程示例。
//     /// </summary>
//     public class NetworkManager : MonoBehaviour
//     {
//         public static NetworkManager Instance { get; private set; }

//         [Header("Server")]
//         public string ServerBaseUrl = "http://127.0.0.1:8080";

//         private void Awake()
//         {
//             if (Instance != null && Instance != this)
//             {
//                 Destroy(gameObject);
//                 return;
//             }
//             Instance = this;
//             DontDestroyOnLoad(gameObject);
//         }

//         #region DTO

//         [Serializable]
//         public class PagedResult<T>
//         {
//             public int TotalCount;
//             public List<T> Items;
//         }

//         #endregion

//         public IEnumerator GetAssetsCoroutine(
//             AssetType? type,
//             string nameKeyword,
//             IEnumerable<string> tags,
//             int page,
//             int pageSize,
//             Action<PagedResult<AssetMetaData>> onSuccess,
//             Action<string> onError)
//         {
//             var url = $"{ServerBaseUrl}/api/assets?page={page}&pageSize={pageSize}";
//             if (type.HasValue) url += $"&type={type.Value}";
//             if (!string.IsNullOrEmpty(nameKeyword)) url += $"&name={UnityWebRequest.EscapeURL(nameKeyword)}";
//             if (tags != null)
//             {
//                 var joined = string.Join(",", tags);
//                 if (!string.IsNullOrEmpty(joined))
//                     url += $"&tags={UnityWebRequest.EscapeURL(joined)}";
//             }

//             using var req = UnityWebRequest.Get(url);
//             yield return req.SendWebRequest();

//             if (req.result == UnityWebRequest.Result.Success)
//             {
//                 var json = req.downloadHandler.text;
//                 var result = JsonUtilityEx.FromJson<PagedResult<AssetMetaData>>(json);
//                 onSuccess?.Invoke(result);
//             }
//             else
//             {
//                 onError?.Invoke(req.error);
//             }
//         }

//         // TODO: GetAssetById, UploadAsset, UpdateAsset, DeleteAsset, DownloadFile 等接口类似封装
//     }
// }
// 4. RemoteAssetRepository：远程模式实现
// csharp
// // Assets/Scripts/Network/RemoteAssetRepository.cs
// using System.Collections.Generic;
// using AssetLibrary.Core;
// using UnityEngine;

// namespace AssetLibrary.Network
// {
//     public class RemoteAssetRepository : IAssetRepository
//     {
//         private readonly NetworkManager _networkManager;

//         public RemoteAssetRepository(NetworkManager networkManager)
//         {
//             _networkManager = networkManager;
//         }

//         public void SetLibraryRoot(string path) { /* 远程模式下无意义，可以空实现 */ }

//         public void ScanLibrary() { /* 远程模式由服务器维护索引，无需客户端扫描 */ }

//         public IEnumerable<AssetMetaData> GetAssets(
//             AssetType? type,
//             string nameKeyword,
//             IEnumerable<string> tags,
//             int page,
//             int pageSize,
//             out int totalCount)
//         {
//             // 注意：这里是同步接口，但NetworkManager是异步协程
//             // 实际使用时建议改为纯异步。下面仅给示意，不建议在生产中这样写。
//             totalCount = 0;
//             List<AssetMetaData> items = new();

//             var done = false;
//             var errorMsg = "";

//             NetworkManager.Instance.StartCoroutine(
//                 _networkManager.GetAssetsCoroutine(
//                     type, nameKeyword, tags, page, pageSize,
//                     result =>
//                     {
//                         totalCount = result.TotalCount;
//                         items = result.Items ?? new List<AssetMetaData>();
//                         done = true;
//                     },
//                     err =>
//                     {
//                         Debug.LogError($"GetAssets error: {err}");
//                         errorMsg = err;
//                         done = true;
//                     }));

//             // 简化版：等到请求结束（会阻塞一帧循环），真实项目应使用回调/async解决
//             while (!done) { /* 等待 */ }

//             return items;
//         }

//         public AssetMetaData GetAssetById(string id)
//         {
//             // TODO: 调用 /api/assets/{id}
//             return null;
//         }

//         public AssetMetaData CreateAsset(
//             string sourceFilePath,
//             AssetType type,
//             List<string> tags,
//             string displayName = null)
//         {
//             // TODO: 通过 multipart/form-data 上传到服务器
//             return null;
//         }

//         public void UpdateAssetInfo(AssetMetaData asset)
//         {
//             // TODO: 调用 PUT /api/assets/{id} 提交 meta 修改
//         }

//         public void UpdateAssetFile(
//             AssetMetaData asset,
//             string newSourceFilePath,
//             bool createNewVersion,
//             string comment = null)
//         {
//             // TODO: 调用 PUT /api/assets/{id}/file 上传新文件
//         }

//         public void RemoveAsset(AssetMetaData asset, bool deleteAllVersions)
//         {
//             // TODO: 调用 DELETE /api/assets/{id}
//         }

//         public void DownloadAssetFile(AssetMetaData asset, string localTargetPath)
//         {
//             // TODO: 调用 GET /api/assets/{id}/file 下载并保存到 localTargetPath
//         }

//         public void DownloadThumbnail(AssetMetaData asset, string localTargetPath)
//         {
//             // TODO: 调用 GET /api/assets/{id}/thumbnail 下载
//         }
//     }
// }
// 五、服务层（导入、导出、缩略图、Hash）骨架
// 这里保持和之前类似，只展示关键类名和简单骨架。

// 1. ImportService
// csharp
// // Assets/Scripts/Services/ImportService.cs
// using System.Collections.Generic;
// using AssetLibrary.Core;

// namespace AssetLibrary.Services
// {
//     public class ImportService
//     {
//         private readonly IAssetRepository _repository;
//         private readonly ThumbnailGenerator _thumbnailGenerator;

//         public ImportService(IAssetRepository repository, ThumbnailGenerator thumbnailGenerator)
//         {
//             _repository = repository;
//             _thumbnailGenerator = thumbnailGenerator;
//         }

//         public AssetMetaData ImportAsset(
//             string sourceFilePath,
//             AssetType type,
//             List<string> tags,
//             string displayName = null)
//         {
//             var asset = _repository.CreateAsset(sourceFilePath, type, tags, displayName);
//             if (asset == null) return null;

//             // 缩略图生成通常在本地模式做；远程模式则由服务器生成
//             if (_repository is LocalAssetRepository)
//             {
//                 _thumbnailGenerator.GenerateThumbnail(asset);
//                 _repository.UpdateAssetInfo(asset); // 更新缩略图路径等
//             }

//             return asset;
//         }
//     }
// }
// 2. ThumbnailGenerator（按类型分发）
// csharp
// // Assets/Scripts/Services/ThumbnailGenerator.cs
// using AssetLibrary.Core;

// namespace AssetLibrary.Services
// {
//     public class ThumbnailGenerator
//     {
//         public void GenerateThumbnail(AssetMetaData asset)
//         {
//             switch (asset.Type)
//             {
//                 case AssetType.Model:
//                     GenerateModelThumbnail(asset);
//                     break;
//                 case AssetType.Texture:
//                     GenerateTextureThumbnail(asset);
//                     break;
//                 case AssetType.Sound:
//                     GenerateSoundThumbnail(asset);
//                     break;
//                 case AssetType.VFX:
//                     GenerateVFXThumbnail(asset);
//                     break;
//             }
//         }

//         public void GenerateModelThumbnail(AssetMetaData asset)
//         {
//             // TODO: 使用 PreviewSceneManager + RuntimeModelLoader 截图
//         }

//         public void GenerateTextureThumbnail(AssetMetaData asset)
//         {
//             // TODO: 读取原图，缩小生成缩略图
//         }

//         public void GenerateSoundThumbnail(AssetMetaData asset)
//         {
//             // TODO: 使用统一图标
//         }

//         public void GenerateVFXThumbnail(AssetMetaData asset)
//         {
//             // TODO: 将特效Prefab放入预览场景，播放几帧截图
//         }
//     }
// }
// 3. HashService
// csharp
// // Assets/Scripts/Services/HashService.cs
// using System.IO;
// using System.Security.Cryptography;
// using System.Text;

// namespace AssetLibrary.Services
// {
//     public static class HashService
//     {
//         public static string CalculateFileHash(string filePath)
//         {
//             using var sha1 = SHA1.Create();
//             using var stream = File.OpenRead(filePath);
//             var hash = sha1.ComputeHash(stream);
//             var sb = new StringBuilder();
//             foreach (var b in hash)
//                 sb.Append(b.ToString("x2"));
//             return sb.ToString();
//         }
//     }
// }
// 六、服务器端骨架（Unity 中模拟）
// 1. ServerLibraryManager：简单封装 LibraryManager
// csharp
// // Assets/Scripts/Server/ServerLibraryManager.cs
// using AssetLibrary.Core;

// namespace AssetLibrary.Server
// {
//     /// <summary>
//     /// 服务器端对 LibraryManager 的封装，日后可加入线程安全控制。
//     /// </summary>
//     public class ServerLibraryManager
//     {
//         public LibraryManager Library => LibraryManager.Instance;

//         public void Init(string libraryRoot)
//         {
//             Library.SetLibraryRoot(libraryRoot);
//             Library.ScanLibrary();
//         }

//         // 此处可以封装服务器专用的线程安全操作
//     }
// }
// 2. HttpServer：简化 HTTP 监听骨架
// csharp
// // Assets/Scripts/Server/HttpServer.cs
// using System.Net;
// using System.Text;
// using System.Threading;
// using AssetLibrary.Utilities;
// using UnityEngine;

// namespace AssetLibrary.Server
// {
//     /// <summary>
//     /// 非生产级，仅作为Unity内模拟的简易HTTP服务器骨架。
//     /// 正式环境建议使用独立 .NET WebAPI。
//     /// </summary>
//     public class HttpServer
//     {
//         private readonly HttpListener _listener = new();
//         private readonly ServerLibraryManager _serverLibraryManager = new();

//         public void Start(string prefix, string libraryRoot)
//         {
//             _listener.Prefixes.Add(prefix); // e.g. "http://*:8080/"
//             _listener.Start();

//             _serverLibraryManager.Init(libraryRoot);

//             var thread = new Thread(ListenLoop) { IsBackground = true };
//             thread.Start();

//             Logger.Info($"HttpServer started at {prefix}, library: {libraryRoot}");
//         }

//         public void Stop()
//         {
//             _listener.Stop();
//         }

//         private void ListenLoop()
//         {
//             while (_listener.IsListening)
//             {
//                 try
//                 {
//                     var context = _listener.GetContext();
//                     ProcessRequest(context);
//                 }
//                 catch (HttpListenerException)
//                 {
//                     // listener stopped
//                     break;
//                 }
//             }
//         }

//         private void ProcessRequest(HttpListenerContext context)
//         {
//             var path = context.Request.Url.AbsolutePath;
//             var method = context.Request.HttpMethod;

//             // 简化路由
//             if (path.StartsWith("/api/assets"))
//             {
//                 AssetController.Handle(context, _serverLibraryManager);
//             }
//             else if (path == "/api/ping")
//             {
//                 var buffer = Encoding.UTF8.GetBytes("{\"status\":\"ok\"}");
//                 context.Response.ContentType = "application/json";
//                 context.Response.OutputStream.Write(buffer, 0, buffer.Length);
//                 context.Response.Close();
//             }
//             else
//             {
//                 context.Response.StatusCode = 404;
//                 context.Response.Close();
//             }
//         }
//     }
// }
// 3. AssetController：REST API 处理骨架
// csharp
// // Assets/Scripts/Server/Controllers/AssetController.cs
// using System;
// using System.IO;
// using System.Linq;
// using System.Net;
// using System.Text;
// using AssetLibrary.Core;
// using AssetLibrary.Utilities;

// namespace AssetLibrary.Server
// {
//     public static class AssetController
//     {
//         [Serializable]
//         public class PagedResult<T>
//         {
//             public int TotalCount;
//             public T[] Items;
//         }

//         public static void Handle(HttpListenerContext context, ServerLibraryManager serverLib)
//         {
//             var req = context.Request;
//             var res = context.Response;
//             var path = req.Url.AbsolutePath;
//             var method = req.HttpMethod;

//             // /api/assets
//             if (path == "/api/assets" && method == "GET")
//             {
//                 HandleGetAssets(context, serverLib);
//                 return;
//             }

//             // /api/assets/{id} or /api/assets/{id}/file/thumbnail
//             var segments = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
//             if (segments.Length >= 2 && segments[0] == "api" && segments[1] == "assets")
//             {
//                 if (segments.Length == 3)
//                 {
//                     var id = segments[2];
//                     if (method == "GET") { HandleGetAssetById(context, serverLib, id); return; }
//                     if (method == "DELETE") { HandleDeleteAsset(context, serverLib, id); return; }
//                     // TODO: PUT /api/assets/{id} 更新元信息
//                 }
//                 else if (segments.Length == 4)
//                 {
//                     var id = segments[2];
//                     var sub = segments[3];
//                     if (sub == "file" && method == "GET")
//                     {
//                         HandleDownloadFile(context, serverLib, id);
//                         return;
//                     }
//                     if (sub == "thumbnail" && method == "GET")
//                     {
//                         HandleDownloadThumbnail(context, serverLib, id);
//                         return;
//                     }
//                 }
//             }

//             res.StatusCode = 404;
//             res.Close();
//         }

//         private static void HandleGetAssets(HttpListenerContext context, ServerLibraryManager serverLib)
//         {
//             var query = context.Request.QueryString;
//             var typeStr = query["type"];
//             var name = query["name"];
//             var tagsCsv = query["tags"];
//             var page = int.TryParse(query["page"], out var p) ? Math.Max(1, p) : 1;
//             var pageSize = int.TryParse(query["pageSize"], out var ps) ? Math.Max(1, ps) : 50;

//             AssetType? type = null;
//             if (!string.IsNullOrEmpty(typeStr) && Enum.TryParse<AssetType>(typeStr, out var at))
//                 type = at;

//             var tags = string.IsNullOrEmpty(tagsCsv)
//                 ? null
//                 : tagsCsv.Split(',').Where(t => !string.IsNullOrWhiteSpace(t)).ToArray();

//             var all = serverLib.Library.QueryAssets(type, name, tags).ToList();
//             var total = all.Count;
//             var pageItems = all.Skip((page - 1) * pageSize).Take(pageSize).ToArray();

//             var result = new PagedResult<AssetMetaData>
//             {
//                 TotalCount = total,
//                 Items = pageItems
//             };

//             var json = JsonUtilityEx.ToJson(result, true);
//             var bytes = Encoding.UTF8.GetBytes(json);
//             context.Response.ContentType = "application/json";
//             context.Response.OutputStream.Write(bytes, 0, bytes.Length);
//             context.Response.Close();
//         }

//         private static void HandleGetAssetById(HttpListenerContext context, ServerLibraryManager serverLib, string id)
//         {
//             var asset = serverLib.Library.GetAssetById(id);
//             if (asset == null)
//             {
//                 context.Response.StatusCode = 404;
//                 context.Response.Close();
//                 return;
//             }

//             var json = JsonUtilityEx.ToJson(asset, true);
//             var bytes = Encoding.UTF8.GetBytes(json);
//             context.Response.ContentType = "application/json";
//             context.Response.OutputStream.Write(bytes, 0, bytes.Length);
//             context.Response.Close();
//         }

//         private static void HandleDeleteAsset(HttpListenerContext context, ServerLibraryManager serverLib, string id)
//         {
//             var asset = serverLib.Library.GetAssetById(id);
//             if (asset == null)
//             {
//                 context.Response.StatusCode = 404;
//                 context.Response.Close();
//                 return;
//             }

//             serverLib.Library.RemoveAsset(asset, deleteAllVersions: true);
//             context.Response.StatusCode = 204;
//             context.Response.Close();
//         }

//         private static void HandleDownloadFile(HttpListenerContext context, ServerLibraryManager serverLib, string id)
//         {
//             var asset = serverLib.Library.GetAssetById(id);
//             if (asset == null)
//             {
//                 context.Response.StatusCode = 404;
//                 context.Response.Close();
//                 return;
//             }

//             var fullPath = Path.Combine(serverLib.Library.LibraryRoot, asset.RelativePath);
//             if (!File.Exists(fullPath))
//             {
//                 context.Response.StatusCode = 404;
//                 context.Response.Close();
//                 return;
//             }

//             context.Response.ContentType = "application/octet-stream";
//             using var fs = File.OpenRead(fullPath);
//             fs.CopyTo(context.Response.OutputStream);
//             context.Response.Close();
//         }

//         private static void HandleDownloadThumbnail(HttpListenerContext context, ServerLibraryManager serverLib, string id)
//         {
//             var asset = serverLib.Library.GetAssetById(id);
//             if (asset == null || string.IsNullOrEmpty(asset.ThumbnailPath))
//             {
//                 context.Response.StatusCode = 404;
//                 context.Response.Close();
//                 return;
//             }

//             var fullPath = Path.Combine(serverLib.Library.LibraryRoot, asset.ThumbnailPath);
//             if (!File.Exists(fullPath))
//             {
//                 context.Response.StatusCode = 404;
//                 context.Response.Close();
//                 return;
//             }

//             context.Response.ContentType = "image/png";
//             using var fs = File.OpenRead(fullPath);
//             fs.CopyTo(context.Response.OutputStream);
//             context.Response.Close();
//         }
//     }
// }
// 4. ServerMain：在 Unity 场景里启动服务器
// csharp
// // Assets/Scripts/Server/ServerMain.cs
// using AssetLibrary.Utilities;
// using UnityEngine;

// namespace AssetLibrary.Server
// {
//     /// <summary>
//     /// 挂在一个GameObject上，用于在Unity内启动简易服务器（开发测试用）。
//     /// </summary>
//     public class ServerMain : MonoBehaviour
//     {
//         public string Prefix = "http://*:8080/";
//         public string LibraryRootPath = "D:/SharedAssetLibrary"; // 测试时可在Inspector中设定

//         private HttpServer _server;

//         private void Start()
//         {
//             _server = new HttpServer();
//             _server.Start(Prefix, LibraryRootPath);
//         }

//         private void OnDestroy()
//         {
//             _server?.Stop();
//             Logger.Info("Server stopped.");
//         }
//     }
// }
// 七、Utilities：Json、Path、Logger
// csharp
// // Assets/Scripts/Utilities/JsonUtilityEx.cs
// using System;
// using UnityEngine;

// namespace AssetLibrary.Utilities
// {
//     public static class JsonUtilityEx
//     {
//         public static string ToJson<T>(T obj, bool prettyPrint = false)
//         {
//             return JsonUtility.ToJson(obj, prettyPrint);
//         }

//         public static T FromJson<T>(string json)
//         {
//             return JsonUtility.FromJson<T>(json);
//         }
//     }
// }
// csharp
// // Assets/Scripts/Utilities/PathUtility.cs
// using System;
// using System.IO;

// namespace AssetLibrary.Utilities
// {
//     public static class PathUtility
//     {
//         public static string GetRelativePath(string fullPath, string basePath)
//         {
//             var fullUri = new Uri(AppendSlash(fullPath));
//             var baseUri = new Uri(AppendSlash(basePath));
//             return Uri.UnescapeDataString(
//                 baseUri.MakeRelativeUri(fullUri).ToString().Replace('/', Path.DirectorySeparatorChar));
//         }

//         private static string AppendSlash(string path)
//         {
//             if (!path.EndsWith(Path.DirectorySeparatorChar.ToString()))
//                 path += Path.DirectorySeparatorChar;
//             return path;
//         }
//     }
// }
// csharp
// // Assets/Scripts/Utilities/Logger.cs
// using UnityEngine;

// namespace AssetLibrary.Utilities
// {
//     public static class Logger
//     {
//         public static void Info(string msg) => Debug.Log("[AssetLibrary] " + msg);
//         public static void Warn(string msg) => Debug.LogWarning("[AssetLibrary] " + msg);
//         public static void Error(string msg) => Debug.LogError("[AssetLibrary] " + msg);
//     }
// }