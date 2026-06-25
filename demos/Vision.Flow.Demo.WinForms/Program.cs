using System;
using System.Windows.Forms;

namespace Vision.Flow.Demo.WinForms
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var runtimePath = args != null && args.Length > 0 ? args[0] : null;
            Application.Run(new MainForm(runtimePath));
        }
    }
}
