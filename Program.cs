using System.Windows.Forms;

namespace MonitorSwitcher;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MonitorSwitcherApplicationContext());
    }
}
