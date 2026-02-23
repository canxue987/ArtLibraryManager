using System;
using System.Collections.Generic;

namespace AssetLibrary.Core
{
    // 1. 定义工作流枚举 (这是缺失的部分)
    public enum WorkflowMode 
    { 
        Metallic, 
        Specular 
    }

    [System.Serializable]
    public class AssetMetaData
    {
        public string Name;
        public string RelativePath; 
        public AssetType Type;
        public string ThumbnailPath; 

        public List<string> Tags = new List<string>();
        public string Description;

        // [旧] 单材质支持 (保留以兼容旧数据)
        public MaterialBindings Materials;

        // [新] 多材质支持：存储每个材质球名字对应的绑定配置
        public List<AssetMaterialSetting> MultiMaterials = new List<AssetMaterialSetting>();

        public AssetMetaData()
        {
            Tags = new List<string>();
            Description = "";
            Materials = new MaterialBindings();
            MultiMaterials = new List<AssetMaterialSetting>();
        }
    }

    // [新] 单个材质球的存档配置
    [System.Serializable]
    public class AssetMaterialSetting
    {
        public string MaterialName; // 模型里的材质球名字 (e.g. "Body", "Face")
        public MaterialBindings Bindings; // 对应的贴图配置

        public AssetMaterialSetting() { Bindings = new MaterialBindings(); }
    }

    /// <summary>
    /// 管理模型的 URP Lit 材质贴图绑定信息
    /// 存储各个材质贴图的相对路径，允许用户自定义赋予
    /// </summary>
    [System.Serializable]
    public class MaterialBindings
    {
        // 这里引用了 WorkflowMode
        public WorkflowMode Workflow = WorkflowMode.Metallic;  
        
        /// <summary>基础颜色贴图</summary>
        public string BaseMapPath;
        
        /// <summary>金属度贴图（R通道）</summary>
        public string MetallicMapPath;
        
        /// <summary>光滑度贴图（A通道），可与MetallicMap共用一张或单独提供</summary>
        public string SmoothnessMapPath;
        
        /// <summary>法线贴图</summary>
        public string NormalMapPath;
        
        /// <summary>高度贴图</summary>
        public string HeightMapPath;
        
        /// <summary>环境光遮蔽贴图</summary>
        public string OcclusionMapPath;
        
        /// <summary>自发光贴图</summary>
        public string EmissionMapPath;

        public MaterialBindings()
        {
            BaseMapPath = "";
            MetallicMapPath = "";
            SmoothnessMapPath = "";
            NormalMapPath = "";
            HeightMapPath = "";
            OcclusionMapPath = "";
            EmissionMapPath = "";
        }
    }
}
