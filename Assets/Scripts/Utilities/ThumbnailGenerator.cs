
using System;
using System.IO;
using UnityEngine;

namespace AssetLibrary.Utilities
{
    public static class ThumbnailGenerator
    {
        public const int ThumbnailSize = 128; // 缩略图大小

        public static void GenerateThumbnail(string sourceFilePath, string thumbnailFilePath)
        {
            // 如果缩略图已经存在，就不重新生成了
            if (File.Exists(thumbnailFilePath)) return;

            try
            {
                if (IsImageFile(sourceFilePath))
                {
                    // 1. 读取原始图片文件
                    byte[] fileData = File.ReadAllBytes(sourceFilePath);
                    Texture2D tex = new Texture2D(2, 2);
                    tex.LoadImage(fileData); // 这会自动调整 tex 的尺寸

                    // 2. 简单处理：如果图片太大，我们这里只做简单的保存（实际生产中应该做 Resize 算法，但 Unity 运行时 Resize 比较麻烦，这里先偷懒直接保存原图或不做处理，依赖 UI 显示时的缩放）
                    // 为了性能，我们这里只处理非常大的图片，或者暂时直接拷贝原图作为缩略图（虽然浪费空间，但最稳健）
                    // 改进方案：使用 RenderTexture 缩小图片
                    
                    Texture2D scaledTex = ResizeTexture(tex, ThumbnailSize, ThumbnailSize);
                    
                    // 3. 保存为 JPG 缩略图
                    byte[] thumbBytes = scaledTex.EncodeToJPG(50); // 50% 质量压缩
                    File.WriteAllBytes(thumbnailFilePath, thumbBytes);

                    // 清理内存
                    UnityEngine.Object.DestroyImmediate(tex);
                    UnityEngine.Object.DestroyImmediate(scaledTex);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"生成缩略图失败: {sourceFilePath} \nError: {e.Message}");
            }
        }

        private static Texture2D ResizeTexture(Texture2D source, int width, int height)
        {
            RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
            Graphics.Blit(source, rt);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            
            Texture2D readableTexture = new Texture2D(width, height);
            readableTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            readableTexture.Apply();
            
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            return readableTexture;
        }

        private static bool IsImageFile(string path)
        {
            string ext = Path.GetExtension(path).ToLower();
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".tga";
        }
    }
}
