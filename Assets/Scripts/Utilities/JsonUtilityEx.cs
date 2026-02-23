/* 
    文件: Assets/Scripts/Utilities/JsonUtilityEx.cs 
*/
using UnityEngine;

namespace AssetLibrary.Utilities
{
    public static class JsonUtilityEx
    {
        public static string ToJson<T>(T obj, bool prettyPrint = false)
        {
            return JsonUtility.ToJson(obj, prettyPrint);
        }

        public static T FromJson<T>(string json)
        {
            return JsonUtility.FromJson<T>(json);
        }
    }
}
