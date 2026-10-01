# BaiduAI 项目概述

## 一、项目简介

**BaiduAI** 是一个基于 **.NET Framework 4.8 + WinForms** 的桌面应用，将**百度 AI 开放平台的人脸识别能力**与**本机 USB 摄像头**结合，实现"图片人脸对比"与"刷脸注册 / 刷脸登录"。

界面按 **DSH 设计系统**构建：暗色基座、卡片式分区、统一圆角与间距、自绘控件，并支持亮 / 暗主题切换。界面全部由代码创建（不使用 Windows 窗体设计器）。

| 项目 | 说明 |
| --- | --- |
| 程序类型 | Windows 桌面窗体应用（WinExe） |
| 目标框架 | .NET Framework 4.8 |
| 开发语言 | C# |
| 解决方案 | `BaiduAI.sln` |
| 工程文件 | `BaiduAI/BaiduAI.csproj` |
| 源码规模 | 11 个 `.cs` 文件，约 3100 行 |
| 界面实现 | 代码构建，无 `.Designer.cs`；自绘控件位于 `Ui/` |
| 视觉规范 | DSH 设计令牌（`--dsw-alias-*` / `--dsw-radius-*` / `--dsw-font-*`） |
| 主题 | 暗色 / 亮色双主题，可运行时切换 |
| 依赖管理 | NuGet（`packages.config`）+ 一个 COM 组件引用 |
| 编译状态 | **编译通过，0 error / 0 warning** |

## 二、运行环境

- Windows 10 / 11
- .NET Framework 4.8 运行时
- 一个可用的 USB 摄像头（Windows 能识别为"视频输入设备"）
- 百度 AI 开放平台的**人脸识别**应用（需要 API Key 与 Secret Key）
- 联网环境（所有人脸识别均调用百度云端接口）

## 三、主要依赖

| 依赖 | 版本 | 用途 |
| --- | --- | --- |
| `Baidu.AI`（AipSdk） | 4.15.12 | 百度 AI SDK，提供人脸检测、比对、注册、搜索接口 |
| `AForge.Video.DirectShow` | 2.2.5 | 枚举并访问 DirectShow 摄像头设备 |
| `AForge.Controls` | 2.2.5 | 提供 `VideoSourcePlayer` 预览控件 |
| `AForge.Imaging` / `AForge.Math` / `AForge` | 2.2.5 | AForge 底层图像与数学支持 |
| `Newtonsoft.Json` | 10.0.3 | JSON 序列化与反序列化 |
| `System.Configuration` | 框架内置 | 读取 `app.config` 中的认证信息 |
| `WMPLib` / `AxWMPLib`（COM） | Windows Media Player | 播放登录成功后的欢迎语音 |

界面绘制只使用 GDI+（`System.Drawing`）与 WinForms 原生控件，未引入任何第三方 UI 库。

## 四、界面设计

### 4.1 整体布局

程序为自定义标题栏的三栏工作台布局：

```text
标题栏（可拖动，含品牌标记、密钥状态、主题切换、最小化/最大化/关闭）
├── 左栏：图片检测卡片 + 接口原始返回卡片
├── 中栏：摄像头预览卡片（设备选择、连接、重检测、停止、拍照 + 视频区）
├── 右栏：人脸库卡片（分组ID、姓名、注册、登录）+ 识别结果卡片
└── 状态栏（操作反馈 + 技术栈标注）
```

### 4.2 设计令牌

配色、字体、圆角、控件尺寸均取自 DSH 前端的设计令牌，而非主观配色。

| 类别 | 令牌与取值 |
| --- | --- |
| 背景基座 | 亮 `#ffffff` / 暗 `#151517` |
| 表面层级 | layer1 暗 `#232324`、layer2 暗 `#2c2c2e`、layer3 暗 `#353638` |
| 主要文字 | 亮 `#0f1115` / 暗 `#f9fafb` |
| 次要 / 三级文字 | 亮 `#61666b` / `#81858c`，暗 `#cfd3d6` / `#adb2b8` |
| 描边 | 亮 `#0000000a`~`#00000029` / 暗 `#ffffff0f`~`#ffffff33`（分 l1~l4 四级） |
| 业务色（聚焦、强调） | 亮 `#4176e6` / 暗 `#7aaaff` |
| 成功色（人脸框） | `#22c55e` |
| 警告 / 错误色 | `#f59e0b` / 亮 `#ec1313`、暗 `#f25a5a` |
| 圆角 | xs 4 / sm 8 / md 12 / lg 16 / xl 20 / panel 28（px） |
| 主按钮 | 亮 `#0f1115` / 暗 `#f9fafb`，高 **36px**，圆角 **12px**，文字反色 |
| 次按钮 | 描边式，1px 描边，悬停出现 `#2631480f` 等效填充 |
| 输入框 | 高 **32px**，圆角 12px，聚焦时描边切为业务色 + 加粗 |
| 卡片 | 圆角 **20px**，1px 描边，表面填 `layer2` |
| 字体 | `Microsoft YaHei UI`，正文 14px、说明 12px、微标 11px；代码 `Consolas` 12px |
| 阴影 | 黑色低透明度柔和阴影，**无彩色发光** |

