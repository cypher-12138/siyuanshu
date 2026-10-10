# 四元数旋转变换演示 · Unity 版（siyuanshu）

把一个 **Visual Studio + MFC + OpenGL** 的旧教学程序 <code>anweihua</code>，
完整移植成 **Unity 2022.3 LTS + URP** 工程。

算法部分 **逐行忠实还原**；渲染与交互做了 Unity 化重写，
并新增了鼠标拖拽自由旋转。

> 仓库地址：<https://github.com/cypher-12138/siyuanshu>

---

## 目录

- [1. 这是什么](#1-这是什么)
- [2. 快速开始](#2-快速开始)
- [3. 项目结构](#3-项目结构)
- [4. 原程序 vs 本项目：逐项对照](#4-原程序-vs-本项目逐项对照)
- [5. 核心算法说明](#5-核心算法说明)
- [6. 与原程序的差异清单](#6-与原程序的差异清单)
- [7. 操作说明](#7-操作说明)
- [8. 推送到 GitHub](#8-推送到-github)
- [9. 常见问题](#9-常见问题)

---

## 1. 这是什么

原始程序是一个典型的 MFC 单文档 + 传统 OpenGL 示例：窗口里画一个经典的
**Utah 茶壶**，用定时器驱动，让茶壶从姿态 A 平滑转到姿态 B。
整个演示的数据流是：

```
欧拉角  ──SetEulerAngle──▶  四元数  ──NLerp 插值──▶  四元数  ──GetEulerAngle──▶  欧拉角  ──glRotatef──▶  画面
```

本工程把这套流程原样搬进 Unity，并保留了一个关键的教学价值：
**原程序用的插值方法是 NLerp，不是 SLERP** —— 这一点很多人会看漏，
详见 [第 5 节](#5-核心算法说明)。

---

## 2. 快速开始

### 环境要求

| 项目 | 要求 |
|---|---|
| Unity | **2022.3 LTS**（任意补丁版本；本工程按 `2022.3.62f3c1` 生成） |
| 渲染管线 | URP（随工程自动配置，见下） |
| 平台 | Windows / macOS / Linux 均可（中文字体在 Windows 上最稳） |
| 首次打开 | 需要联网，用于从 Unity 包注册表拉取 URP 等包 |

> **说明：`2022.3.62f3c1` 中的 `c1` 后缀表示这是 Unity 中国版（团结引擎之外的国内发行版）。**
> 它使用国内包注册表（`packages.unity.cn`），URP 版本号与官方版一致（均为 `14.0.x`）。
> 如果你用的是官方国际版，把 `ProjectSettings/ProjectVersion.txt` 和
> `Packages/manifest.json` 里的版本号改成你自己的即可。

> ⚠ **建议先把工程移出 OneDrive / 坚果云 / Dropbox 等同步目录再打开。**
> Unity 在导入和编译期间会高频读写 `Library/`，与云同步抢文件锁
> 很容易出现「资源导入失败」「脚本莫名重新编译」「工程打不开」等问题。
> 例如移到 `D:\UnityProjects\siyuanshu` 再打开。

### 步骤

1. **用 Unity Hub 添加工程**：`Add` → 选中本仓库根目录（含 `Assets/`、`ProjectSettings/` 的那一层）。
2. **打开**。
   - 如果 Hub 提示工程版本与你安装的版本不一致，选择「用已安装的版本打开」即可。
     也可以直接改 `ProjectSettings/ProjectVersion.txt` 里的版本号。
   - 首次打开会自动导入 URP 包并编译脚本，需要几分钟。
3. **按 Play**。

就这三步。相机、灯光、茶壶网格、材质、UI 面板**全部在运行时由代码创建**，
不依赖任何预制体或美术资源，所以不存在「场景丢引用」的问题。

打开后你会看到：白底、一个棕红色带高光的茶壶，
从姿态 `(0°, 90°, 0°)` 平滑旋转到 `(90°, 45°, 90°)`，约 3.3 秒后停下，
左侧实时显示四元数与欧拉角的数值。

### 关于 URP

工程里带了 `Assets/Editor/URPAutoSetup.cs`，它在编辑器加载时自动完成：

- 把演示场景加入 Build Settings；
- 创建并登记 URP 管线资源（如果还没有）。

菜单栏还有：

- **四元数演示 / 一键完整配置（URP + 线性色彩空间 + 场景）**
- **四元数演示 / 仅配置 URP 渲染管线**
- **四元数演示 / 打开演示场景**

> 为什么 URP 资源不直接放进仓库？
> 因为 `UniversalRenderPipelineAsset` 的脚本 GUID 随 URP 版本变化，
> 手写进版本库一旦对不上就会出现 *Missing Script*。
> 交给编辑器在首次打开时**自己创建**，是最稳的做法。
> 这套脚本使用反射访问 URP 类型，所以即使 URP 包缺失也不会导致编译错误 ——
> 此时工程回退到 Built-in 管线，材质会自动选 `Standard` 着色器，演示照常运行。

---

## 3. 项目结构

```
siyuanshu/
├── Assets/
│   ├── Editor/
│   │   └── URPAutoSetup.cs              # 编辑器：自动配置 URP、Build Settings
│   ├── Scenes/
│   │   └── QuaternionDemo.unity         # 唯一的场景（里面只有一个空物体挂控制器）
│   └── Scripts/
│       ├── QuaternionMath.cs            # ★ 四元数数学：对应原程序 Quaternion.cpp
│       ├── TeapotMeshBuilder.cs         # ★ Utah 茶壶网格的程序化生成
│       ├── QuaternionDemoController.cs  # ★ 主逻辑：对应原程序 OnTimer
│       ├── DemoHud.cs                   # 屏幕信息面板（IMGUI）
│       └── DemoBootstrap.cs             # 启动兜底：任何场景按 Play 都能跑
├── Packages/
│   └── manifest.json                    # URP 14.x + 内置模块
├── ProjectSettings/
│   ├── ProjectVersion.txt
│   └── EditorBuildSettings.asset
├── .gitignore                           # Unity 标准忽略规则
└── README.md
```

> `ProjectSettings/` 下只有两个文件是有意为之：
> 其余设置文件（`GraphicsSettings.asset`、`QualitySettings.asset` 等）
> 由 Unity 首次打开时按默认值生成，然后由 `URPAutoSetup.cs` 写入 URP 配置。
> 手工编写这些体积庞大、版本敏感的 YAML 反而容易把工程读坏。

---

## 4. 原程序 vs 本项目：逐项对照

### 4.1 文件级对照

| 原程序文件 | 本项目文件 | 说明 |
|---|---|---|
| `anweihua/Quaternion.h` | `QuaternionMath.cs` 中的 `Q4` 结构体 | 字段名保持 `qw/qx/qy/qz`，便于对照 |
| `anweihua/Quaternion.cpp` | `QuaternionMath.cs` | `SetEulerAngle` / `GetEulerAngle` 公式一字未改 |
| `anweihua/anweihuaView.cpp` → `OnTimer` | `QuaternionDemoController.cs` → `EvaluateQuaternion` | 插值流程 |
| `anweihua/anweihuaView.cpp` → `OnDraw` | `QuaternionDemoController.cs` → `ApplyToTeapot` | 把旋转施加到物体 |
| `anweihua/anweihuaView.cpp` → `OnCreate` / `OnSize` | `QuaternionDemoController.cs` → `BuildScene` | 相机、灯光、背景 |
| `auxSolidTeapot(0.5)` | `TeapotMeshBuilder.cs` | 内嵌 Newell 茶壶数据，运行时生成网格 |
| （无） | `DemoHud.cs` | 新增的信息面板 |
| （无） | `URPAutoSetup.cs` | 新增的工程配置脚本 |

### 4.2 参数级对照

| 原程序 | 本项目 | 值 |
|---|---|---|
| `m_xrotation_begin, _y, _z` | `QuaternionDemoController.BeginEuler` | `(0, 90, 0)` |
| `m_xrotation_end, _y, _z` | `QuaternionDemoController.EndEuler` | `(90, 45, 90)` |
| `t += 0.005` | `TimeStep` | `0.005` |
| `auxSolidTeapot(0.5)` | `TeapotScale` | `0.5` |
| `glOrtho(-2*aspect, 2*aspect, -2, 2, ...)` | `CameraOrthoSize` | `2` |
| `glClearColor(1,1,1,1)` | `camera.backgroundColor` | `white` |
| `light_position = {1,1,1,0}` | 平行光朝向 | 从 `(1,1,1)` 射向原点 |
| `light_ambient = {0.1,0.1,0.1,1}` | `RenderSettings.ambientLight` | `(0.1,0.1,0.1)` |
| `mat_diffuse = {0.5,0.3,0.1,1}` | 材质 `_BaseColor` | `(0.5,0.3,0.1)` |
| `mat_emission = {0.5,0,0,1}` | 材质 `_EmissionColor` | `(0.5,0,0)` |
| `mat_shiness = 30` | 材质 `_Smoothness` | `0.5`（等价观感） |
| `glRotatef(z)→(y)→(x)` | 矩阵 `Rz·Ry·Rx` | 见 5.4 节 |

---

## 5. 核心算法说明

### 5.1 欧拉角 → 四元数

原程序 `Quaternion::SetEulerAngle`：

```cpp
qw = fCosZ * fCosX * fCosY + fSinZ * fSinX * fSinY;
qx = fCosZ * fSinX * fCosY - fSinZ * fCosX * fSinY;
qy = fCosZ * fCosX * fSinY + fSinZ * fSinX * fCosY;
qz = fSinZ * fCosX * fCosY - fCosZ * fSinX * fSinY;
```

这是教科书上 **XYZ 内旋**（等价于外旋 ZYX）那一套标准公式。
本项目在 `Q4.SetEulerAngle` 里逐项保留。

### 5.2 双覆盖（double cover）修正

四元数有个性质：**`q` 与 `-q` 表示同一个旋转**。
但如果在四元数空间里做线性插值，选错了符号就会绕「远路」转一大圈。

原程序的处理：

```cpp
float temp = begin.qw*end.qw + begin.qx*end.qx + begin.qy*end.qy + begin.qz*end.qz;
if (temp < 0) { end.qw *= -1; end.qx *= -1; end.qy *= -1; end.qz *= -1; }
```

即：两端四元数点积为负时，把终点取反，让它落到同一个半球里。
本项目在 `EvaluateQuaternion()` 里保留了这个判断，
并且把 `temp`（`DotValue`）和是否触发取反（`SignFlipped`）显示在 HUD 上，方便观察。

### 5.3 ⚠ 插值方法是 NLerp，不是 SLERP

这是整套代码里**最容易被误读的一处**。原程序是这样写的：

```cpp
Quaternion_middle.qw = (1 - t) * Quaternion_begin.qw + t * Quaternion_end.qw;
Quaternion_middle.qx = (1 - t) * Quaternion_begin.qx + t * Quaternion_end.qx;
Quaternion_middle.qy = (1 - t) * Quaternion_begin.qy + t * Quaternion_end.qy;
Quaternion_middle.qz = (1 - t) * Quaternion_begin.qz + t * Quaternion_end.qz;

Quaternion_middle.qw /= sqrt(qw*qw + qx*qx + qy*qy + qz*qz);   // 再除以模长
Quaternion_middle.qx /= sqrt(...);
Quaternion_middle.qy /= sqrt(...);
Quaternion_middle.qz /= sqrt(...);
```

**先线性插值四个分量，再归一化** —— 这就是 **NLerp（Normalized Linear interpolation）**。
如果原程序用的是 SLERP，代码里应该出现 `sin`、`acos` 或 `sin((1-t)*ω)/sin(ω)` 这类式子，
但它一次都没出现。

两者的区别：

| | NLerp | SLERP |
|---|---|---|
| 路径 | 弦（四维空间里的直线，归一化后落在球面上） | 大圆上的弧 |
| 角速度 | **不恒定**（两端慢、中间快） | 恒定 |
| 计算量 | 一次归一化 | 多次三角函数 |
| 是否经过中间姿态 | 否 | 是（严格沿测地线） |

对于「两个姿态之间放个过渡动画」这种用途，NLerp 完全够用，
原作者的代码选择是合理的。本项目**忠实保留 NLerp**，
并在 HUD 里明确标出「NLerp：线性插值 + 归一化」。

### 5.4 万向锁：一个可以直接观察到的现象

原程序最后把插值结果反解回欧拉角：

```cpp
x_rotate = atan2(2 * (qw*qx + qz*qy), 1 - 2 * (qy*qy + qx*qx));
y_rotate = asin (CLAMP(2 * (qw*qy - qx*qz), -1.0f, 1.0f));
z_rotate = atan2(2 * (qw*qz + qx*qy), 1 - 2 * (qz*qz + qy*qy));
```

当 `y_rotate → ±90°` 时，前两式的分子分母会**同时趋近 0**，
`atan2(0, 0)` 没有唯一解 —— X 与 Z 的分量可以任意互换，这就是**万向锁**。

而这个演示的**起始姿态恰好是 `(0, 90, 0)`，正坐在万向锁上**。

会发生什么：四元数一路平滑，但反解出的欧拉角会出现**跳变**。
本项目把这个现象**故意显示在 HUD 上**：

```
插值结果（NLerp）  Quaternion_middle  w=... x=... y=... z=...   ← 始终平滑
反解欧拉角：       x=... y=... z=...                            ← 可能突变
```

**为什么画面不抖？**
因为 `SetEulerAngle` 与 `GetEulerAngle` 在数学上互为逆运算
（本工程已用数值验证：`GetEuler(q)` 反解出的角度再交给 `Rz·Ry·Rx`
得到的旋转矩阵，与 `q` 自己的旋转矩阵逐元素相等），
所以「欧拉角 → 四元数 → NLerp → 欧拉角」这条往返路径在**不发生跳变时是无损的**。

本项目的做法是：

- **用四元数直接驱动 Transform**（数学上与原程序的 `glRotatef` 链完全等价，
  但避开了万向锁附近的数值不稳定）；
- **仍然调用 `GetEulerAngle` 并把结果显示出来**，让那个跳变可以被看见。

于是既没有画面抖动，又完整保留了这个教学点。

### 5.5 欧拉角的旋转顺序（重要坑）

原程序的渲染代码：

```cpp
glRotatef(m_zrotation, 0.0, 0.0, 1.0);
glRotatef(m_yrotation, 0.0, 1.0, 0.0);
glRotatef(m_xrotation, 1.0, 0.0, 0.0);
```

OpenGL 的 `glRotatef` 是**右乘**，所以合成矩阵是 `Rz · Ry · Rx`。

**不要图省事写成 `Quaternion.Euler(x, y, z)`！**
Unity 的 `Quaternion.Euler` 采用 **ZXY** 顺序（矩阵 `Ry·Rx·Rz`），
只在角度取特殊值时才和原程序碰巧相等。

本项目在 `Q4.FromEulerLikeOriginal` 里显式按原顺序做四元数乘法：

```csharp
Quaternion.AngleAxis(zDeg, Vector3.forward)   // A * B 表示「先 B 后 A」
* Quaternion.AngleAxis(yDeg, Vector3.up)
* Quaternion.AngleAxis(xDeg, Vector3.right)
```

> 不过本演示的主路径是直接用四元数驱动 Transform，
> 这个函数作为「顺序正确」的参考实现保留在 `QuaternionMath.cs` 里。

### 5.6 为什么茶壶数据要内嵌进代码

原程序用的 `auxSolidTeapot()` 是 GLAUX 库里的内置函数，Unity 没有对应物。
为了做到「克隆下来直接能跑、不依赖任何外部资源」，
`TeapotMeshBuilder.cs` 把公开的 **Newell 茶壶控制点数据**
（32 个双三次 Bézier patch、290 个控制点）内嵌进源码，
运行时用 Bernstein 基函数求值生成网格，法线由 `∂P/∂s × ∂P/∂t` 解析求出。

顺带做了两件事：

- 数据是 **Z 轴向上**，Unity 是 **Y 轴向上**。
  转换用的是**绕 X 轴 -90° 的纯旋转**，而不是简单地交换 Y/Z ——
  交换两个轴是镜像变换（行列式 = −1），会让法线方向与三角形绕序的对应关系整体翻转，导致光照全黑。
- 壶盖按数据惯例放大了 7.7%（`LidFitScale`），否则壶盖会嵌进壶口里。
  想关掉这个修正，把 `TeapotMeshBuilder.Build(segments, fitLid: false)` 的 `fitLid` 传 `false` 即可。

---

## 6. 与原程序的差异清单

以下是**全部**差异。凡是涉及算法的，都只做了等价改写；其余是工程性适配。

### 6.1 算法层（等价改写，不改变数学结果）

| # | 原程序 | 本项目 | 原因 |
|---|---|---|---|
| 1 | `x / 180.0 * 3.14159` | `Mathf.Deg2Rad` | 原程序用了低精度常数 `3.14159`，累积误差可达 `1e-6` 量级 |
| 2 | `#define CLAMP(x,min,max)` | `Mathf.Clamp` | 宏 → 函数，行为一致 |
| 3 | `atan2` / `asin` / `sqrt` | `Mathf.Atan2` / `Mathf.Asin` / `Mathf.Sqrt` | 单精度浮点，与原程序一致（原程序用的也是 `float`） |
| 4 | 归一化时没有除零保护 | 加了一层 `m <= 1e-8` 判断 | 退化时返回单位四元数，避免 NaN 污染画面 |

### 6.2 行为层（有意改动）

| # | 原程序 | 本项目 | 原因 |
|---|---|---|---|
| 5 | `SetTimer(0, 0.33)` 按定时器节拍推进，速度随帧率变化 | 保留每跳 `+0.005` 的步长语义，但按 `Time.deltaTime` 换算（以 60 FPS 为基准） | 原程序在不同机器上快慢不一；改成按真实时间推进后动画时长恒定 |
| 6 | 播到 `t = 1` 就永久停止 | 同样停止，但提供「重播」按钮 / `R` 键 | 只跑一次的演示不方便反复观察；不构成算法改动 |
| 7 | 转起来的只有茶壶，相机固定 | 新增鼠标拖拽自由旋转茶壶（拖拽时暂停插值，松开恢复播放） | 需求指定新增 |
| 8 | 无任何信息显示 | 新增左上角 HUD，实时显示 `t`、点积、三个四元数、反解欧拉角 | 需求指定「中文 UI」，且让万向锁现象可见 |
| 9 | 没有启用 `GL_CULL_FACE`（OpenGL 默认关闭） | 材质 `_Cull = Off`（双面渲染） | 与原程序一致；同时让三角形绕序不影响可见性 |

### 6.3 平台层（坐标系差异，无法消除）

| # | 说明 |
|---|---|
| 10 | **左右手系**：OpenGL 是右手坐标系，Unity 是左手坐标系。把**相同的** `(x, y, z, w)` 交给 Unity，得到的旋转在几何上是原旋转的**镜像**——数值、模长、点积、万向锁行为全部一致，只是旋向相反。本工程选择「**数值忠实优先**」，这样 HUD 上显示的数值与你对着原程序调试时看到的完全一样。如果你更希望视觉旋向也一致，在 `Q4.ToUnityQuaternion()` 里把 `qy`（或 `qz`）取反即可。 |
| 11 | **颜色空间**：原程序是 Gamma 空间的固定管线光照；Unity 建议 URP 用 Linear。菜单「一键完整配置」会把色彩空间切到 Linear，此时高光观感与原程序略有差别。想完全贴近原程序，可在 Project Settings → Player → Color Space 改回 Gamma。 |

---

## 7. 操作说明

| 操作 | 效果 |
|---|---|
| **按住鼠标左键拖动** | 自由旋转茶壶。拖动期间**暂停**自动插值，鼠标完全接管 |
| **松开鼠标左键** | 结束接管，自动播放从当前 `t` 继续 |
| `R` | 重播（`t` 归零并开始播放） |
| `空格` | 暂停 / 继续。已播完时按空格等同于重播 |
| 面板上的 **重播 (R)** / **暂停 (空格)** 按钮 | 同上 |

面板上显示的数值与原程序 `OnTimer` 里的中间变量一一对应：

```
状态：自动播放中
插值参数 t = 0.3250   （32.5%）

── 起始姿态 ──            ← m_xrotation_begin 等
欧拉角：x=0.0000  y=90.0000  z=0.0000
Quaternion_begin  w=0.7071  x=0.0000  y=0.7071  z=0.0000

── 结束姿态 ──            ← m_xrotation_end 等
欧拉角：x=90.0000  y=45.0000  z=90.0000
Quaternion_end    w=0.6533  x=0.2706  y=0.6533  z=0.2706
点积 temp = 0.923880  →  temp ≥ 0，无需取反

── 插值结果（NLerp：线性插值 + 归一化）──
Quaternion_middle w=0.6914  ...                  ← 始终平滑
模长 |q| = 1.000000
反解欧拉角：x=...  y=...  z=...                   ← 万向锁附近会突变
```

---

## 8. 推送到 GitHub

目标仓库：`https://github.com/cypher-12138/siyuanshu.git`

### 方式 A：用 GitHub CLI（最省事）

```bash
cd siyuanshu
git init
git add .
git commit -m "四元数旋转变换演示：从 MFC+OpenGL 移植到 Unity URP"
git branch -M main

# 一步创建远程仓库并推送
gh repo create siyuanshu --public --source=. --remote=origin --push
```

### 方式 B：先在网页上建空仓库，再推

在 <https://github.com/new> 创建名为 `siyuanshu` 的仓库，
**不要**勾选 *Add a README* / *.gitignore* / *license*（否则会产生无关的初始提交）。

```bash
cd siyuanshu
git init
git add .
git commit -m "四元数旋转变换演示：从 MFC+OpenGL 移植到 Unity URP"
git branch -M main
git remote add origin https://github.com/cypher-12138/siyuanshu.git
git push -u origin main
```

> 仓库已在 `.gitignore` 里排除了 `Library/`、`Temp/`、`*.csproj`、`*.sln` 等，
> 因此第一次提交的体积很小（几百 KB 级别，全是文本），不会有二进制垃圾。

---

## 9. 常见问题

**Q：按 Play 什么都没有，只有蓝/灰背景？**

先看 Console 有没有编译错误。如果没有，
本项目有一套兜底机制：`DemoBootstrap` 会在 `AfterSceneLoad` 时检查场景里有没有控制器，
没有就现场创建一个。若 Console 出现
「场景中没有找到 QuaternionDemoController，已自动创建」，
说明场景里的脚本引用断了，但演示仍能运行 ——
此时用菜单 **四元数演示 / 打开演示场景** 重新打开场景即可。

**Q：茶壶是纯粉色（洋红色）？**

说明当前渲染管线的着色器不匹配。
`CreateTeapotMaterial()` 会按
`Universal Render Pipeline/Lit` → `Simple Lit` → `Standard` → `Legacy Diffuse`
依次回退，正常情况不会出现粉色。
若真的出现，检查 `Packages/manifest.json` 里的 URP 版本号
是否与你的 Unity 2022.3 小版本匹配（可改成 `14.0.8` 或 `14.0.12` 试试），
或者直接删掉那一行，让 Unity 以 Built-in 管线运行 —— 演示同样正常。

**Q：中文字显示成方框？**

`DemoHud` 会用 `Font.CreateDynamicFontFromOSFont` 依次尝试
微软雅黑 / 黑体 / 宋体 / Noto Sans CJK 等系统字体。
Linux 上如果这些都没有，装一个 `fonts-noto-cjk` 即可。

**Q：茶壶看起来转的方向和原程序相反？**

这是左右手系差异，属于已知且有意保留的行为，详见 [6.3 节](#63-平台层坐标系差异无法消除)。

**Q：怎样让它一直循环播放？**

在 `QuaternionDemoController.Update()` 里把

```csharp
if (T >= 1f) { T = 1f; IsPlaying = false; }
```

改成 `if (T >= 1f) { T = 0f; }` 即可。
（原程序是播完即停，本工程默认保持原行为。）

**Q：想换成 SLERP 对比一下？**

`QuaternionMath.cs` 里预留了 `Q4.Lerp` / `Q4.Nlerp`。
标准 SLERP 实现思路：

```csharp
float dot = Mathf.Clamp(Q4.Dot(a, b), -1f, 1f);
float theta = Mathf.Acos(dot);
if (theta < 1e-4f) return a;
float s = Mathf.Sin(theta);
return (Mathf.Sin((1 - t) * theta) / s) * a + (Mathf.Sin(t * theta) / s) * b;
```

把它接进 `EvaluateQuaternion()` 的第三步即可与 NLerp 对比。

---

## 许可

原始程序为教学示例。本移植工程以 MIT 许可发布，可自由使用与修改。
