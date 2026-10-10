using UnityEngine;

namespace Siyuanshu
{
    /// <summary>
    /// 四元数：与原始 MFC + OpenGL 程序 <c>anweihua</c> 中
    /// <c>Quaternion.h</c> / <c>Quaternion.cpp</c> 的实现**一一对应**。
    ///
    /// 字段名刻意保留原程序的 <c>qw / qx / qy / qz</c>，
    /// 这样把本文件与原版并排阅读时可以逐行对照。
    ///
    /// ── 原程序的数学约定（本类完整保留）──────────────────────────────
    ///   1. 欧拉角 → 四元数 使用 **XYZ 内旋**（等价于外旋 ZYX）公式。
    ///   2. 反解 asin 之前对入参做 [-1, 1] 钳制，防止浮点误差产生 NaN。
    ///   3. 角度输入/输出统一为「度」。
    ///
    /// ── 与原程序的差异（仅此两处，均为工程性修正）──────────────────
    ///   1. 原程序用 <c>/ 180.0 * 3.14159</c> 这种低精度常数手写换算，
    ///      本实现改用 <see cref="Mathf.Deg2Rad"/> / <see cref="Mathf.Rad2Deg"/>。
    ///   2. 原程序把钳制写成宏 <c>CLAMP</c>，本实现使用 <see cref="Mathf.Clamp"/>。
    ///   两处都不改变数学语义，只提高精度与可读性。
    /// </summary>
    public struct Q4
    {
        // ── 四元数分量（对应原程序 Quaternion::qw / qx / qy / qz）─────────
        public float qw;
        public float qx;
        public float qy;
        public float qz;

        public Q4(float w, float x, float y, float z)
        {
            qw = w;
            qx = x;
            qy = y;
            qz = z;
        }

        /// <summary>单位四元数 (1, 0, 0, 0)，对应「无旋转」。</summary>
        public static Q4 Identity
        {
            get { return new Q4(1f, 0f, 0f, 0f); }
        }

        // =================================================================
        // 1. 欧拉角 → 四元数   （原程序 Quaternion::SetEulerAngle）
        // =================================================================

        /// <summary>
        /// 欧拉角（单位：度）转四元数。
        ///
        /// 原程序代码（Quaternion.cpp 第 20–37 行）：
        /// <code>
        /// qw = fCosZ * fCosX * fCosY + fSinZ * fSinX * fSinY;
        /// qx = fCosZ * fSinX * fCosY - fSinZ * fCosX * fSinY;
        /// qy = fCosZ * fCosX * fSinY + fSinZ * fSinX * fCosY;
        /// qz = fSinZ * fCosX * fCosY - fCosZ * fSinX * fSinY;
        /// </code>
        /// 这正是教科书上「XYZ 内旋」的欧拉角转四元数公式。
        /// </summary>
        /// <param name="x_rotate">绕 X 轴旋转角度（度）</param>
        /// <param name="y_rotate">绕 Y 轴旋转角度（度）</param>
        /// <param name="z_rotate">绕 Z 轴旋转角度（度）</param>
        public void SetEulerAngle(float x_rotate, float y_rotate, float z_rotate)
        {
            // 原程序：x_rotate = x_rotate / 180.0 * 3.14159;
            float xRad = x_rotate * Mathf.Deg2Rad;
            float yRad = y_rotate * Mathf.Deg2Rad;
            float zRad = z_rotate * Mathf.Deg2Rad;

            // 半角
            float fCosZ = Mathf.Cos(zRad * 0.5f);
            float fSinZ = Mathf.Sin(zRad * 0.5f);
            float fCosX = Mathf.Cos(xRad * 0.5f);
            float fSinX = Mathf.Sin(xRad * 0.5f);
            float fCosY = Mathf.Cos(yRad * 0.5f);
            float fSinY = Mathf.Sin(yRad * 0.5f);

            qw = fCosZ * fCosX * fCosY + fSinZ * fSinX * fSinY;
            qx = fCosZ * fSinX * fCosY - fSinZ * fCosX * fSinY;
            qy = fCosZ * fCosX * fSinY + fSinZ * fSinX * fCosY;
            qz = fSinZ * fCosX * fCosY - fCosZ * fSinX * fSinY;
        }

