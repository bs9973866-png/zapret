using System.Reflection;
using System.Runtime.InteropServices;

namespace ZFlow;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        try
        {
            NativeAssets.EnsureExtracted();
            NativeLibrary.SetDllImportResolver(typeof(Program).Assembly, ResolveNativeLibrary);
            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Z-Flow не удалось запустить.\r\n\r\n" + ex.Message,
                "Ошибка запуска",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static IntPtr ResolveNativeLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!libraryName.Equals("WinDivert.dll", StringComparison.OrdinalIgnoreCase))
            return IntPtr.Zero;

        string path = Path.Combine(NativeAssets.DirectoryPath, "WinDivert.dll");
        return NativeLibrary.Load(path);
    }
}

internal static class NativeAssets
{
    public static string DirectoryPath { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZFlow", "native");

    public static void EnsureExtracted()
    {
        Directory.CreateDirectory(DirectoryPath);
        Extract("ZFlow.Native.WinDivert.dll", "WinDivert.dll");
        Extract("ZFlow.Native.WinDivert64.sys", "WinDivert64.sys");
        Extract("ZFlow.Native.LICENSE.txt", "WinDivert-LICENSE.txt");
        if (!File.Exists(Path.Combine(DirectoryPath, "WinDivert.dll")) ||
            !File.Exists(Path.Combine(DirectoryPath, "WinDivert64.sys")))
        {
            throw new FileNotFoundException(
                "В сборку не включён драйвер WinDivert. Скачай официальный релиз Z-Flow из GitHub Actions или пересобери через workflow.");
        }
    }

    private static void Extract(string resourceName, string fileName)
    {
        using Stream? resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
        if (resource is null)
            return;

        string target = Path.Combine(DirectoryPath, fileName);
        if (File.Exists(target) && new FileInfo(target).Length == resource.Length)
            return;

        string temp = target + ".tmp";
        using (var output = File.Create(temp))
            resource.CopyTo(output);

        File.Move(temp, target, overwrite: true);
    }
}
