# Monitor Switcher

一个 Windows 托盘应用，用于在笔记本主屏和外接显示器之间快速切换，支持两种方式：

- 系统托盘右键菜单切换
- iPhone 在同一局域网下通过快捷指令触发切换

## 功能说明

本程序调用 Windows 自带的 `DisplaySwitch.exe`：

- `主显示器（仅电脑屏幕）` 对应 `DisplaySwitch.exe /internal`
- `外接显示器（仅第二屏幕）` 对应 `DisplaySwitch.exe /external`

这适合常见的笔记本 + 外接显示器场景。如果你需要的是更复杂的多显示器布局管理，比如扩展模式下动态改主屏，这个版本没有覆盖。

## 本地开发

要求：

- Windows 10 / 11
- .NET 8 SDK

构建和运行：

```powershell
dotnet restore
dotnet run
```

发布单文件 exe：

```powershell
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

## 托盘使用

启动后应用常驻系统托盘。

- 右键图标可以切换到主显示器
- 右键图标可以切换到外接显示器
- 可以复制局域网控制地址到剪贴板
- 双击图标会弹出当前可用控制地址

## iPhone 快捷指令接入

首次启动后，程序会在以下位置生成配置文件：

`%LocalAppData%\MonitorSwitcher\config.json`

里面会保存：

- `Port`: HTTP 服务端口，默认 `8765`
- `ApiToken`: 局域网调用鉴权 token

在 iPhone 快捷指令里新建两个快捷指令，使用“获取 URL 内容”动作即可：

主显示器：

```text
http://你的电脑IP:8765/api/switch/internal?token=你的ApiToken
```

外接显示器：

```text
http://你的电脑IP:8765/api/switch/external?token=你的ApiToken
```

如果想做单个“来回切换”的快捷指令：

```text
http://你的电脑IP:8765/api/switch/toggle?token=你的ApiToken
```

建议快捷指令请求方法用 `GET`。

## 网络与权限

- iPhone 和 Windows 电脑必须在同一局域网
- Windows 首次监听端口时，系统可能弹出防火墙提示，请允许“专用网络”
- 如果公司网络限制了设备互访，iPhone 可能无法直接访问电脑

## 接口

状态查询：

```text
GET /api/status?token=...
```

切到主显示器：

```text
GET /api/switch/internal?token=...
```

切到外接显示器：

```text
GET /api/switch/external?token=...
```

切换最近一次相反目标：

```text
GET /api/switch/toggle?token=...
```
