using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Siyuanshu.EditorTools
{
    /// <summary>
    /// URP 渲染管线自动配置（仅编辑器）。
    ///
    /// 为什么需要这个脚本？
    /// ─────────────────────────────────────────────────────────────
    /// 「把 URP 设为当前渲染管线」在 Unity 里必须靠一个
    /// <c>UniversalRenderPipelineAsset</c> 资源，并把它登记到
    /// GraphicsSettings / QualitySettings 上。
    ///
    /// 这个资源由 URP 包内的脚本类型创建，而该脚本的 GUID 随 URP 版本变化 ——
    /// 如果把这个 .asset 文件直接手写进仓库，GUID 一旦对不上，
    /// 打开工程就会看到「Missing Script」，而且可能连 ProjectSettings 都读不进来。
    ///
    /// 更稳的做法是：让 Unity 自己在首次打开工程时把这个资源建出来。
    /// 本脚本正是做这件事，并且全程使用**反射**访问 URP 类型 ——
    /// 这样即使工程里根本没有 URP 包，本文件也不会产生编译错误。
    ///
    /// 兜底策略：任何一步失败都只输出警告，绝不抛异常；
    /// 即使 URP 完全不可用，工程仍会以 Built-in 管线运行，
    /// 而 <c>QuaternionDemoController</c> 里的着色器回退逻辑会挑到 Standard，
    /// 演示效果丝毫不受影响（只是少了 URP 的后处理能力）。
    /// </summary>
    public static class URPAutoSetup
    {
        const string SettingsFolderParent = "Assets";
        const string SettingsFolderName = "Settings";
        const string SettingsDir = SettingsFolderParent + "/" + SettingsFolderName;
        const string RendererPath = SettingsDir + "/SiyuanshuURPRenderer.asset";
        const string PipelinePath = SettingsDir + "/SiyuanshuURPAsset.asset";
        const string ScenePath = "Assets/Scenes/QuaternionDemo.unity";

        // =================================================================
        // 入口
        // =================================================================

        /// <summary>
        /// 工程加载后自动执行一次。
        /// 只做「安全且静默」的事：补上 Build Settings 里的场景、必要时创建 URP 资源。
        /// 不在这里改色彩空间（那会触发全量重导入，交给菜单命令去做）。
        /// </summary>
        [InitializeOnLoadMethod]
        static void OnEditorLoad()
        {
            // 延后到编辑器空闲时执行，避开导入资产的时机
            EditorApplication.delayCall += () =>
            {
                try
                {
                    EnsureSceneInBuildSettings();
                    EnsurePipelineAsset(verbose: false);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[四元数演示] 自动配置时出现问题（不影响运行）：" + e.Message);
                }
            };
        }

        [MenuItem("四元数演示/一键完整配置（URP + 线性色彩空间 + 场景）")]
        static void MenuFullSetup()
        {
            try
            {
                EnsureSceneInBuildSettings();
                EnsureLinearColorSpace();
                EnsurePipelineAsset(verbose: true);
                AssetDatabase.SaveAssets();
                Debug.Log("[四元数演示] 配置完成。");
            }
            catch (Exception e)
            {
                Debug.LogError("[四元数演示] 配置失败：" + e);
            }
        }

        [MenuItem("四元数演示/仅配置 URP 渲染管线")]
        static void MenuPipelineOnly()
        {
            EnsurePipelineAsset(verbose: true);
        }

        [MenuItem("四元数演示/打开演示场景")]
        static void MenuOpenScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                Debug.LogWarning("[四元数演示] 没找到场景：" + ScenePath);
                return;
            }
            EditorSceneManager.OpenScene(ScenePath);
        }

        // =================================================================
        // 各项配置
        // =================================================================

        static void EnsureSceneInBuildSettings()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                return;
            }

            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            for (int i = 0; i < scenes.Length; i++)
            {
                if (scenes[i] != null && scenes[i].path == ScenePath) return;
            }

            List<EditorBuildSettingsScene> list = new List<EditorBuildSettingsScene>(scenes);
            list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();

            Debug.Log("[四元数演示] 已把 " + ScenePath + " 加入 Build Settings。");
        }

        static void EnsureLinearColorSpace()
        {
            if (PlayerSettings.colorSpace != ColorSpace.Linear)
            {
                PlayerSettings.colorSpace = ColorSpace.Linear;
                Debug.Log("[四元数演示] 色彩空间已切换为 Linear（URP 推荐设置）。");
            }
        }

        static bool EnsurePipelineAsset(bool verbose)
        {
            RenderPipelineAsset current = GraphicsSettings.defaultRenderPipeline;
            if (current != null)
            {
                if (verbose)
                {
                    Debug.Log("[四元数演示] 当前渲染管线已是：" + current.name + "，无需改动。");
                }
                return true;
            }

            Type rendererType = FindType("UnityEngine.Rendering.Universal.UniversalRendererData");
            Type pipelineType = FindType("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset");

            if (rendererType == null || pipelineType == null)
            {
                if (verbose)
                {
                    Debug.LogWarning(
                        "[四元数演示] 没找到 URP 类型（包可能尚未安装完成）。" +
                        "工程将以 Built-in 管线运行，演示本身完全正常。", null);
                }
                return false;
            }

            EnsureFolder();

            // ── 渲染器数据 ────────────────────────────────────────
            ScriptableObject rendererData = AssetDatabase.LoadAssetAtPath<ScriptableObject>(RendererPath);
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance(rendererType);
                rendererData.name = "SiyuanshuURPRenderer";
                AssetDatabase.CreateAsset(rendererData, RendererPath);
            }

            // ── 管线资源 ──────────────────────────────────────────
            ScriptableObject pipelineSO = AssetDatabase.LoadAssetAtPath<ScriptableObject>(PipelinePath);
            if (pipelineSO == null)
            {
                pipelineSO = CreatePipelineAsset(pipelineType, rendererType, rendererData);
                if (pipelineSO == null) return false;
                pipelineSO.name = "SiyuanshuURPAsset";
                AssetDatabase.CreateAsset(pipelineSO, PipelinePath);
            }

            AssetDatabase.SaveAssets();

            RenderPipelineAsset pipeline = pipelineSO as RenderPipelineAsset;
            if (pipeline == null)
            {
                Debug.LogWarning("[四元数演示] 创建出来的资源不是 RenderPipelineAsset，跳过登记。");
                return false;
            }

            // ── 登记到 GraphicsSettings 与所有质量等级 ────────────
            GraphicsSettings.defaultRenderPipeline = pipeline;
            TryAssignToAllQualityLevels(pipeline);

            MarkProjectSettingsDirty();

            if (verbose)
            {
                Debug.Log("[四元数演示] URP 已配置：" + PipelinePath);
            }
            return true;
        }

        static void TryAssignToAllQualityLevels(RenderPipelineAsset pipeline)
        {
            try
            {
                int original = QualitySettings.GetQualityLevel();
                string[] names = QualitySettings.names;
                for (int i = 0; i < names.Length; i++)
                {
                    QualitySettings.SetQualityLevel(i, false);
                    QualitySettings.renderPipeline = pipeline;
                }
                QualitySettings.SetQualityLevel(original, false);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[四元数演示] 质量等级的渲染管线设置失败（不影响运行）：" + e.Message);
            }
        }

        /// <summary>
        /// 用反射创建 UniversalRenderPipelineAsset：
        /// 优先用官方工厂方法 <c>Create(ScriptableRendererData)</c>，
        /// 拿不到就退回「直接实例化 + SerializedObject 写渲染器列表」。
        /// </summary>
        static ScriptableObject CreatePipelineAsset(Type pipelineType, Type rendererType, ScriptableObject rendererData)
        {
            try
            {
                MethodInfo create = pipelineType.GetMethod(
                    "Create",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { rendererType },
                    null);

                if (create != null)
                {
                    ScriptableObject created = create.Invoke(null, new object[] { rendererData }) as ScriptableObject;
                    if (created != null) return created;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[四元数演示] URP Create() 调用失败，改用备用方式：" + e.Message);
            }

            try
            {
                ScriptableObject so = ScriptableObject.CreateInstance(pipelineType);
                SerializedObject ser = new SerializedObject(so);

                SerializedProperty list = ser.FindProperty("m_RendererDataList");
                if (list != null)
                {
                    list.arraySize = 1;
                    list.GetArrayElementAtIndex(0).objectReferenceValue = rendererData;
                }

                SerializedProperty index = ser.FindProperty("m_DefaultRendererIndex");
                if (index != null) index.intValue = 0;

                ser.ApplyModifiedPropertiesWithoutUndo();
                return so;
            }
            catch (Exception e)
            {
                Debug.LogError("[四元数演示] 创建 URP 管线资源失败：" + e);
                return null;
            }
        }

        // =================================================================
        // 工具方法
        // =================================================================

        static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(SettingsDir))
            {
                AssetDatabase.CreateFolder(SettingsFolderParent, SettingsFolderName);
            }
        }

        /// <summary>
        /// 在已加载的程序集里找类型。先试程序集限定名（快），
        /// 失败则遍历所有程序集（慢但稳，不受程序集改名影响）。
        /// </summary>
        static Type FindType(string fullName)
        {
            Type t = Type.GetType(fullName + ", Unity.RenderPipelines.Universal.Runtime");
            if (t != null) return t;

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                try
                {
                    t = assemblies[i].GetType(fullName, false);
                    if (t != null) return t;
                }
                catch
                {
                    // 个别动态程序集可能抛异常，忽略即可
                }
            }
            return null;
        }

        /// <summary>
        /// GraphicsSettings 是 ProjectSettings 下的资源，
        /// 改完之后需要显式标脏并保存，否则改动可能在关闭编辑器时丢失。
        /// </summary>
        static void MarkProjectSettingsDirty()
        {
            string[] paths =
            {
                "ProjectSettings/GraphicsSettings.asset",
                "ProjectSettings/QualitySettings.asset"
            };

            for (int i = 0; i < paths.Length; i++)
            {
                UnityEngine.Object[] objs = AssetDatabase.LoadAllAssetsAtPath(paths[i]);
                if (objs == null) continue;
                for (int j = 0; j < objs.Length; j++)
                {
                    if (objs[j] != null) EditorUtility.SetDirty(objs[j]);
                }
            }

            AssetDatabase.SaveAssets();
        }
    }
}