        /// <summary>便捷工厂方法，等价于先 new Q4() 再调用 <see cref="SetEulerAngle"/>。</summary>
        public static Q4 FromEuler(float xDeg, float yDeg, float zDeg)
        {
            Q4 q = new Q4();
            q.SetEulerAngle(xDeg, yDeg, zDeg);
            return q;
        }

        // =================================================================
        // 2. 四元数 → 欧拉角   （原程序 Quaternion::GetEulerAngle）
        // =================================================================

        /// <summary>
        /// 四元数转欧拉角（单位：度）。
        ///
        /// 原程序代码（Quaternion.cpp 第 42–48 行）：
        /// <code>
        /// x_rotate = atan2(2 * (qw*qx + qz*qy), 1 - 2 * (qy*qy + qx*qx));
        /// y_rotate = asin (CLAMP(2 * (qw*qy - qx*qz), -1.0f, 1.0f));
        /// z_rotate = atan2(2 * (qw*qz + qx*qy), 1 - 2 * (qz*qz + qy*qy));
        /// </code>
        ///
        /// ⚠ 教学要点：当 <c>|y_rotate| → 90°</c> 时（万向锁），
        /// 上式前两个参数的分子分母会同时趋近 0，X 与 Z 的解不再唯一，
        /// 结果会出现**跳变**。本演示的起始姿态恰好是 (0, 90, 0)，
        /// 正处在万向锁上 —— 运行时会看到 HUD 里欧拉角跳而四元数平滑，
        /// 这不是 bug，而是这条公式的固有性质。
        /// </summary>
        public void GetEulerAngle(out float x_rotate, out float y_rotate, out float z_rotate)
        {
            x_rotate = Mathf.Atan2(2f * (qw * qx + qz * qy), 1f - 2f * (qy * qy + qx * qx));
            // 原程序用 CLAMP 宏把 asin 入参限制在 [-1, 1]
            y_rotate = Mathf.Asin(Mathf.Clamp(2f * (qw * qy - qx * qz), -1.0f, 1.0f));
            z_rotate = Mathf.Atan2(2f * (qw * qz + qx * qy), 1f - 2f * (qz * qz + qy * qy));

            // 原程序：x_rotate = x_rotate / 3.14159 * 180;
            x_rotate *= Mathf.Rad2Deg;
            y_rotate *= Mathf.Rad2Deg;
            z_rotate *= Mathf.Rad2Deg;
        }

        // =================================================================
        // 3. 四元数插值所需的基础运算
        // =================================================================

        /// <summary>
        /// 点积（内积）。对应原程序 <c>OnTimer</c> 中手写的那一长串：
        /// <code>
        /// float temp = begin.qw*end.qw + begin.qx*end.qx + begin.qy*end.qy + begin.qz*end.qz;
        /// </code>
        /// 用于判断两个四元数的「夹角」是否超过 180°，从而决定要不要翻转符号。
        /// </summary>
        public static float Dot(Q4 a, Q4 b)
        {
            return a.qw * b.qw + a.qx * b.qx + a.qy * b.qy + a.qz * b.qz;
        }

        /// <summary>取反：q 与 -q 表示**同一个旋转**（四元数双覆盖特性）。</summary>
        public Q4 Negated()
        {
            return new Q4(-qw, -qx, -qy, -qz);
        }

        /// <summary>模长 |q|。</summary>
        public float Magnitude
        {
            get { return Mathf.Sqrt(qw * qw + qx * qx + qy * qy + qz * qz); }
        }

        /// <summary>单位化。对应原程序 OnTimer 里那条对四个分量分别除以模长的长语句。</summary>
        public Q4 Normalized()
        {
            float m = Magnitude;
            if (m <= 1e-8f)
            {
                // 原程序没有做除零保护；这里补上，避免退化时产生 NaN 污染整个画面
                return Identity;
            }
            float inv = 1f / m;
            return new Q4(qw * inv, qx * inv, qy * inv, qz * inv);
        }

