using UnityEngine;
using UnityEngine.Rendering;

namespace Siyuanshu
{
    /// <summary>
    /// 四元数旋转变换演示 —— 主控制器。
    ///
    /// 本类是整个工程的核心，负责把原始 MFC + OpenGL 程序 <c>anweihua</c> 中
    /// <c>CanweihuaView::OnTimer</c> 的逻辑逐行搬到 Unity：
    ///
    /// <code>
    /// // 原程序 anweihuaView.cpp —— OnTimer
    /// Quaternion_begin.SetEulerAngle(m_xrotation_begin, m_yrotation_begin, m_zrotation_begin);
    /// Quaternion_end  .SetEulerAngle(m_xrotation_end,   m_yrotation_end,   m_zrotation_end);
    ///
    /// float temp = begin.qw*end.qw + begin.qx*end.qx + begin.qy*end.qy + begin.qz*end.qz;
    /// if (temp &lt; 0) { Quaternion_end.qw *= -1; ... }   // 双覆盖修正
    ///
    /// if (t &lt; 1.0) t += 0.005;
    ///
    /// Quaternion_middle = (1-t)*begin + t*end;              // ① 线性插值
    /// Quaternion_middle.qw /= sqrt(...);                    // ② 归一化
    /// ...
    /// Quaternion_middle.GetEulerAngle(m_xrotation, m_yrotation, m_zrotation);
    /// </code>
    ///
    /// ① + ② 合起来就是 **NLerp（归一化线性插值）**，不是 SLERP。
    /// 详见 <see cref="Q4.Nlerp"/> 的注释。
    ///
    /// ── 与原程序的工程性差异（README「已知差异」有完整列表）─────────
    ///   · 原程序用 <c>SetTimer(0, 0.33)</c> 按「每次定时器跳 0.005」推进，
    ///     跳的节奏取决于 MFC 定时器粒度与帧率，不同机器速度不同。
    ///     本实现保留 0.005 的**步长语义**，但换算成按真实时间推进
    ///     （以 60FPS 为基准），保证任何帧率下动画时长一致。
    ///   · 原程序把插值结果反解成欧拉角后再交给 glRotatef 渲染。
    ///     由于 SetEulerAngle / GetEulerAngle 互为逆运算，
    ///     这条路径在不发生万向锁时与直接用四元数**完全等价**。
    ///     本实现直接用四元数驱动 Transform（避免万向锁附近的数值跳变影响画面），
    ///     同时仍然调用 GetEulerAngle 并把结果显示在 HUD 上 ——
    ///     于是那个「四元数平滑、欧拉角跳变」的现象可以被直接观察到。
    /// </summary>
    [DisallowMultipleComponent]
    public class QuaternionDemoController : MonoBehaviour
    {
        // =================================================================
        // 与原程序逐项对应的参数
        // =================================================================

        /// <summary>每次插值推进的步长，对应原程序 <c>t += 0.005</c>。</summary>
        public const float TimeStep = 0.005f;

        /// <summary>基准帧率：把「每跳 0.005」换算成「每秒推进多少」。</summary>
        public const float BaseTicksPerSecond = 60f;

        /// <summary>起始姿态欧拉角（度），对应 m_xrotation_begin / _y / _z。</summary>
        public static readonly Vector3 BeginEuler = new Vector3(0f, 90f, 0f);

        /// <summary>结束姿态欧拉角（度），对应 m_xrotation_end / _y / _z。</summary>
        public static readonly Vector3 EndEuler = new Vector3(90f, 45f, 90f);

        /// <summary>茶壶缩放，对应原程序 <c>auxSolidTeapot(0.5)</c> 的 0.5。</summary>
        public const float TeapotScale = 0.5f;

        /// <summary>每个 patch 的细分数。</summary>
        public const int TeapotSegments = 12;

        /// <summary>正交相机半高，对应原程序 <c>glOrtho(-2*aspect, 2*aspect, -2, 2, ...)</c> 的 2。</summary>
        public const float CameraOrthoSize = 2f;

        /// <summary>鼠标拖拽灵敏度（度 / 像素）。</summary>
        public const float DragSensitivity = 0.6f;

        // =================================================================
        // 运行时状态（HUD 会读取这些字段做显示）
        // =================================================================

        /// <summary>插值参数 t，原程序成员变量 <c>float t</c>。</summary>
        public float T { get; private set; }

