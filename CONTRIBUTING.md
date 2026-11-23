# 贡献指南

感谢关注 **搞机工具箱 · 本机桥接**。

## 环境

- Windows 10/11
- 系统自带 .NET Framework 4.x（`csc.exe`）
- 可选：本机已装 Android `platform-tools`

## 改代码

1. Fork / 克隆本仓库
2. 改 `*.cs` 后执行：

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

3. 本地双击 `dist\搞机工具箱-本机桥接.exe`，配合 [sj.aikex.ink](https://sj.aikex.ink/) 测无线连接

## 提交建议

- 保持只监听 `127.0.0.1`，不要默认对外网开端口
- UI 色值跟网站靛蓝（约 `#4657be`）对齐
- 免责声明门禁不要绕过
- PR 说明：改了什么、怎么测的

## 许可证

贡献代码默认按 **GPL-3.0** 授权（见 `LICENSE`）。
