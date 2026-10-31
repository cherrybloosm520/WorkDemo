using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Demo.Editor
{
    /// <summary>
    /// 项目一次性初始化脚本:
    /// 1. 创建 URP 资产并挂载到 Graphics / Quality 设置
    /// 2. 启用新版 Input System(同时保留旧输入,兼容第三方资产)
    /// 3. 导入 TMP Essentials
    /// 通过命令行执行:Unity.exe -batchmode -quit -projectPath ... -executeMethod Demo.Editor.ProjectSetup.Run
    /// </summary>
    public static class ProjectSetup
    {
        [MenuItem("Demo/项目初始化(幂等,可重复执行)")]
        public static void Run()
        {
            CreateUrpAsset();
            EnableInputSystem();
            ImportTmpEssentials();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[ProjectSetup] 全部完成:URP 已挂载、Input System 已启用、TMP Essentials 已导入。");
        }

        private static void CreateUrpAsset()
        {
            const string folder = "Assets/Settings";
            const string rendererPath = folder + "/URP-Renderer.asset";
            const string assetPath = folder + "/URP-Asset.asset";

            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets", "Settings");

            // 已存在则复用,不重复创建
            var existing = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
            if (existing != null)
            {
                GraphicsSettings.defaultRenderPipeline = existing;
                QualitySettings.renderPipeline = existing;
                Debug.Log("[ProjectSetup] URP 资产已存在,直接挂载。");
                return;
            }

            var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(rendererData, rendererPath);

            var urpAsset = UniversalRenderPipelineAsset.Create(rendererData);
            AssetDatabase.CreateAsset(urpAsset, assetPath);

            GraphicsSettings.defaultRenderPipeline = urpAsset;
            QualitySettings.renderPipeline = urpAsset;

            Debug.Log("[ProjectSetup] URP 资产已创建并挂载到 Graphics / Quality。");
        }

        private static void EnableInputSystem()
        {
            // ProjectSettings.asset 里的 activeInputHandler:0=旧输入,1=新输入,2=两者共存
            var settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (settings.Length == 0)
            {
                Debug.LogError("[ProjectSetup] 找不到 ProjectSettings.asset");
                return;
            }

            var serialized = new SerializedObject(settings[0]);
            var prop = serialized.FindProperty("activeInputHandler");
            if (prop != null && prop.intValue != 2)
            {
                prop.intValue = 2;
                serialized.ApplyModifiedProperties();
                Debug.Log("[ProjectSetup] activeInputHandler 已设为 2(新旧输入共存)。");
            }
            else
            {
                Debug.Log("[ProjectSetup] activeInputHandler 已经是目标值,跳过。");
            }
        }

        private static void ImportTmpEssentials()
        {
            // 通过 PackageManager API 拿到 TMP 包在磁盘上的实际路径(而不是直接拼 Packages 虚拟路径)
            var info = UnityEditor.PackageManager.PackageInfo.FindForPackageName("com.unity.textmeshpro");
            if (info == null)
            {
                Debug.LogWarning("[ProjectSetup] 未找到 com.unity.textmeshpro 包,跳过 TMP Essentials 导入。");
                return;
            }

            var packagePath = Path.Combine(info.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage");
            if (!File.Exists(packagePath))
            {
                Debug.LogWarning("[ProjectSetup] TMP 资源包不存在:" + packagePath);
                return;
            }

            AssetDatabase.ImportPackage(packagePath, false);
            Debug.Log("[ProjectSetup] TMP Essentials 已导入。");
        }
    }
}
