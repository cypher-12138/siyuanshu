using UnityEngine;

namespace Siyuanshu
{
    /// <summary>
    /// 屏幕左上角的信息面板（IMGUI / OnGUI 实现）。
    ///
    /// 为什么用 OnGUI 而不是 uGUI / TextMeshPro？
    /// ─────────────────────────────────────────────────────────────
    ///   1. 不依赖任何预制体、Canvas、EventSystem 或字体资源，
    ///      克隆仓库后直接 Play 就有界面，符合「开箱即用」。
    ///   2. 中文显示需要带 CJK 字形的字体。uGUI 用内置 Arial 会把中文
    ///      渲染成方框；这里用 <see cref="Font.CreateDynamicFontFromOSFont(string[], int)"/>
    ///      直接取操作系统字体（微软雅黑 / 黑体 / 宋体……），Windows 上必然存在。
    ///
    /// 面板显示的内容刻意与原程序 <c>CanweihuaView::OnTimer</c> 里的中间变量一一对应，
    /// 方便对照阅读。
    /// </summary>
    [DisallowMultipleComponent]
    public class DemoHud : MonoBehaviour
    {
        /// <summary>面板矩形（IMGUI 坐标系：左上角为原点，Y 轴向下）。</summary>
        public static readonly Rect PanelRect = new Rect(12f, 12f, 452f, 616f);

        /// <summary>要显示的控制器，由 <see cref="QuaternionDemoController"/> 创建自己时赋值。</summary>
        [System.NonSerialized] public QuaternionDemoController controller;

        Font _font;
        GUIStyle _box, _title, _text, _mono, _hint, _button;

        bool _stylesReady;

        // =================================================================

        void EnsureStyles()
        {
            if (_stylesReady && _box != null) return;

            if (_font == null)
            {
                _font = CreateChineseFont(14);
            }

            _box = new GUIStyle(GUI.skin.box);
            _title = new GUIStyle(GUI.skin.label);
            _text = new GUIStyle(GUI.skin.label);
            _mono = new GUIStyle(GUI.skin.label);
            _hint = new GUIStyle(GUI.skin.label);
            _button = new GUIStyle(GUI.skin.button);

            if (_font != null)
            {
                _box.font = _font;
                _title.font = _font;
                _text.font = _font;
                _mono.font = _font;
                _hint.font = _font;
                _button.font = _font;
            }

            _title.fontSize = 16;
            _title.fontStyle = FontStyle.Bold;
            _title.normal.textColor = new Color(0.10f, 0.12f, 0.16f);

            _text.fontSize = 13;
            _text.normal.textColor = new Color(0.15f, 0.17f, 0.21f);
            _text.wordWrap = false;

            _mono.fontSize = 13;
            _mono.normal.textColor = new Color(0.10f, 0.32f, 0.55f);
            _mono.wordWrap = false;

            _hint.fontSize = 12;
            _hint.normal.textColor = new Color(0.42f, 0.44f, 0.48f);
            _hint.wordWrap = true;

            _button.fontSize = 13;

            _stylesReady = true;
        }

        /// <summary>
        /// 取一个含中文字形的系统字体。
        /// 按 Windows → macOS → Linux 的常见中文字体名依次尝试。
        /// </summary>
        static Font CreateChineseFont(int size)
        {
            string[] candidates =
            {
                "Microsoft YaHei UI", "Microsoft YaHei", "微软雅黑",
                "SimHei", "黑体",
                "SimSun", "宋体",
                "Noto Sans CJK SC", "Source Han Sans SC", "WenQuanYi Micro Hei",
                "PingFang SC", "Hiragino Sans GB", "STHeiti",
                "Arial Unicode MS"
            };

            Font f = null;
            try
            {
                f = Font.CreateDynamicFontFromOSFont(candidates, size);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[四元数演示] 创建中文字体失败，将回退到内置字体：" + e.Message);
            }

            if (f == null)
            {
                try { f = Font.CreateDynamicFontFromOSFont("Arial", size); }
                catch { /* 忽略：后面会用内置字体兜底 */ }
            }

            if (f == null)
            {
                Debug.LogWarning("[四元数演示] 未找到可用的系统中文字体，界面中文可能显示为方框。");
            }

            return f;
        }

        // =================================================================

