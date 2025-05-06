// AppConfig.cs — конфигурация приложения

namespace Sentra.Config;

public static class AppConfig
{
    public static string AiServerUrl { get; set; } = "http://localhost:8000/embed";
    public static string RootDirectoryToIndex { get; set; } = "C:\\test";
    public static string DatabasePath { get; set; } = "sentra_index.db";

    public static bool IndexAllDrives { get; set; } = false;

    public static List<string> ExcludedPaths { get; set; } = new()
    {
        "C:\\Windows",
        "C:\\Program Files",
        "C:\\Program Files (x86)",
        "C:\\System Volume Information",
        "C:\\$Recycle.Bin"
    };

    public static List<string> GetTargetFoldersToIndex()
    {
        if (!IndexAllDrives)
            return new List<string> { RootDirectoryToIndex };

        var drives = DriveInfo.GetDrives()
            .Where(d => d.DriveType == DriveType.Fixed && d.IsReady)
            .Select(d => d.RootDirectory.FullName);

        return drives.ToList();
    }
}