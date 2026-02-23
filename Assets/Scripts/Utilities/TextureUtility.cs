using UnityEngine;
using System.IO;

namespace AssetLibrary.Utilities
{
    public static class TextureUtility
    {
        /// <summary>
        /// 合并贴图通道：将 MapA 的 RGB 和 MapB 的 R通道(作为Alpha) 合并
        /// </summary>
        /// <param name="mapAPath">主贴图 (Metallic 或 Specular)</param>
        /// <param name="mapBPath">通道贴图 (Smoothness)</param>
        /// <param name="savePath">保存路径 (必须是 .tga)</param>
        public static void MergeChannelsToTGA(string mapAPath, string mapBPath, string savePath)
        {
            Texture2D texA = LoadTextureRaw(mapAPath);
            Texture2D texB = null;

            // 如果有第二张图，加载它；如果没有，默认为纯色（比如全光滑）
            if (!string.IsNullOrEmpty(mapBPath) && File.Exists(mapBPath))
            {
                texB = LoadTextureRaw(mapBPath);
            }

            if (texA == null) return;

            int width = texA.width;
            int height = texA.height;

            // 如果 texB 存在但尺寸不一致，需要调整 (这里简单处理：强制缩放 texB 匹配 texA，或者通过 UV 采样)
            // 为简化代码和性能，这里假设用户提供的贴图尺寸一致，或者只处理像素对应
            // 严谨的做法应该是 Resize，但运行时 Resize 比较耗时。
            
            Color32[] colorsA = texA.GetPixels32();
            Color32[] resultColors = new Color32[colorsA.Length];

            // 准备 B 的像素数据
            Color32[] colorsB = null;
            bool hasB = (texB != null);
            if (hasB)
            {
                // 如果尺寸不匹配，暂不支持合并，或者直接使用 A 的 Alpha
                if (texB.width != width || texB.height != height)
                {
                    Debug.LogWarning("[TextureUtility] Map sizes assume equal for merging. Scaling functionality omitted for performance.");
                    // 可以在这里添加 Resize 逻辑，但现在先 fallback
                    hasB = false; 
                }
                else
                {
                    colorsB = texB.GetPixels32();
                }
            }

            for (int i = 0; i < resultColors.Length; i++)
            {
                byte r = colorsA[i].r;
                byte g = colorsA[i].g;
                byte b = colorsA[i].b;
                
                // Alpha 通道逻辑：
                // 如果有 Smoothness 图，取其 R 通道作为 Alpha
                // 如果没有，保留 A 原有的 Alpha (假设已经是 packed)
                byte a = hasB ? colorsB[i].r : colorsA[i].a;

                resultColors[i] = new Color32(r, g, b, a);
            }

            Texture2D resultTex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            resultTex.SetPixels32(resultColors);
            resultTex.Apply();

            byte[] bytes = resultTex.EncodeToTGA();
            File.WriteAllBytes(savePath, bytes);

            // 清理内存
            if (Application.isPlaying)
            {
                Object.Destroy(texA);
                if (texB != null) Object.Destroy(texB);
                Object.Destroy(resultTex);
            }
            else
            {
                Object.DestroyImmediate(texA);
                if (texB != null) Object.DestroyImmediate(texB);
                Object.DestroyImmediate(resultTex);
            }
        }

        private static Texture2D LoadTextureRaw(string path)
        {
            if (!File.Exists(path)) return null;
            byte[] data = File.ReadAllBytes(path);
            Texture2D tex = new Texture2D(2, 2);
            tex.LoadImage(data);
            return tex;
        }
    }
}