        /// <summary>插值是否还在推进（true = 自动播放中）。</summary>
        public bool IsPlaying { get; private set; } = true;

        /// <summary>鼠标是否正在接管旋转。</summary>
        public bool IsDragging { get; private set; }

        /// <summary>起始姿态四元数，对应 <c>Quaternion_begin</c>。</summary>
        public Q4 QBgn { get; private set; }

        /// <summary>结束姿态四元数，对应 <c>Quaternion_end</c>（可能已被双覆盖修正取反）。</summary>
        public Q4 QEnd { get; private set; }

        /// <summary>插值结果四元数，对应 <c>Quaternion_middle</c>。</summary>
        public Q4 QMid { get; private set; }

        /// <summary>原始的点积值（未做符号修正前），对应原程序局部变量 <c>temp</c>。</summary>
        public float DotValue { get; private set; }

        /// <summary>是否触发了双覆盖符号修正（原程序 <c>if (temp &lt; 0)</c> 分支）。</summary>
        public bool SignFlipped { get; private set; }

        /// <summary>把 <see cref="QMid"/> 反解回欧拉角的结果（度），对应 <c>GetEulerAngle</c> 的输出。</summary>
        public Vector3 EulerFromQuaternion { get; private set; }

        /// <summary>HUD 面板是否压在鼠标下方（由 <see cref="DemoHud"/> 每帧写入，避免拖拽与 UI 冲突）。</summary>
        [System.NonSerialized] public bool MouseOverHud;

        // =================================================================
        // 内部字段
        // =================================================================

        Transform _teapot;
        Camera _camera;
        Light _keyLight;

        // 拖拽相关
        bool _dragButtonHeld;
        Vector3 _dragStartMouse;
        UnityEngine.Quaternion _dragStartRotation;

        // =================================================================
        // 生命周期
        // =================================================================

        void Awake()
        {
            BuildScene();
            EnsureHud();
            ResetDemo();
        }

        /// <summary>保证场景里存在 HUD 面板，并把它连到本控制器上。</summary>
        void EnsureHud()
        {
            DemoHud hud = GetComponent<DemoHud>();
            if (hud == null)
            {
                hud = gameObject.AddComponent<DemoHud>();
            }
            hud.controller = this;
        }

        void Update()
        {
            HandleKeyboard();
            HandleDrag();

            if (!IsDragging && IsPlaying)
            {
                // 保留原程序「每跳 +0.005」的步长语义，但按真实时间推进
                float dt = Mathf.Min(Time.deltaTime, 0.1f);
                T += TimeStep * dt * BaseTicksPerSecond;
                if (T >= 1f)
                {
                    T = 1f;
                    IsPlaying = false;   // 原程序 t 到 1 后不再推进（不循环）
                }
            }

            EvaluateQuaternion();

            if (!IsDragging)
            {
                ApplyToTeapot(QMid.ToUnityQuaternion());
            }
        }

        // =================================================================
        // 场景搭建（相机 / 灯光 / 茶壶 / 材质）
        // =================================================================

        void BuildScene()
        {
            // ── 相机：还原原程序的正交投影与白色背景 ────────────────
            if (_camera == null)
            {
                _camera = FindExistingCamera();
            }

            if (_camera == null)
            {
                GameObject camGO = new GameObject("演示相机 (对应原程序 glOrtho 正交投影)");
                camGO.transform.SetParent(transform, false);
                _camera = camGO.AddComponent<Camera>();
            }

            _camera.orthographic = true;
            _camera.orthographicSize = CameraOrthoSize;   // glOrtho 的 ±2
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 100f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.white;        // glClearColor(1,1,1,1)
            _camera.transform.position = new Vector3(0f, 0f, -6f);
            _camera.transform.rotation = UnityEngine.Quaternion.identity;

            // ── 光照：还原 glEnable(GL_LIGHT0)，位置 (1,1,1)，白色 ──
            if (_keyLight == null)
            {
                _keyLight = FindExistingDirectionalLight();
            }

            if (_keyLight == null)
            {
                GameObject lightGO = new GameObject("主光源 (对应原程序 GL_LIGHT0)");
                lightGO.transform.SetParent(transform, false);
                _keyLight = lightGO.AddComponent<Light>();
                _keyLight.type = LightType.Directional;
                _keyLight.color = Color.white;
                _keyLight.intensity = 1f;
                _keyLight.shadows = LightShadows.None;
                // 原程序 light_position = {1,1,1,0}，方向光朝向 = 从 (1,1,1) 射向原点
                _keyLight.transform.rotation =
                    UnityEngine.Quaternion.LookRotation(new Vector3(-1f, -1f, -1f).normalized, Vector3.up);
            }

            // 环境光：原程序 light_ambient = {0.1,0.1,0.1,1}
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.1f, 0.1f, 0.1f, 1f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.skybox = null;   // 原程序没有天空盒，纯白背景

            // ── 茶壶 ────────────────────────────────────────────────
            if (_teapot == null)
            {
                GameObject teapotGO = new GameObject("Utah 茶壶 (对应原程序 auxSolidTeapot)");
                teapotGO.transform.SetParent(transform, false);
                _teapot = teapotGO.transform;

                MeshFilter mf = teapotGO.AddComponent<MeshFilter>();
                mf.sharedMesh = TeapotMeshBuilder.Build(TeapotSegments, true);

                MeshRenderer mr = teapotGO.AddComponent<MeshRenderer>();
                mr.sharedMaterial = CreateTeapotMaterial();

                teapotGO.transform.localScale = Vector3.one * TeapotScale;
            }
        }