        void OnGUI()
        {
            if (controller == null) return;

            EnsureStyles();

            // 记录鼠标是否悬停在面板上，供控制器避免「点 UI 却把物体转了」
            Vector2 guiMouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            controller.MouseOverHud = PanelRect.Contains(guiMouse);

            GUI.Box(PanelRect, GUIContent.none, _box);

            GUILayout.BeginArea(new Rect(
                PanelRect.x + 12f,
                PanelRect.y + 10f,
                PanelRect.width - 24f,
                PanelRect.height - 20f));

            DrawContent();

            GUILayout.EndArea();
        }

        void DrawContent()
        {
            var c = controller;

            GUILayout.Label("四元数旋转变换演示", _title);
            GUILayout.Label("移植自 Visual Studio (MFC + OpenGL) 工程 anweihua", _hint);
            GUILayout.Space(8f);

            // ── 播放状态 ────────────────────────────────────────────
            string stateText;
            if (c.IsDragging) stateText = "鼠标接管中（松开鼠标恢复自动播放）";
            else if (!c.IsPlaying && c.T >= 1f) stateText = "已完成（原程序播完即停，不循环）";
            else if (!c.IsPlaying) stateText = "已暂停";
            else stateText = "自动播放中";

            GUILayout.Label("状态：" + stateText, _text);
            GUILayout.Label(string.Format("插值参数 t = {0:F4}   （{1:F1}%）", c.T, c.ProgressPercent), _mono);
            GUILayout.Label("步长：每帧 +0.005（对应原程序 t += 0.005）", _hint);
            GUILayout.Space(8f);

            // ── 起止姿态 ────────────────────────────────────────────
            GUILayout.Label("── 起始姿态（m_xrotation_begin 等）──", _text);
            GUILayout.Label(string.Format("欧拉角：x={0:F4}  y={1:F4}  z={2:F4}",
                QuaternionDemoController.BeginEuler.x,
                QuaternionDemoController.BeginEuler.y,
                QuaternionDemoController.BeginEuler.z), _text);
            GUILayout.Label("Quaternion_begin  " + c.QBgn.ToDisplayString(), _mono);
            GUILayout.Space(6f);

            GUILayout.Label("── 结束姿态（m_xrotation_end 等）──", _text);
            GUILayout.Label(string.Format("欧拉角：x={0:F4}  y={1:F4}  z={2:F4}",
                QuaternionDemoController.EndEuler.x,
                QuaternionDemoController.EndEuler.y,
                QuaternionDemoController.EndEuler.z), _text);
            GUILayout.Label("Quaternion_end    " + c.QEnd.ToDisplayString(), _mono);
            GUILayout.Label(string.Format("点积 temp = {0:F6}  →  {1}",
                c.DotValue,
                c.SignFlipped ? "temp < 0，已把 q_end 取反（走短弧）" : "temp ≥ 0，无需取反"), _hint);
            GUILayout.Space(8f);

            // ── 插值结果 ────────────────────────────────────────────
            GUILayout.Label("── 插值结果（NLerp：线性插值 + 归一化）──", _text);
            GUILayout.Label("Quaternion_middle " + c.QMid.ToDisplayString(), _mono);
            GUILayout.Label(string.Format("模长 |q| = {0:F6}   （归一化后恒为 1）", c.QMid.Magnitude), _mono);
            GUILayout.Label("反解欧拉角：" + string.Format("x={0:F4}  y={1:F4}  z={2:F4}",
                c.EulerFromQuaternion.x, c.EulerFromQuaternion.y, c.EulerFromQuaternion.z), _text);
            GUILayout.Label(
                "注意上行的「反解欧拉角」：起始姿态 y=90° 正是万向锁位置，" +
                "那里 X 与 Z 的解不唯一；四元数一路平滑，反解出的欧拉角却可能突跳。",
                _hint);
            GUILayout.Space(10f);

            // ── 按钮 ────────────────────────────────────────────────
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("重播 (R)", _button, GUILayout.Height(26f), GUILayout.Width(120f)))
            {
                c.Replay();
            }
            string toggleLabel = (c.IsPlaying && c.T < 1f) ? "暂停 (空格)" : "播放 (空格)";
            if (GUILayout.Button(toggleLabel, _button, GUILayout.Height(26f), GUILayout.Width(130f)))
            {
                c.TogglePlay();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            GUILayout.Label("操作：按住鼠标左键拖动可自由旋转茶壶（此时暂停插值），松开恢复播放。", _hint);

            GUILayout.Space(6f);
            GUILayout.Label(
                "说明：原程序用的是 NLerp（线性插值后归一化），不是 SLERP；" +
                "两者都从起点转到终点，但 NLerp 的角速度不恒定。本演示忠实保留原做法。",
                _hint);
        }
    }
}
