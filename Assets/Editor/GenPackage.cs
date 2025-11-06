using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class GenPackage 
{
    [MenuItem("Tools/Build Asset Bundles")]
    static void BuildAllBundles()
    {
        string dir = "Assets/AssetBundles";
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        BuildPipeline.BuildAssetBundles(dir, BuildAssetBundleOptions.None, BuildTarget.StandaloneWindows64);
    }
}
