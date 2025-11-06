using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class ConverBuildInMatToURP : Editor
{
    [MenuItem("Tools/转换所有 Standard 材质到 URP Lit")]
    public static void ConvertMaterials()
    {
        string[] guids = AssetDatabase.FindAssets("t:Material"); // 查找所有材质资源
        int count = 0;
        Debug.Log($"[ConverBuildInMatToURP] : 正在检查 {guids.Length} 个材质。");

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null && mat.shader.name.StartsWith("Standard")) // 检查是否是Built-in Shader
            {
                Texture baseTex = mat.GetTexture("_MainTex");
                mat.shader = Shader.Find("Universal Render Pipeline/Lit"); // 修改为URP Lit Shader
                mat.SetTexture("_BaseMap", baseTex);
                count++;
                EditorUtility.SetDirty(mat); // 标记材质为已修改，以便保存更改
            }
        }
        AssetDatabase.SaveAssets(); // 保存所有修改过的资产
        AssetDatabase.Refresh(); // 刷新资产数据库以显示更改
        Debug.Log($"[ConverBuildInMatToURP] : 已转换 {count} 个材质。");
    }
}
