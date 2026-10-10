# 隅星集 · Nook Constellation

Windows 透明桌面任务组件。当前为 **0.1.0-preview.1 桌面基础开发预览**，尚不支持任务管理。

## 开发与运行

需要 Windows 10/11 x64 和 .NET 8 SDK。在仓库目录的 PowerShell 中运行：

```powershell
dotnet build src/Nook.Constellation/Nook.Constellation.csproj -c Release
dotnet run --project src/Nook.Constellation/Nook.Constellation.csproj
```

窗口固定右下角，可放大、隐藏到托盘、置顶。托盘菜单提供鼠标穿透锁定和解锁。设置默认启用当前用户开机启动，运行开发预览时也会注册；可在设置中关闭。

当前图标采用系统占位图标。任务录入按钮尚未启用。设置保存在 `%LOCALAPPDATA%\NookConstellation`。

计划见 [docs/PLAN.md](docs/PLAN.md)，验收见 [docs/VERIFICATION.md](docs/VERIFICATION.md)。每阶段验收后递增版本并推送，推送记录保存在本机 `.local/push-records.md`。