        /// <summary>
        /// 分量线性插值（**不做归一化**）。
        /// ⚠ 单独使用它得到的不是旋转插值，必须再 <see cref="Normalized"/>。
        /// </summary>
        public static Q4 Lerp(Q4 a, Q4 b, float t)
        {
            return new Q4(
                (1f - t) * a.qw + t * b.qw,
                (1f - t) * a.qx + t * b.qx,
                (1f - t) * a.qy + t * b.qy,
                (1f - t) * a.qz + t * b.qz);
        }

        /// <summary>
        /// NLerp（Normalized Linear interpolation）＝ 线性插值 + 归一化。
        ///
        /// ⚠⚠ 这是本演示最容易被误解的一点：
        /// **原程序用的就是 NLerp，不是 SLERP。**
        /// 原程序 OnTimer 先做四分量线性插值，紧接着除以模长，正是此式。
        /// NLerp 与 SLERP 的差别在于：NLerp 的角速度不恒定
        /// （两端慢、中间快），而 SLERP 沿大圆以恒定角速度运动。
        /// 因为这里只是「两个姿态之间的过渡动画」，NLerp 完全够用，
        /// 原作者的代码选择是合理的；本工程忠实保留。
        /// </summary>
        public static Q4 Nlerp(Q4 begin, Q4 end, float t)
        {
            return Lerp(begin, end, t).Normalized();
        }

        // =================================================================
        // 4. 与 Unity 引擎对接
        // =================================================================

        /// <summary>
        /// 转成 Unity 的 <see cref="UnityEngine.Quaternion"/>。
        ///
        /// ⚠ 注意参数顺序：Unity 构造函数是 <c>(x, y, z, w)</c>，
        /// 而原程序字段顺序是 <c>(qw, qx, qy, qz)</c>，写反了会得到完全不同的旋转。
        ///
        /// ⚠ 左右手系差异：原程序基于 OpenGL 右手坐标系，
        /// Unity 使用左手坐标系。把相同的 (x, y, z, w) 数值交给 Unity，
        /// 得到的旋转在几何上是原旋转的**镜像**——
        /// 数值、模长、点积、万向锁行为全部一致，只是旋向相反。
        /// 本工程选择「数值忠实优先」，详见 README「已知差异」一节。
        /// </summary>
        public UnityEngine.Quaternion ToUnityQuaternion()
        {
            return new UnityEngine.Quaternion(qx, qy, qz, qw);
        }

        /// <summary>
        /// 按原程序 <c>glRotatef(z) → glRotatef(y) → glRotatef(x)</c> 的顺序
        /// 用欧拉角构造 Unity 旋转：矩阵为 Rz · Ry · Rx。
        ///
        /// 为什么不直接写 <c>Quaternion.Euler(x, y, z)</c>？
        /// Unity 的 <c>Quaternion.Euler</c> 是 **ZXY** 顺序（外旋 Z→X→Y，矩阵 Ry·Rx·Rz），
        /// 与原程序的 XYZ 不同。二者只有角度取特殊值时才碰巧相等，
        /// 因此这里显式按原顺序做四元数乘法。A * B 表示「先 B 后 A」。
        /// </summary>
        public static UnityEngine.Quaternion FromEulerLikeOriginal(float xDeg, float yDeg, float zDeg)
        {
            return UnityEngine.Quaternion.AngleAxis(zDeg, Vector3.forward)
                 * UnityEngine.Quaternion.AngleAxis(yDeg, Vector3.up)
                 * UnityEngine.Quaternion.AngleAxis(xDeg, Vector3.right);
        }

        // =================================================================
        // 5. 显示
        // =================================================================

        /// <summary>格式化输出，用于 HUD 面板，例如 "w=0.7071 x=0.0000 y=0.7071 z=0.0000"。</summary>
        public string ToDisplayString()
        {
            return string.Format("w={0:F4}  x={1:F4}  y={2:F4}  z={3:F4}", qw, qx, qy, qz);
        }

        public override string ToString()
        {
            return ToDisplayString();
        }
    }
}
