# Monitor Switcher

Monitor Switcher 是一个轻量的 Windows 托盘工具，用于在笔记本内置屏幕和外接显示器之间快速切换，并提供局域网 HTTP 接口，方便通过 iPhone 快捷指令远程触发。

它适合这样的使用场景：

- 笔记本在桌面模式和移动模式之间频繁切换
- 合盖后只使用外接显示器
- 离开工位时快速切回电脑屏幕
- 希望通过 iPhone 一键切换显示输出

## 特性

- 常驻系统托盘，右键即可切换显示器
- 支持切换到仅电脑屏幕
- 支持切换到仅外接显示器
- 支持“来回切换”快捷操作
- 支持开机自启动
- 支持局域网 HTTP 控制接口
- 支持 iPhone 快捷指令集成
- 自动生成本地配置与访问 token

## 工作方式

项目基于 Windows 自带的 `DisplaySwitch.exe`：

- `DisplaySwitch.exe /internal`：仅电脑屏幕
- `DisplaySwitch.exe /external`：仅第二屏幕

因此这个项目聚焦于最常见的“双屏切换”需求，而不是完整的多显示器布局管理器。如果你需要扩展模式下切换主屏、调整坐标布局、记忆复杂显示配置，这个项目暂时不覆盖。

## 适用系统

- Windows 10
- Windows 11
- .NET 8

## 快速开始

### 运行源码

```powershell
dotnet restore
dotnet run
```

### 发布单文件程序

```powershell
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

发布完成后，建议直接使用生成的 exe，并在该版本上开启“开机自启动”。

## 发布 Release

仓库已经配置了 GitHub Actions 自动发布流程。

当你创建并推送形如 `v0.1.0` 的 tag 时，GitHub 会自动：

- 在 Windows 环境编译项目
- 生成 `win-x64` 自包含单文件发布包
- 打包为 zip
- 创建对应的 GitHub Release
- 将 zip 附件上传到 Release 页面

发布命令：

```bash
git tag v0.1.0
git push origin v0.1.0
```

工作流文件位于：

- `.github/workflows/build.yml`：普通构建
- `.github/workflows/release.yml`：tag 触发的自动发布

## 托盘菜单

程序启动后不会弹出主窗口，而是常驻系统托盘。

托盘菜单支持：

- 切换到主显示器（仅电脑屏幕）
- 切换到外接显示器（仅第二屏幕）
- 切换当前显示器
- 开启或关闭开机自启动
- 复制 iPhone 快捷指令控制地址
- 查看当前控制信息

双击托盘图标会显示当前局域网控制地址。

## 开机自启动

开机自启动使用当前用户注册表项：

`HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`

这意味着：

- 不需要管理员权限
- 只对当前 Windows 用户生效
- 建议在最终发布路径上启用，避免后续移动 exe 导致启动项指向旧路径

## iPhone 快捷指令控制

应用启动后会在本地生成配置文件：

`%LocalAppData%\MonitorSwitcher\config.json`

默认配置包括：

- `Port`：HTTP 服务端口，默认 `8765`
- `ApiToken`：局域网访问 token

在 iPhone 快捷指令中，使用“获取 URL 内容”动作即可触发切换。

切换到电脑屏幕：

```text
http://你的电脑IP:8765/api/switch/internal?token=你的ApiToken
```

切换到外接显示器：

```text
http://你的电脑IP:8765/api/switch/external?token=你的ApiToken
```

执行来回切换：

```text
http://你的电脑IP:8765/api/switch/toggle?token=你的ApiToken
```

建议使用 `GET` 请求。

## HTTP API

查询状态：

```text
GET /api/status?token=...
```

切换到电脑屏幕：

```text
GET /api/switch/internal?token=...
```

切换到外接显示器：

```text
GET /api/switch/external?token=...
```

执行来回切换：

```text
GET /api/switch/toggle?token=...
```

## 网络说明

- iPhone 与 Windows 电脑需要处于同一局域网
- Windows 首次监听端口时可能弹出防火墙提示，请允许专用网络访问
- 某些公司网络或访客网络会阻止设备间互访

## 项目结构

- `MonitorSwitcherApplicationContext.cs`：托盘应用与菜单逻辑
- `HttpControlServer.cs`：局域网 HTTP 控制服务
- `DisplaySwitcher.cs`：调用 Windows 原生显示切换命令
- `StartupManager.cs`：开机自启动注册表管理
- `AppConfigStore.cs`：本地配置与 token 管理

## 开发说明

本项目使用：

- .NET 8
- Windows Forms
- GitHub Actions 自动构建

如果你希望扩展这个项目，比较自然的方向包括：

- 支持更多显示模式，比如扩展和复制
- 支持自定义端口与 token 重置
- 支持更丰富的快捷指令返回结果
- 提供安装包和自动更新