### 4.3 实现方式

界面由 `Form1.Ui.cs` 中的 `InitializeComponent()` 用代码创建，配合 `Ui/` 下的自绘控件层：

| 文件 | 职责 |
| --- | --- |
| `Ui/DshTheme.cs` | 设计令牌定义与亮 / 暗两套主题 |
| `Ui/DshPaint.cs` | 圆角路径、圆角描边、文本渲染参数等绘制辅助 |
| `Ui/DshControls.cs` | 自绘控件：`DshCard`、`DshButton`、`DshTextField`、`DshPill`、`DshTitleBar` |

自绘控件统一继承 `DshControlBase`（开启双缓冲与自绘，消除闪烁），并实现 `IDshThemed` 接口，
主题切换时由窗体统一广播刷新。

### 4.4 与 DSH 的差异说明

WinForms 的绘制能力与浏览器不同，以下三点做了等价处理，属于有意的取舍：

1. **圆角形状**：DSH 的圆角为超椭圆（`corner-shape: superellipse(1.5)`），WinForms 无法实现，改用普通圆角近似。
2. **半透明叠加**：DSH 用 `color-mix()` / 8 位 hex 生成半透明色并叠加到底色上，本项目折算为**等效实色**（例如交互悬停填充）。
3. **原生控件**：`ComboBox`、`VideoSourcePlayer`、滚动条无法自绘，只能设置配色；暗色主题下额外调用
   `uxtheme.SetWindowTheme(handle, "DarkMode_Explorer")` 让原生滚动条跟随暗色（Windows 10 1809+）。

## 五、功能清单

### 1. 图片模式（左栏）

| 功能 | 操作 | 说明 |
| --- | --- | --- |
| 选择人脸图片 | 点击「选择人脸图片」两次 | 依次把两张图片路径填入图片 A / B 输入框；两张都已选时自动左移替换 |
| 人脸对比 | 点击「开始对比」 | 提交两张图片调用人脸比对接口，相似度显示在状态栏与原始返回区 |

### 2. 摄像头模式（中栏 + 右栏）

| 功能 | 操作 | 说明 |
| --- | --- | --- |
| 连接 | 下拉框选设备 → 点击「连接」 | 以 320×240 分辨率、15fps 打开摄像头，卡片状态变为「已连接」 |
| 实时人脸框 | 连接后自动运行 | 预览画面中自动框出人脸（绿色），并同步更新年龄与人脸质量提示 |
| 重新检测 | 点击「重新检测」 | 重新枚举视频输入设备并刷新下拉列表 |
| 拍照 | 点击「拍照」 | 抓取当前帧、缩放到最大 800×600、保存 JPG，并读取年龄与美颜度 |
| 停止 | 点击「停止」 | 释放摄像头，卡片状态回到「未连接」 |
| 人脸注册 | 填写分组 ID、用户姓名 → 点击「人脸注册」 | 抓取当前帧，将人脸加入指定用户组 |
| 人脸登录 | 填写分组 ID → 点击「人脸登录」 | 人脸搜索，命中后显示用户 ID 与相似度并播放欢迎语音 |

### 3. 通用

| 功能 | 说明 |
| --- | --- |
| 主题切换 | 标题栏「切换亮色 / 切换暗色」按钮，即时生效 |
| 密钥状态 | 标题栏胶囊标记显示「密钥已配置」/「未配置密钥」 |
| 状态栏 | 实时反馈操作结果与错误原因（成功绿色 / 失败红色） |
| 接口原始返回 | 左栏下方代码字体文本区，展示接口返回的完整 JSON |

## 六、典型使用流程

**流程 A —— 照片对比**

```text
选择图片 A 与图片 B → 开始对比 → 查看相似度
```

**流程 B —— 建立人脸库并登录**

```text
连接摄像头
  → 填写「用户分组ID」+「用户姓名」
  → 人脸注册（当前画面写入该分组的用户库）
  → 重新对准摄像头，填写相同分组ID
  → 人脸登录（人脸搜索）
  → 命中：显示用户ID与相似度 + 播放欢迎语音
  → 未命中：状态栏红色提示
```

## 七、目录结构