        static Camera FindExistingCamera()
        {
            // Camera.main 依赖 "MainCamera" 标签，这里直接找场景里任意一台相机，
            // 场景若已自带相机（例如别人往场景里拖了一台）就复用它
            Camera[] cams = Object.FindObjectsOfType<Camera>();
            if (cams != null && cams.Length > 0) return cams[0];
            return null;
        }

        static Light FindExistingDirectionalLight()
        {
            Light[] lights = Object.FindObjectsOfType<Light>();
            if (lights == null) return null;
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null && lights[i].type == LightType.Directional) return lights[i];
            }
            return null;
        }

        /// <summary>
        /// 创建茶壶材质，还原原程序的 <c>glMaterialfv</c> 设置：
        /// ambient(0.1,0.1,0.1) / diffuse(0.5,0.3,0.1) / specular(0.9,0.9,0.9)
        /// / emission(0.5,0,0) / shininess(30)。
        ///
        /// 着色器按 URP Lit → URP Simple Lit → Standard → Legacy Diffuse 依次回退，
        /// 这样无论工程处于 URP 还是 Built-in 管线都不会出现「粉色材质」。
        /// </summary>
        static Material CreateTeapotMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Legacy Shaders/Diffuse");
            if (shader == null) shader = Shader.Find("Hidden/InternalErrorShader");

            Material mat = new Material(shader);
            mat.name = "茶壶材质 (来自原程序 glMaterialfv)";

            Color diffuse = new Color(0.5f, 0.3f, 0.1f, 1f);
            Color emission = new Color(0.5f, 0f, 0f, 1f);

            // URP Lit
            SetColorIfExists(mat, "_BaseColor", diffuse);
            SetFloatIfExists(mat, "_Metallic", 0f);
            SetFloatIfExists(mat, "_Smoothness", 0.5f);      // shininess 30 → 约 0.5 的等价观感
            SetColorIfExists(mat, "_EmissionColor", emission);
            SetFloatIfExists(mat, "_Surface", 0f);           // 不透明
            // Built-in Standard
            SetColorIfExists(mat, "_Color", diffuse);
            SetFloatIfExists(mat, "_Glossiness", 0.5f);

            // emission 需要显式打开关键字才会生效
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            // 关闭背面剔除（_Cull = 0 表示 Off）。
            // OpenGL 默认不开启 GL_CULL_FACE，原程序也没有启用它 —— 两侧都画。
            // 这样做同时让三角形绕序不影响可见性，避免因绕序判定错误导致模型完全消失。
            SetFloatIfExists(mat, "_Cull", 0f);

            return mat;
        }

        static void SetColorIfExists(Material m, string prop, Color c)
        {
            if (m.HasProperty(prop)) m.SetColor(prop, c);
        }

        static void SetFloatIfExists(Material m, string prop, float v)
        {
            if (m.HasProperty(prop)) m.SetFloat(prop, v);
        }

        // =================================================================
        // 核心：四元数求值（对应原程序 OnTimer 的整段逻辑）
        // =================================================================

        void EvaluateQuaternion()
        {
            // ① 两个姿态的欧拉角 → 四元数（原程序每 tick 都重算一次）
            QBgn = Q4.FromEuler(BeginEuler.x, BeginEuler.y, BeginEuler.z);
            QEnd = Q4.FromEuler(EndEuler.x, EndEuler.y, EndEuler.z);

            // ② 双覆盖修正：判断两个四元数的夹角是否超过 180°
            //    q 与 -q 表示同一个旋转，若不修正，插值会绕「远路」转一大圈。
            DotValue = Q4.Dot(QBgn, QEnd);
            SignFlipped = DotValue < 0f;
            if (SignFlipped)
            {
                QEnd = QEnd.Negated();
            }

            // ③ NLerp：线性插值 + 归一化（注意：原程序不是 SLERP）
            QMid = Q4.Nlerp(QBgn, QEnd, Mathf.Clamp01(T));

            // ④ 反解欧拉角 —— 原程序用它来驱动 glRotatef。
            //    本实现只用它做 HUD 显示，因为这条反解在万向锁附近会跳变。
            float ex, ey, ez;
            QMid.GetEulerAngle(out ex, out ey, out ez);
            EulerFromQuaternion = new Vector3(ex, ey, ez);
        }

        void ApplyToTeapot(UnityEngine.Quaternion q)
        {
            if (_teapot != null) _teapot.localRotation = q;
        }

        // =================================================================
        // 鼠标拖拽接管
        // =================================================================

        void HandleDrag()
        {
            // 鼠标按下（左键）且不在 HUD 面板上 → 开始接管
            if (Input.GetMouseButtonDown(0) && !MouseOverHud)
            {
                // 已经开始拖了就不再重置起点
                if (!_dragButtonHeld)
                {
                    _dragButtonHeld = true;
                    IsDragging = true;
                    _dragStartMouse = Input.mousePosition;
                    _dragStartRotation = (_teapot != null)
                        ? _teapot.localRotation
                        : UnityEngine.Quaternion.identity;
                }
            }

            if (_dragButtonHeld && IsDragging)
            {
                Vector3 delta = Input.mousePosition - _dragStartMouse;

                // 水平拖动绕世界 Y 轴（偏航），垂直拖动绕世界 X 轴（俯仰）
                UnityEngine.Quaternion yaw =
                    UnityEngine.Quaternion.AngleAxis(delta.x * DragSensitivity, Vector3.up);
                UnityEngine.Quaternion pitch =
                    UnityEngine.Quaternion.AngleAxis(-delta.y * DragSensitivity, Vector3.right);

                ApplyToTeapot(yaw * pitch * _dragStartRotation);
            }

            // 松开 → 结束接管，自动播放从当前 t 继续
            if (_dragButtonHeld && !Input.GetMouseButton(0))
            {
                _dragButtonHeld = false;
                IsDragging = false;
            }
        }

        void HandleKeyboard()
        {
            // 空格：暂停 / 继续
            if (Input.GetKeyDown(KeyCode.Space))
            {
                TogglePlay();
            }

            // R：重播
            if (Input.GetKeyDown(KeyCode.R))
            {
                Replay();
            }
        }

        // =================================================================
        // 对外控制（HUD 按钮调用）
        // =================================================================

        /// <summary>重播：t 归零并开始自动播放（对应原程序重新运行一次）。</summary>
        public void Replay()
        {
            T = 0f;
            IsPlaying = true;
            EvaluateQuaternion();
            ApplyToTeapot(QMid.ToUnityQuaternion());
        }

        /// <summary>暂停 / 继续自动播放。</summary>
        public void TogglePlay()
        {
            // 原程序播到 t=1 就停了，此时再按「继续」应从头开始
            if (!IsPlaying && T >= 1f)
            {
                Replay();
                return;
            }
            IsPlaying = !IsPlaying;
        }

        /// <summary>把 t 归零并停在起始姿态。</summary>
        public void ResetDemo()
        {
            T = 0f;
            IsPlaying = true;
            IsDragging = false;
            _dragButtonHeld = false;
            EvaluateQuaternion();
            ApplyToTeapot(QMid.ToUnityQuaternion());
        }

        /// <summary>当前 t 对应的进度百分比，供 HUD 显示。</summary>
        public float ProgressPercent
        {
            get { return Mathf.Clamp01(T) * 100f; }
        }
    }
}
