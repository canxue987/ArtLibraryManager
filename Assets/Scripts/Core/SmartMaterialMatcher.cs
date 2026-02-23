using System.IO;
using System.Linq;
using System.Collections.Generic;
using AssetLibrary.Core;
using UnityEngine;

namespace AssetLibrary.Utilities
{
    public static class SmartMaterialMatcher
    {
        /// <summary>
        /// 核心算法：传入模型名、材质名、一堆贴图，自动填空
        /// </summary>
        public static void AutoMatch(string modelName, List<string> allTexturePaths, MaterialSlotConfig slot)
        {
            if (allTexturePaths == null || allTexturePaths.Count == 0) return;

            string matName = slot.SlotName.ToLower();
            string modName = modelName.ToLower();

            // 1. 筛选候选池：贴图文件名必须包含 "模型名" 或 "材质名"
            // 例如：模型叫 Hero，材质叫 Skin。贴图 Hero_Skin_Albedo.png (命中) / Sword_Albedo.png (排除)
            var candidates = allTexturePaths.Where(path => 
            {
                string fName = Path.GetFileNameWithoutExtension(path).ToLower();
                return fName.Contains(matName) || fName.Contains(modName);
            }).ToList();

            if (candidates.Count == 0) return;

            // 2. 按照后缀规则匹配
            slot.BaseMapPath = FindBestMatch(candidates, NamingRules.BaseMap);
            slot.NormalMapPath = FindBestMatch(candidates, NamingRules.NormalMap);
            slot.OcclusionMapPath = FindBestMatch(candidates, NamingRules.AOMap);
            slot.EmissionMapPath = FindBestMatch(candidates, NamingRules.EmissionMap);
            
            // 简单处理 Metallic
            slot.MetallicMapPath = FindBestMatch(candidates, NamingRules.MaskMap);
            // 如果没找到 Mask，试着找 Specular/Metallic 单通道
            if (string.IsNullOrEmpty(slot.MetallicMapPath))
            {
                slot.MetallicMapPath = FindBestMatch(candidates, new[] { "_Metallic", "_Metal", "_Spec" });
            }
        }

        private static string FindBestMatch(List<string> candidates, string[] suffixes)
        {
            foreach (var path in candidates)
            {
                string fName = Path.GetFileNameWithoutExtension(path).ToLower();
                foreach (var suffix in suffixes)
                {
                    string s = suffix.ToLower();
                    // 优先全字匹配后缀 (e.g. Hero_Albedo)
                    if (fName.EndsWith(s)) return path;
                }
            }
            return "";
        }
    }
}