```text
BaiduAI/                         解决方案根目录
├── BaiduAI.sln                  解决方案文件
├── packages/                    NuGet 包还原目录
├── packages截图.docx            包管理界面截图（文档材料）
└── BaiduAI/                     主工程
    ├── BaiduAI.csproj           工程文件
    ├── app.config               ★ 应用配置（认证信息唯一来源）
    ├── packages.config          NuGet 依赖清单
    ├── README.md                快速上手、界面说明与配置步骤
    ├── .gitignore               版本控制忽略规则
    ├── PROJECT_OVERVIEW.md      本文档
    ├── Program.cs               程序入口（含 --render-test 界面自检开关）
    ├── Form1.cs                 ★ 业务逻辑：摄像头、接口调用、实时检测
    ├── Form1.Ui.cs              ★ 界面构建：全部由代码创建
    ├── FaceDetectInfo.cs        人脸检测接口的返回数据模型
    ├── FaceIdentifyInfo.cs      人脸识别接口的返回数据模型
    ├── Ui/                      自绘控件与设计令牌
    │   ├── DshTheme.cs          DSH 设计令牌（亮 / 暗双主题）
    │   ├── DshPaint.cs          GDI+ 绘制辅助
    │   └── DshControls.cs       卡片 / 按钮 / 输入框 / 胶囊 / 标题栏
    ├── Common/                  公共工具类
    │   ├── ClassLoger.cs        文件日志记录与日志清理
    │   ├── JsonHelper.cs        JSON 序列化 / 反序列化封装（Newtonsoft.Json）
    │   ├── Extensions.cs        对象与字符串扩展方法
    │   └── ValidHelper.cs       类型转换、校验、加密等扩展方法
    ├── Properties/              程序集信息与资源、设置
    ├── bin/Debug/               编译输出（含 exe、依赖 DLL 与欢迎语音 mp3）
    └── obj/                     编译中间产物
```

### 分层现状

程序为**四层**结构：

- **界面构建层**：`Form1.Ui.cs` + `Ui/*`（设计令牌、自绘控件、布局）
- **业务逻辑层**：`Form1.cs`（摄像头控制、接口调用、实时检测、状态反馈）
- **数据模型层**：`FaceDetectInfo` / `FaceIdentifyInfo` 等返回结构体
- **工具层**：`Common` 下的日志、JSON、扩展方法

界面与逻辑已分离到不同文件，公共方法也做了抽取（`CaptureScaledFrame`、`CaptureFrameBase64`、
`CheckApiResult`、`StopCamera`、`BuildQualityReport`、`ApplyTheme`、`SetStatus`），
但业务逻辑仍在窗体类内，后续可进一步拆为独立服务类。

## 八、关键技术点

| 技术点 | 实现方式 |
| --- | --- |
| 摄像头访问 | AForge `FilterInfoCollection` 枚举设备，`VideoCaptureDevice` 采集，`VideoSourcePlayer` 预览 |
| 图像上传 | `Image` / `Bitmap` → `MemoryStream`（JPEG）→ `Convert.ToBase64String` |
| 人脸检测 | `Face.Detect(image, "BASE64", options)`，`face_field` 指定 `age,beauty,qualities` 等字段 |
| 人脸比对 | `Face.Match(faces)`，提交两个 `{image, image_type, face_type, quality_control, liveness_control}` 对象 |
| 人脸注册 | `Face.UserAdd(...)`，参数含分组 ID、用户 ID、用户信息与动作类型 |
| 人脸搜索 | `Face.Search(...)`，配置相似度阈值、质量控制、活体控制与最大返回数 |
| 实时人脸定位 | 帧回调中按 700ms 节流克隆帧 → 后台线程调用接口 → 坐标按缩放比换算回预览帧 → 帧上绘制矩形 |
| 并发控制 | `Interlocked` 保证同一时刻只有一帧在检测；连续失败 5 次自动暂停实时检测；`volatile` 标记控制关闭流程 |
| 异步调用 | 所有接口调用 `async/await + Task.Run`，界面保持响应 |
| 自绘界面 | `GraphicsPath` 圆角路径 + `TextRenderer` 文本渲染；`DshControlBase` 统一开启双缓冲 |
| 主题系统 | 设计令牌对象 + `IDshThemed` 接口广播刷新；原生控件单独设置配色与滚动条主题 |
| 语音播报 | Windows Media Player COM 控件播放随程序分发的 mp3 文件（按可执行文件目录定位） |
| 日志 | `ClassLoger` 按 `年+月/日.Log` 分目录写文件，支持 DEBUG/INFO/FAIL/ERROR 级别 |

## 九、已完成的修复

### 安全

| # | 问题 | 处理方式 |
| --- | --- | --- |
| 1 | 密钥硬编码在 `Form1.cs`，且 `app.config` 中另有一套互相矛盾的密钥 | 删除源码中的硬编码；认证信息统一从 `app.config` 读取，`app.config` 改为占位符 + 安全提醒；未配置时标题栏显示「未配置密钥」并给出明确提示 |

### 稳定性

