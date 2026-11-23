# 搞机工具箱 · 本机桥接（gj-adb-bridge）

> 作者：**艾阳** · GitHub [@aiyangdie](https://github.com/aiyangdie) · 邮箱 `aike1015@qq.com`  
> 网站：[sj.aikex.ink](https://sj.aikex.ink/) · 许可证：**GPL-3.0**

浏览器无法直接连局域网手机（没有原始 TCP），所以无线调试需要在本机跑这个很小的托盘程序：把网页 WebSocket 转成 ADB。

## 下载使用

1. 打开 https://sj.aikex.ink/ → 「下载本机桥接」
2. 双击 `搞机工具箱-本机桥接.exe`
3. 首次会弹出 **关于 / 免责声明**（请阅读）
4. 托盘常驻后，回网站点「无线连接」

需要本机有 Android `platform-tools`（`adb.exe`）。程序会自动查找常见 SDK 路径。

## 功能

- 仅监听 `127.0.0.1`（默认端口 `17855`）
- 托盘常驻、一键打开网站
- 二维码无线配对（配合网站）
- 开源信息、免责声明、作者头像与 GitHub 入口

## 自己编译（Windows）

依赖：系统自带 .NET Framework 4.8 编译器（`csc.exe`），无需安装 Visual Studio。

```powershell
cd bridge/desktop
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

产物在 `dist/搞机工具箱-本机桥接.exe`。

## 与 USB 的关系

| | USB | 无线 + 本桥接 |
|--|--|--|
| 是否需要本 exe | 否 | 是 |
| 连上后功能 | 相同 | 相同 |
| 稳定性 | 通常更稳 | 依赖 Wi‑Fi；本仓库持续优化 |

## 上游

网站 UI 增强基于 [CzJam/gjgjxweb](https://github.com/CzJam/gjgjxweb)（GPL-3.0）。  
ADB 引擎使用 [yume-chan/ya-webadb](https://github.com/yume-chan/ya-webadb) 生态。

## 免责声明（摘要）

本软件按「现状」提供。请仅在你拥有或已获授权的设备上使用；无线调试 / 卸载 / 清数据等操作有数据丢失风险，请自行备份。继续使用即表示同意完整免责说明（见程序内「关于 / 免责声明」）。

## 反馈

- Issue：本仓库 Issues  
- 邮箱：aike1015@qq.com  
- 作者：https://github.com/aiyangdie
