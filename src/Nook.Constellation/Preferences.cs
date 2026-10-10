using System;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;
namespace Nook.Constellation;
public sealed class Preferences
{
    public bool AlwaysOnTop { get; set; }
    public bool Expanded { get; set; }
    public bool AutoStart { get; set; } = true;
    public static string DataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NookConstellation");
    private static string FilePath => Path.Combine(DataDirectory, "settings.json");
    public static Preferences Load()
    {
        if (!File.Exists(FilePath)) return new();
        try { return JsonSerializer.Deserialize<Preferences>(File.ReadAllText(FilePath)) ?? new(); }
        catch (JsonException) { File.Copy(FilePath, FilePath + ".invalid-" + DateTime.Now.ToString("yyyyMMddHHmmss"), true); return new(); }
    }
    public void Save()
    {
        Directory.CreateDirectory(DataDirectory);
        File.WriteAllText(FilePath + ".tmp", JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(FilePath + ".tmp", FilePath, true);
    }
    public void ApplyAutoStart()
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if (AutoStart) key.SetValue("NookConstellation", "\"" + Environment.ProcessPath + "\"");
        else key.DeleteValue("NookConstellation", false);
    }
}
