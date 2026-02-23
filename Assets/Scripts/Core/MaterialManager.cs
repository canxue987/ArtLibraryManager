using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace AssetLibrary.Core
{
    public static class MaterialManager
    {
        // Shader 属性 ID 缓存 (性能优化)
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int MetallicGlossMapId = Shader.PropertyToID("_MetallicGlossMap");
        private static readonly int SpecGlossMapId = Shader.PropertyToID("_SpecGlossMap");
        private static readonly int BumpMapId = Shader.PropertyToID("_BumpMap");
        private static readonly int OcclusionMapId = Shader.PropertyToID("_OcclusionMap");
        private static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");
        private static readonly int ParallaxMapId = Shader.PropertyToID("_ParallaxMap");

        /// <summary>
        /// 应用材质绑定 (支持多材质和单材质模式)
        /// </summary>
        /// <param name="model">目标模型</param>
        /// <param name="assetData">资源元数据 (包含材质配置)</param>
        /// <param name="libraryRoot">库根目录</param>
        /// <param name="coroutineRunner">用于运行协程的 MonoBehaviour</param>
        /// <param name="tracker">协程追踪列表 (可选)</param>
        /// <param name="onProgress">进度回调 (0.0 ~ 1.0)</param>
        public static void ApplyMaterialBindings(GameObject model, AssetMetaData assetData, string libraryRoot, MonoBehaviour coroutineRunner, List<Coroutine> tracker = null, System.Action<float> onProgress = null)
        {
            if (model == null || assetData == null)
            {
                onProgress?.Invoke(1.0f);
                return;
            }

            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            
            // 1. 统计总任务数 (按材质球槽位数量统计，简化进度计算)
            int totalTasks = 0;
            foreach (var r in renderers) totalTasks += r.sharedMaterials.Length;
            int finishedTasks = 0;

            if (totalTasks == 0)
            {
                onProgress?.Invoke(1.0f);
                return;
            }

            // 进度汇报辅助函数
            void OnTaskFinished()
            {
                finishedTasks++;
                // 防止除以0，虽然上面防住了
                float progress = (totalTasks > 0) ? (float)finishedTasks / totalTasks : 1.0f;
                onProgress?.Invoke(progress);
            }

            // 判断是否启用了多材质列表
            bool useMultiMaterial = assetData.MultiMaterials != null && assetData.MultiMaterials.Count > 0;

            // 2. 遍历所有渲染器和材质
            foreach (var r in renderers)
            {
                // 获取材质数组的副本
                Material[] mats = r.sharedMaterials; 

                for (int i = 0; i < mats.Length; i++)
                {
                    Material mat = mats[i];
                    
                    // 如果材质为空，直接标记完成并跳过
                    if (mat == null) 
                    { 
                        OnTaskFinished(); 
                        continue; 
                    }

                    // --- 核心逻辑：决定使用哪套配置 ---
                    MaterialBindings bindingToUse = null;

                    if (useMultiMaterial)
                    {
                        // 清理材质名 (移除 " (Instance)")
                        string matName = mat.name.Replace(" (Instance)", "").Trim();
                        
                        // 在列表中查找匹配项
                        var setting = assetData.MultiMaterials.Find(s => s.MaterialName == matName);
                        if (setting != null)
                        {
                            bindingToUse = setting.Bindings;
                        }
                        else
                        {
                            // 如果没找到名字匹配的，且没有默认单材质，就跳过
                             // 你也可以在这里决定是否 fallback 到 assetData.Materials
                        }
                    }
                    else
                    {
                        // 旧模式：直接使用通用的单材质配置
                        bindingToUse = assetData.Materials;
                    }

                    // --- 执行应用逻辑 ---
                    if (bindingToUse != null)
                    {
                        UpgradeShaderToURP(mat, bindingToUse.Workflow);

                        if (coroutineRunner != null)
                        {
                            // 启动一个协程来处理该材质的所有贴图加载
                            Coroutine c = coroutineRunner.StartCoroutine(ApplyMaterialRoutine(mat, bindingToUse, libraryRoot, OnTaskFinished));
                            if (tracker != null) tracker.Add(c);
                        }
                        else
                        {
                            OnTaskFinished(); // 如果没有 Runner，无法加载贴图，标记完成
                        }
                    }
                    else
                    {
                        OnTaskFinished(); // 没有对应的绑定配置，标记完成
                    }
                }
            }
        }

        // 单个材质的应用协程
        private static IEnumerator ApplyMaterialRoutine(Material mat, MaterialBindings bindings, string libraryRoot, System.Action onComplete)
        {
            // 并行加载所有贴图 (只要路径有效)
            // 注意：这里简化为顺序 yield，实际上因为 WebRequest 和 FileIO 是异步的，顺序执行也不会卡死
            
            if (!string.IsNullOrEmpty(bindings.BaseMapPath))
                yield return ApplyTextureFast(mat, bindings.BaseMapPath, libraryRoot, "_BaseMap");

            if (!string.IsNullOrEmpty(bindings.EmissionMapPath))
                yield return ApplyTextureFast(mat, bindings.EmissionMapPath, libraryRoot, "_EmissionMap");

            if (bindings.Workflow == WorkflowMode.Specular)
            {
                if (!string.IsNullOrEmpty(bindings.MetallicMapPath)) // Specular 流程里这个字段存的是 Specular贴图
                    yield return ApplyTextureLinear(mat, bindings.MetallicMapPath, libraryRoot, "_SpecGlossMap");
            }
            else
            {
                if (!string.IsNullOrEmpty(bindings.MetallicMapPath))
                    yield return ApplyTextureLinear(mat, bindings.MetallicMapPath, libraryRoot, "_MetallicGlossMap");
            }

            if (!string.IsNullOrEmpty(bindings.NormalMapPath))
                yield return ApplyTextureLinear(mat, bindings.NormalMapPath, libraryRoot, "_BumpMap");

            if (!string.IsNullOrEmpty(bindings.OcclusionMapPath))
                yield return ApplyTextureLinear(mat, bindings.OcclusionMapPath, libraryRoot, "_OcclusionMap");

            if (!string.IsNullOrEmpty(bindings.HeightMapPath))
                yield return ApplyTextureLinear(mat, bindings.HeightMapPath, libraryRoot, "_ParallaxMap");

            // 该材质处理完毕
            onComplete?.Invoke();
        }

        private static void UpgradeShaderToURP(Material mat, WorkflowMode workflow)
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) urpLit = Shader.Find("Standard"); // Fallback

            if (urpLit != null)
            {
                if (mat.shader.name != urpLit.name) mat.shader = urpLit;

                // 激活常用关键字
                mat.EnableKeyword("_NORMALMAP");
                mat.EnableKeyword("_EMISSION");
                mat.EnableKeyword("_OCCLUSIONMAP");
                mat.EnableKeyword("_PARALLAXMAP");

                if (workflow == WorkflowMode.Specular)
                {
                    mat.DisableKeyword("_METALLICSPECGLOSSMAP");
                    mat.EnableKeyword("_SPECULAR_SETUP");
                    mat.EnableKeyword("_SPECGLOSSMAP");
                }
                else
                {
                    mat.DisableKeyword("_SPECULAR_SETUP");
                    mat.DisableKeyword("_SPECGLOSSMAP");
                    mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                }
            }
        }

        /// <summary>
        /// 【快速通道】使用 UnityWebRequest 加载 sRGB 颜色贴图 (Albedo, Emission)
        /// </summary>
        private static IEnumerator ApplyTextureFast(Material mat, string path, string libraryRoot, string propertyName)
        {
            if (string.IsNullOrEmpty(path)) yield break;
            string fullPath = ResolvePath(path, libraryRoot);
            if (!File.Exists(fullPath)) yield break;

            string url = "file://" + fullPath;
            using (UnityWebRequest uwr = UnityWebRequestTexture.GetTexture(url))
            {
                yield return uwr.SendWebRequest();
                if (uwr.result == UnityWebRequest.Result.Success)
                {
                    Texture2D tex = DownloadHandlerTexture.GetContent(uwr);
                    if (tex != null)
                    {
                        tex.wrapMode = TextureWrapMode.Repeat;
                        ApplyTextureToMat(mat, tex, propertyName);
                    }
                }
            }
        }

        /// <summary>
        /// 【精确通道】使用 IO 加载 Linear 贴图 (法线/金属/AO/高度)
        /// </summary>
        private static IEnumerator ApplyTextureLinear(Material mat, string path, string libraryRoot, string propertyName)
        {
            if (string.IsNullOrEmpty(path)) yield break;
            string fullPath = ResolvePath(path, libraryRoot);
            if (!File.Exists(fullPath)) yield break;

            bool isNormal = (propertyName == "_BumpMap");

            // 使用 Task 在后台线程读取文件字节，避免卡顿
            var readTask = Task.Run(() => File.ReadAllBytes(fullPath));
            while (!readTask.IsCompleted) yield return null; 

            byte[] fileData = readTask.Result;
            if (fileData != null)
            {
                // linear: true 确保数据贴图不进行 sRGB 矫正
                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, true, true);
                if (tex.LoadImage(fileData))
                {
                    tex.wrapMode = TextureWrapMode.Repeat;
                    tex.filterMode = FilterMode.Trilinear;
                    if (isNormal) tex.anisoLevel = 4;
                    
                    ApplyTextureToMat(mat, tex, propertyName);
                }
                else
                {
                    Object.Destroy(tex);
                }
            }
        }

        private static string ResolvePath(string path, string root)
        {
            if (!Path.IsPathRooted(path) && !string.IsNullOrEmpty(root)) return Path.Combine(root, path);
            return path;
        }

        private static void ApplyTextureToMat(Material mat, Texture2D tex, string propertyName)
        {
            if (mat == null || tex == null) return;

            if (propertyName == "_BaseMap")
            {
                mat.SetTexture(BaseMapId, tex);
                mat.SetTexture(MainTexId, tex); // 兼容 Standard shader
                mat.color = Color.white;
            }
            else if (propertyName == "_BumpMap")
            {
                mat.SetTexture(BumpMapId, tex);
            }
            else if (propertyName == "_MetallicGlossMap")
            {
                mat.SetTexture(MetallicGlossMapId, tex);
                mat.SetFloat("_Smoothness", 1.0f); // 让贴图控制
                mat.SetFloat("_Metallic", 1.0f);
            }
            else if (propertyName == "_SpecGlossMap")
            {
                mat.SetTexture(SpecGlossMapId, tex);
            }
            else if (propertyName == "_OcclusionMap")
            {
                mat.SetTexture(OcclusionMapId, tex);
            }
            else if (propertyName == "_EmissionMap")
            {
                mat.SetTexture(EmissionMapId, tex);
                mat.SetColor("_EmissionColor", Color.white);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else if (propertyName == "_ParallaxMap")
            {
                mat.SetTexture(ParallaxMapId, tex);
                mat.SetFloat("_Parallax", 0.02f); // 默认给一点高度
            }
        }
    }
}
