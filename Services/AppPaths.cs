using System.IO;

namespace MyTask.Services;

public sealed class AppPaths
{
    public string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MyTask");

    public string Scripts => Path.Combine(Root, "Scripts");
    public string Recorded => Path.Combine(Root, "Recorded");
    public string Logs => Path.Combine(Root, "Logs");
    public string Config => Path.Combine(Root, "Config");

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(Scripts);
        Directory.CreateDirectory(Recorded);
        Directory.CreateDirectory(Logs);
        Directory.CreateDirectory(Config);
    }
}