| # | 问题 | 处理方式 |
| --- | --- | --- |
| 2 | 未连接摄像头时点击「退出」抛空引用异常 | 抽出 `StopCamera()`，对 `videoSource` / `videoSourcePlayer1` 做完备空值判断，可重复调用 |
| 3 | 接口调用阻塞 UI 线程，网络慢时界面"未响应" | 5 个操作按钮的接口调用全部改为 `async/await + Task.Run`，调用期间禁用按钮、显示等待光标 |
| 4 | 多处空 `catch { }` 静默吞异常 | 全部改为记录日志 + 状态栏提示 + 用户可读弹窗；新增 `CheckApiResult()` 统一解析 `error_code` / `error_msg` |
| 5 | 预览画框每帧 `new Pen` 导致 GDI 句柄泄漏 | 画笔改为字段级复用并在窗体关闭时释放；`GetHbitmap` 申请的句柄补上 `DeleteObject` 释放 |

### 功能性

| # | 问题 | 处理方式 |
| --- | --- | --- |
| 6 | 实时人脸框从未生效（触发检测的调用被注释，相关方法成为死代码） | 已恢复并重写：帧回调中按 700ms 节流触发后台检测，坐标换算后回到帧上绘制，带 3 秒有效期避免残影 |
| 7 | 后台线程空转（仅翻转标志位而无人消费） | 删除无效线程，改由节流机制按需触发检测 |
| 8 | 帧率设为 1 fps，预览卡顿 | 调整为 15 fps；接口调用频率由独立的 700ms 节流控制，不会因提高帧率而刷爆配额 |
| 9 | 欢迎语音用相对路径，从快捷方式启动时找不到 | 改为按可执行文件目录定位并缓存，文件缺失时记录日志而非静默失败 |

### 代码质量

| # | 问题 | 处理方式 |
| --- | --- | --- |
| 10 | `JavaScriptSerializer` 默认 2MB 上限，解析大图片返回会失败 | `JsonHelper` 改用 Newtonsoft.Json，删除对 `System.Web.Extensions` 的依赖 |
| 11 | `Extensions.cs` 为 GBK 编码，与其余文件不一致 | 转为 UTF-8（旧文件备份为 `Extensions.cs.gbk.bak`） |
| 12 | 日志以 `FileShare.None` 打开，多线程并发写时异常被吞掉导致丢日志 | 改用 `FileMode.Append` + `FileShare.Read`，并补上 `using` 与兜底日志 |
| 13 | 编译警告 8 条（未使用变量、未使用字段、过时 API、死代码） | 逐一清理，现为 **0 warning** |

### 界面与工程

| # | 事项 | 处理方式 |
| --- | --- | --- |
| 14 | 原界面为设计器拖拽的固定坐标布局，观感陈旧 | 改为 DSH 设计语言：自定义标题栏 + 三栏卡片式布局 + 自绘控件；界面全部由代码构建 |
| 15 | 缺少 `.gitignore` 与 README | 已补充 `.gitignore` 与 `README.md`（含密钥配置、界面结构、设计令牌与自检方法） |
| 16 | 没有界面自检手段 | `Program.cs` 增加 `--render-test <png>` 开关，可无摄像头/密钥将界面渲染为图片（不影响正常启动） |

## 十、后续可改进项

1. **拆分服务层**：把 `Form1.cs` 中的接口调用与摄像头控制抽成 `BaiduFaceService` 与 `CameraService`，窗体只做绑定。
2. **统一接口版本**：当前 `Detect` 使用旧版返回结构（`result_num` / `result[]`），`Search` / `UserAdd` 使用新版（`error_code` / `user_list`），建议整体迁移到新版接口并统一用强类型模型反序列化。
3. **`user_id` 可配置**：当前注册固定为 `"1"`，多人注册会互相覆盖，建议由用户姓名或输入框生成。
4. **密钥安全再升级**：可改为从环境变量或用户级加密存储（DPAPI）读取，使 `app.config` 完全不含密钥。
5. **参数化配置**：把分辨率、帧率、节流间隔、相似度阈值、主题偏好等提为 `appSettings` 配置项。
6. **视觉细节补完**：DSH 的 0.5px 发丝描边在 WinForms 下最细只能到 1px；如需更接近，可改用半透明 1px 描边的折中方案。
7. **原生控件替换**：`ComboBox` 是原生控件，暗色下边框仍为系统样式，可替换为自绘下拉以完全统一。

## 十一、文档信息

| 项 | 内容 |
| --- | --- |
| 文档名称 | BaiduAI 项目概述 |
| 适用程序 | BaiduAI（.NET Framework 4.8 WinForms） |
| 说明依据 | 全部源码走查 + MSBuild 编译验证（0 error / 0 warning）；设计令牌取自 DSH 前端打包资源中的真实变量表 |
| 备注 | 第九章为已完成的修复与改造记录，第十章为后续改进建议 |
