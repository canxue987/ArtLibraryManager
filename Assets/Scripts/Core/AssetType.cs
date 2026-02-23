/* 
    文件: Assets/Scripts/Core/AssetType.cs 
    功能: 资源类型枚举
*/
namespace AssetLibrary.Core
{
    public enum AssetType
    {
        Unknown = 0,
        Model,    // .fbx, .obj, .blend
        Texture,  // .png, .jpg, .tga
        Sound,    // .wav, .mp3
        Effect,
        VFX       // .prefab, .mat
    }
}
