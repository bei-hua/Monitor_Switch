using System.Windows.Forms;

namespace MonitorSwitcher;

internal sealed class MonitorSwitcherApplicationContext : ApplicationContext
{
    private readonly AppConfigStore _configStore;
    private readonly HttpControlServer _server;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _statusMenuItem;
    private readonly SynchronizationContext _uiContext;

    public MonitorSwitcherApplicationContext()
    {
        _uiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        _configStore = new AppConfigStore();
        _statusMenuItem = new ToolStripMenuItem
        {
            Enabled = false
        };

        var contextMenu = new ContextMenuStrip();
        contextMenu.Opening += HandleMenuOpening;
        contextMenu.Items.Add(_statusMenuItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(CreateMenuItem("切换到主显示器（仅电脑屏幕）", (_, _) => SwitchDisplay(DisplayTarget.Internal)));
        contextMenu.Items.Add(CreateMenuItem("切换到外接显示器（仅第二屏幕）", (_, _) => SwitchDisplay(DisplayTarget.External)));
        contextMenu.Items.Add(CreateMenuItem("切换当前显示器", (_, _) => SwitchDisplay(_configStore.GetNextToggleTarget())));
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(CreateMenuItem("复制 iPhone 快捷指令控制地址", (_, _) => CopyControlInfo()));
        contextMenu.Items.Add(CreateMenuItem("查看控制信息", (_, _) => ShowControlInfo()));
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(CreateMenuItem("退出", (_, _) => ExitApplication()));

        _notifyIcon = new NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "Monitor Switcher",
            Visible = true,
            ContextMenuStrip = contextMenu
        };
        _notifyIcon.DoubleClick += (_, _) => ShowControlInfo();

        _server = new HttpControlServer(_configStore, SwitchDisplay, GetStatusText);
        try
        {
            _server.Start();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"HTTP 服务启动失败: {ex.Message}",
                "Monitor Switcher",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        ShowBalloon("程序已运行。右键托盘图标可切换显示器，也可通过局域网接口控制。", ToolTipIcon.Info);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _server.Dispose();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }

        base.Dispose(disposing);
    }

    private void HandleMenuOpening(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _statusMenuItem.Text = $"当前状态: {GetStatusText()}";
    }

    private ToolStripMenuItem CreateMenuItem(string text, EventHandler onClick)
    {
        var item = new ToolStripMenuItem(text);
        item.Click += onClick;
        return item;
    }

    private bool SwitchDisplay(DisplayTarget target)
    {
        try
        {
            var switched = DisplaySwitcher.Switch(target);
            if (switched)
            {
                _configStore.UpdateLastTarget(target);
            }

            return switched;
        }
        catch (Exception ex)
        {
            ShowBalloon($"切换失败: {ex.Message}", ToolTipIcon.Error);
            return false;
        }
    }

    private string GetStatusText()
    {
        var config = _configStore.GetSnapshot();
        return config.LastTarget == DisplayTarget.Internal
            ? "最近一次切换到主显示器"
            : "最近一次切换到外接显示器";
    }

    private void ShowControlInfo()
    {
        MessageBox.Show(
            BuildControlInfoText(),
            "Monitor Switcher 控制信息",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void CopyControlInfo()
    {
        var text = BuildControlInfoText();
        Clipboard.SetText(text);
        ShowBalloon("控制地址已复制到剪贴板。", ToolTipIcon.Info);
    }

    private string BuildControlInfoText()
    {
        var config = _configStore.GetSnapshot();
        var addresses = NetworkAddressProvider.GetLanAddresses();
        var lines = new List<string>
        {
            "iPhone 快捷指令可直接请求以下地址：",
            string.Empty
        };

        foreach (var address in addresses)
        {
            var baseUrl = $"http://{address}:{config.Port}";
            lines.Add(baseUrl);
            lines.Add($"  主显示器: {baseUrl}/api/switch/internal?token={config.ApiToken}");
            lines.Add($"  外接显示器: {baseUrl}/api/switch/external?token={config.ApiToken}");
            lines.Add($"  来回切换: {baseUrl}/api/switch/toggle?token={config.ApiToken}");
            lines.Add(string.Empty);
        }

        lines.Add("请确保 iPhone 与电脑处于同一局域网，并允许 Windows 防火墙放行专用网络访问。");
        return string.Join(Environment.NewLine, lines);
    }

    private void ExitApplication()
    {
        ExitThread();
    }

    private void ShowBalloon(string message, ToolTipIcon icon)
    {
        _uiContext.Post(_ =>
        {
            _notifyIcon.ShowBalloonTip(3000, "Monitor Switcher", message, icon);
        }, null);
    }
}
