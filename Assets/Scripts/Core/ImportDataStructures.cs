using System;
using System.Collections.Generic;
using UnityEngine;

namespace AssetLibrary.Core
{
    // 批量导入中的单个任务（代表一个模型文件）
    [Serializable]
    public class ImportTask
    {
        public string SourceFilePath;      // 绝对路径
        public string FileName;            // 文件名 (e.g. "Hero")
        public string TargetName;          // 最终存入库的文件夹名 (默认=FileName，可改)
        
        public bool IsSelected = true;     // 是否勾选导入
        public bool IsExpanded = false;    // 在UI列表中是否展开显示材质详情
        
        // 材质槽位列表
        public List<MaterialSlotConfig> Slots = new List<MaterialSlotConfig>();

        // 状态标记
        public bool IsScanned = false;     // 是否已经读取过FBX内部信息(懒加载用)
    }

    // 单个材质槽位配置
    [Serializable]
    public class MaterialSlotConfig
    {
        public string SlotName;            // 模型原本的材质名 (e.g. "Skin_Mat")
        public WorkflowMode Workflow = WorkflowMode.Metallic;

        // 绑定的贴图源文件路径
        public string BaseMapPath;
        public string MetallicMapPath;     // 也是 Specular 流程的主贴图
        public string SmoothnessMapPath;
        public string NormalMapPath;
        public string OcclusionMapPath;
        public string EmissionMapPath;

        // UI 辅助：当前匹配状态
        public bool IsMatched => !string.IsNullOrEmpty(BaseMapPath) || !string.IsNullOrEmpty(NormalMapPath);
    }

    // 命名规则配置 (支持扩展)
    public static class NamingRules
    {
        public static readonly string[] BaseMap = { "_Albedo", "_BaseMap", "_Diffuse", "_Color", "_D" };
        public static readonly string[] MaskMap = { "_MS", "_MetallicSmoothness", "_Mask", "_MRAO" }; 
        public static readonly string[] NormalMap = { "_Normal", "_Nor", "_N" };
        public static readonly string[] AOMap = { "_AO", "_Occlusion", "_Occ" };
        public static readonly string[] EmissionMap = { "_Emission", "_Emit", "_E" };
    }
}
