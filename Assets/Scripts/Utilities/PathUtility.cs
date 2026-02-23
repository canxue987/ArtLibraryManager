/* 
    文件: Assets/Scripts/Utilities/PathUtility.cs 
    状态: 已合并 Core 和 Utilities 版本，请确保只保留这一个
*/
using System;
using System.IO;

namespace AssetLibrary.Utilities
{
    public static class PathUtility
    {
        /// <summary>
        /// 获取相对于根目录的路径 (跨平台兼容版)
        /// </summary>
        public static string GetRelativePath(string fullPath, string basePath)
        {
            if (string.IsNullOrEmpty(fullPath) || string.IsNullOrEmpty(basePath))
                return "";

            // 1. 统一标准化路径分隔符 (全部转为 / 或全部转为系统默认)
            // 为了跨平台安全，这里建议统一转为正斜杠 '/'，因为 Windows 也识别 '/'
            string normalizedFull = fullPath.Replace('\\', '/');
            string normalizedBase = basePath.Replace('\\', '/');

            // 2. 确保 basePath 以分隔符结尾，防止 "Asset" 匹配到 "Assets"
            if (!normalizedBase.EndsWith("/"))
            {
                normalizedBase += "/";
            }

            // 3. 忽略大小写比较 (Windows不区分大小写，但为了保险统一处理)
            if (normalizedFull.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase))
            {
                return normalizedFull.Substring(normalizedBase.Length);
            }

            // 4. 如果不在 basePath 下，回退方案：直接返回文件名，或者保持原样
            // 原 Core 版本返回文件名，原 Utilities 版本返回全路径。
            // 建议：如果在库外，应该返回全路径或文件名，这里采用 Core 的文件名策略作为兜底，防止绝对路径泄露
            return Path.GetFileName(fullPath); 
        }
    }
}
