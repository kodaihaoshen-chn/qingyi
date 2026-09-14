using System;
using System.Net;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("轻译")]
[assembly: System.Reflection.AssemblyDescription("轻量中英文互译与本地朗读工具")]
[assembly: System.Reflection.AssemblyCompany("轻译")]
[assembly: System.Reflection.AssemblyProduct("轻译")]
[assembly: System.Reflection.AssemblyVersion("1.0.1.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.0.1.0")]

namespace QingYi
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
