using UnityEngine;

namespace Siyuanshu
{
    /// <summary>
    /// 启动兜底：保证演示一定能跑起来。
    ///
    /// 背景说明
    /// ─────────────────────────────────────────────────────────────
    /// 本工程的场景 <c>Assets/Scenes/QuaternionDemo.unity</c> 里已经挂了
    /// <see cref="QuaternionDemoController"/>，正常情况下是它自己在 Awake 里
    /// 搭出相机、灯光、茶壶和 UI。
    ///
    /// 但「场景里的脚本引用」依赖 .meta 里的 GUID 与场景 YAML 对得上。
    /// 万一工程在版本控制里被折腾过、或者有人误删了场景里的物体，
    /// 引用就会断掉，按 Play 之后什么都不发生 —— 而且没有任何报错提示，
    /// 排查起来很费时间。
    ///
    /// 所以这里再加一层：场景加载完成后检查一遍，
    /// 如果找不到控制器就现场创建一个。
    /// 这样即使在**任意一个空场景**里按 Play，演示也会正常出现。
    /// </summary>
    public static class DemoBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureDemoExists()
        {
            // 场景里已经有一个（正常情况）→ 什么都不做
            if (Object.FindObjectOfType<QuaternionDemoController>() != null)
            {
                return;
            }

            GameObject go = new GameObject("四元数演示 (运行时自动创建)");
            go.AddComponent<QuaternionDemoController>();

            // 控制器会在 Awake 中自动挂上 DemoHud，这里不需要再处理
            Debug.Log("[四元数演示] 场景中没有找到 QuaternionDemoController，已自动创建。" +
                      "（这说明场景里的脚本引用可能断了，但演示仍可正常运行。）");
        }
    }
}
