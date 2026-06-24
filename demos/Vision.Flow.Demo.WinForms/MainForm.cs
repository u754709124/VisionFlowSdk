using System.Windows.Forms;

namespace Vision.Flow.Demo.WinForms
{
    public sealed class MainForm : Form
    {
        public MainForm()
        {
            Text = "Vision Flow Runtime Demo";
            Width = 1000;
            Height = 700;
            Controls.Add(new Label
            {
                Text = "Vision Flow Runtime Demo",
                Dock = DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter
            });
        }
    }
}
