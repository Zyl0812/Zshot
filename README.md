<div align="center">

<img src="src/logo.png" width="120" alt="Zshot Logo">

# Zshot

**常驻托盘的 Windows 截图工具**

标注编辑 · 本地 OCR · 自带 API 翻译 · 长截图 · 真 HDR 管线

[![Release](https://img.shields.io/github/v/release/Zyl0812/Zshot?style=flat-square)](../../releases)
[![License](https://img.shields.io/badge/license-MIT-blue?style=flat-square)](https://github.com/Zyl0812/Zshot?tab=MIT-1-ov-file)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-0078D6?style=flat-square&logo=windows)](../../releases)

[下载](../../releases) · [快速上手](#快速上手) · [功能](#功能) · [从源码构建](#从源码构建)

**简体中文** | **[English](docs/README.en.md)**

</div>

---

## 这是什么

按下热键 → 框选 → 标注 / 取字 / 翻译 / 接着往下滚 → 自动保存并复制 → 回到托盘。

全程不出现主窗口。没有启动器，没有欢迎页，没有图库。设置是从托盘单独打开的一个窗口，关掉它进程不退出。

Zshot 基于 [Starshot](https://github.com/loliri/Starshot) 二次开发，继承了它那套 16bit HDR 捕获与编解码管线，在其之上把交互重做成了 PixPin / QQ 那一类的即用即走截图工具。

## 为什么

主流截图工具二选一：要么交互顺手但截 HDR 屏幕发灰过曝，要么色彩正确却没有标注和取字。

- **HDR 屏幕上截图是对的。** 直接从 DXGI 拿 `R16G16B16A16Float` scRGB 帧缓冲，不经过系统合成器的 8bit 压缩，高光不削顶、色域不收窄。
- **取字不出机器。** OCR 用本地 PP-OCRv6 模型推理，截图不上传任何服务器。
- **翻译用你自己的 API。** 填自己的 Base URL 和 Key，不经过任何中间服务，密钥存 Windows 凭据库。

## 快速上手

在 [Releases](../../releases) 下载与你的系统架构对应的 `setup.exe` 安装包；如果不想安装，也可以下载 ZIP，完整解压后运行根目录的 `Zshot.exe`。

装好后程序静默进托盘，直接按热键即可：

| 热键 | 动作 |
| --- | --- |
| `Alt` + `Q` | 区域截图（最常用） |
| `Alt` + `W` | 全屏截图 |
| `Alt` + `A` | 区域截图，只进剪贴板不存文件 |

托盘左键单击 = 区域截图；右键出菜单：全屏截图 / 区域截图 / 区域截图（仅复制）/ 设置 / 退出。

框选完成后覆盖层不会关闭，直接就在选区上标注、取字、翻译或继续长截图，按 `Enter` 或点「完成」结束。

默认保存到 `图片\Zshot`，同时自动复制到剪贴板，两个开关都可以在设置里单独关掉。

## 功能

### 区域截图覆盖层

冻结帧渲染，框选期间画面不会变。窗口自动探测（悬停高亮、可吸附到客户区）、像素放大镜、实时坐标、多显示器跨屏框选。选区确认后可以八向缩放和拖动。

`Esc` 或右键取消，`Enter` 确认。

### 标注编辑

在选区内直接画，不弹独立编辑窗：

矩形 · 椭圆 · 直线 · 箭头 · 画笔 · 文字 · 马赛克 · 序号标记

支持选中、移动、删除元素，撤销 / 重做 / 清空，颜色与线宽可调，马赛克有细 / 中 / 粗三档。

马赛克走 GPU 降采样重绘，实时预览不掉帧。

**工具栏可自定义**：17 个按钮都能在设置里显示或隐藏。默认隐藏椭圆、文字、序号、重做四项，保持工具栏精简；需要时勾上即可。

### 本地 OCR

选区里的文字一键提取，识别结果可整段复制。

- 模型是 **PP-OCRv6 Small**（RapidOcrNet + ONNX Runtime），随安装包一起发布，装完即用，不需要联网下载
- 多语言单模型：简体中文、繁体中文、英文、日文及 46 种拉丁语系
- 按行输出并还原自然阅读顺序，中文不会逐字插空格
- **全程本地推理，截图不上传**
- 典型选区约 1 秒出结果；模型文件缺失时自动回退到 Windows 内置 OCR

### AI 翻译

OCR 取出文字后一键翻译，用你自己的 API：

- 兼容 OpenAI 格式的任意服务，填 Base URL / API Key / 模型名
- 目标语言、系统提示词、超时（5~120 秒）都可配
- API Key 存进 Windows 凭据库，不写进配置文件，也不会出现在日志里
- 对返回格式做了容错：网关返回 HTML 错误页、流式 `delta`、缺字段都不会让程序崩

### 长截图

固定选区，手动滚动页面，自动检测新内容并拼接。

- 按重叠区域做灰度 SAD 匹配对齐，避免重复条带
- 实时显示已捕获段数与总高度
- 上限 16384px（D3D11 纹理尺寸上限），达到上限会提示
- 拼接期间覆盖层自动排除出捕获，不会把自己拍进去
- HDR 屏幕下同样输出正确的 SDR 结果

### HDR 截图管线

继承自 Starshot 的核心能力，本次二开未改动其算法：

- 全程 16bit：`R16G16B16A16Float` scRGB 捕获 → 编码，不做有损 tone mapping
- HDR 输出格式：AVIF（默认）/ JPEG XL / PNGv3，带 BT.2020 色域与 PQ 传输函数元数据
- SDR 输出格式：PNG（默认）/ AVIF / JPEG XL
- 智能判定内容是否真 HDR，SDR 内容不会被塞进 HDR 容器白占空间
- 编码质量三档可选
- SDR 显示器上自动走降级路径

> 标注在 SDR 图层上进行，HDR 原图与标注后的 SDR 输出是两条独立的保存管线。

### 保存与剪贴板

- 自动保存到文件、自动复制到剪贴板，两个开关独立
- 「仅复制」热键永不写文件，无视自动保存设置
- 保存和复制发生在编辑器点「完成」之后，不是按下热键的瞬间
- 保存目录可改，支持一键打开

### 文件名模板

全屏截图与区域截图使用**各自独立**的模板，在「设置 → 存储」里配置。

| 占位符 | 含义 | 示例 |
| --- | --- | --- |
| `{process}` | 进程名（不含扩展名） | `explorer` |
| `{processPath}` | EXE 文件名（含扩展名） | `explorer.exe` |
| `{title}` | 窗口标题（去空白，长度可截断） | `原神` |
| `{timestamp}` | Unix 时间戳（秒） | `1721234567` |
| `{time}` | `yyyyMMdd_HHmmssff` | `20260718_14302512` |
| `{date}` | `yyyyMMdd` | `20260718` |
| `{width}` `{height}` | 图像宽高（像素） | `1920` `1080` |
| `{year}` `{month}` `{day}` | 年 / 月 / 日 | `2026` `07` `18` |
| `{hour}` `{minute}` `{second}` | 时 / 分 / 秒 | `14` `30` `25` |

文件名中的非法字符统一替换为 `_`。

### 托盘与热键

- 开机自启（写注册表 Run 项或计划任务，可实时检测外部禁用）
- 三个全局热键均可自定义
- 二次启动不会打开任何窗口
- 更新检查：发现新版本后流式下载、解压、原地替换，支持差分补丁

## 已知限制

- OCR 单次约 1 秒（CPU 推理，PP-OCRv6 Small 的固有开销），点击后会显示「识别中…」
- 长截图需要手动滚动，不会自动滚屏；上限 16384px
- HDR 截图需要 HDR 显示器，SDR 屏幕自动降级
- 翻译依赖你自己提供的 API，程序不内置任何翻译服务

## 系统要求

- Windows 10 / 11（推荐 11）
- x64 / arm64
- HDR 截图需要 HDR 显示器，否则自动走 SDR 路径

## 架构

### 目录结构

```
src/
├── Zshot/              主程序（WinUI 3）
│   └── Features/
│       ├── Screenshot/     捕获、覆盖层、编辑器、OCR、长截图
│       ├── Codec/          HDR 编解码与色彩管理
│       ├── Setting/        设置页
│       ├── Update/         更新检查与差分升级
│       └── ViewHost/       托盘窗口、设置窗口
├── Zshot.Core/         纯逻辑，无 UI 依赖，单测覆盖
│   ├── Editor/             标注元素与撤销栈
│   ├── Overlay/           选区几何与工具栏定位
│   ├── Ocr/               阅读顺序整理
│   ├── LongCapture/       帧对齐与拼接
│   └── Translation/       翻译请求与响应解析
├── Zshot.Language/     多语言资源（en / zh-CN / ja-JP）
└── Zshot.Launcher/     原生 C++ 启动器
tests/
└── Zshot.Core.Tests/   Zshot.Core 的单元测试
```

`Zshot.Core` 刻意不引用任何 WinUI / DirectX 类型，几何计算、撤销栈、拼接对齐、保存策略、阅读顺序这些逻辑都能直接跑单测。

### 技术栈

| 层 | 技术 |
| --- | --- |
| UI 框架 | WinUI 3（Windows App SDK 1.8） |
| 运行时 | .NET 10 |
| 图形 | Win2D 1.3（D3D11 互操作、HDR tone mapping） |
| 编解码 | Starward.Codec（libavif / libjxl / UltraHDR） |
| OCR | RapidOcrNet 4.0.2 + ONNX Runtime + SkiaSharp |
| OCR 模型 | PP-OCRv6 Small（det + rec）+ PP-OCRv5 方向分类器 |
| 数据存储 | SQLite + Dapper |
| 日志 | Serilog |
| 托盘 | H.NotifyIcon.WinUI |
| 启动器 | 原生 C++（v145 工具集，静态 CRT） |

## 从源码构建

### 前置要求

- Visual Studio 2022（含「使用 C++ 的桌面开发」工作负载，构建启动器需要 MSBuild）
- .NET 10 SDK
- Windows 11 SDK

### 步骤

```bash
# === Debug ===
# 主程序（输出到 build/app/）
dotnet build src/Zshot/Zshot.csproj -c Debug -p:Platform=x64

# 启动器（输出到 build/Zshot.exe，需要 VS 的 MSBuild）
msbuild src/Zshot.Launcher/Zshot.Launcher.vcxproj -p:Configuration=Debug -p:Platform=x64

# 单元测试
dotnet test tests/Zshot.Core.Tests/Zshot.Core.Tests.csproj

# === Release 发布 ===
dotnet publish src/Zshot/Zshot.csproj -c Release -p:Platform=x64 -r win-x64
```

### OCR 模型

模型不入库，由 CI 在打包时从 [RapidOCR 官方清单](https://github.com/RapidAI/RapidOCR/blob/main/python/rapidocr/default_models.yaml) 下载并校验 SHA256，放进发布包的 `models/v6/`。

本地开发时若该目录为空，OCR 会自动回退到 Windows 内置引擎。想在本地用 PP-OCRv6，把这三个文件放到 `build/app/models/v6/` 即可：

| 文件 | 大小 |
| --- | --- |
| `PP-OCRv6_det_small.onnx` | 9.5 MB |
| `PP-OCRv6_rec_small.onnx` | 20.3 MB |
| `ppocrv6_dict.txt` | 70 KB |

方向分类器 `models/v5/ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx` 由 RapidOcrNet 包自带，无需手动准备。

## 常见问题

**截图发灰 / 过曝？**
确认在设置里选了合适的 HDR 输出格式。若显示器不是 HDR，程序会自动走 SDR 路径，此时输出的就是普通 SDR 图。

**OCR 第一次特别慢？**
首次调用要加载 ONNX 模型（约 200~400ms），之后进程内复用。稳定后典型选区约 1 秒。

**OCR 认不出某种语言？**
PP-OCRv6 Small 覆盖中日英与拉丁语系。韩文、阿拉伯文等不在支持范围内。

**翻译报错？**
先确认 Base URL 填的是兼容 OpenAI 格式的地址（通常以 `/v1` 结尾即可，程序会自动补 `/chat/completions`）。错误信息会直接显示 API 返回的状态码。

**长截图拼接错位？**
滚动慢一点。每次滚动的距离若超过选区高度，相邻两帧就没有重叠区域可供对齐，该帧会被丢弃。

**关掉设置窗口程序就退出了？**
不会。设置是独立窗口，关掉后程序继续留在托盘。要退出请用托盘右键菜单的「退出」。

## 致谢

- [Starshot](https://github.com/loliri/Starshot) — HDR 捕获 / 编解码 / 覆盖层底座，作者 [@loliri](https://github.com/loliri)（MIT）
- [RapidOcrNet](https://github.com/BobLd/RapidOcrNet) — OCR 推理封装，作者 BobLd 与 RapidAI（Apache-2.0）
- [PaddleOCR](https://github.com/PaddlePaddle/PaddleOCR) — PP-OCRv6 / PP-OCRv5 模型，作者 PaddlePaddle（Apache-2.0）
- [ONNX Runtime](https://github.com/microsoft/onnxruntime) · [SkiaSharp](https://github.com/mono/SkiaSharp) · [Win2D](https://github.com/microsoft/Win2D)（MIT）

完整的第三方声明见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。

## 许可证

MIT，见 [LICENSE](LICENSE)。

Zshot 是基于 Starshot 二次开发的独立产品，不是 Starshot 官方版本。
