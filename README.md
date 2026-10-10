# 隅星集 · Nook Constellation

<img src="assets/branding/nook-constellation.png" width="128" alt="隅星集图标：角落中的星座与暖金主星" />

Windows 透明桌面任务组件，固定右下角，以四象限或任务流安排日常工作。

## 使用

解压 `Nook-Constellation-1.0.0-win-x64.zip`，双击 `Install.cmd`。无需管理员权限，自带 .NET 运行环境，适用 Windows 10 2004+ / Windows 11 x64。

支持任务新增与编辑、标题和备注富文本、计划与截止时间、标签、评分、重复任务、状态高亮、系统通知、搜索筛选、历史与回收站、JSON导入导出、Markdown导出和每日备份。

窗口全透明，可放大、隐藏到托盘、置顶和锁定穿透。开机启动默认开启，可在设置关闭。

- [使用说明](docs/USER-GUIDE.md)
- [需求与计划](docs/PLAN.md)
- [验证记录与限制](docs/VERIFICATION.md)
- [图标预览](assets/branding/icon-preview.png)

## 开发

Windows .NET 8 SDK：

```powershell
dotnet build src/Nook.Constellation/Nook.Constellation.csproj -c Release -o artifacts/dev
powershell -ExecutionPolicy Bypass -File scripts/verify-core.ps1
powershell -ExecutionPolicy Bypass -File scripts/verify-ui.ps1
powershell -ExecutionPolicy Bypass -File scripts/package.ps1
```

UI测试会操作当前桌面；测试使用独立临时目录，不写入正式任务数据。Windows 正式程序和任务数据保存在 `E:\NookConstellation`，任务数据库和设置位于其 `data` 子目录。

图标源文件为 SVG，Windows ICO 和高清 PNG 均已提供；`scripts/generate-icons.py` 使用 Pillow 生成资源。

版本记录见 `CHANGELOG.zh.md`。按用户要求完成后统一更新版本、推送分支与标签；推送记录保存在本机 `.local/push-records.md`，并同步到指定 Obsidian 目录。
