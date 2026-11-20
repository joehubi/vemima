using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vemima.Core.Configuration;

public sealed class VemimaConfig
{
    public const string FileName = "vemima.config.json";
    public const string EnvironmentVariable = "VEMIMA_CONFIG";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    [JsonPropertyName("Cloud-Settings")]
    public CloudSettings CloudSettings { get; set; } = new();

    public ServerSettings Server { get; set; } = new();

    public StorageSettings Storage { get; set; } = new();

    public string DatabasePath { get; set; } = "vemima.db";

    /// <summary>Liest die JSON-Konfiguration (Kommentare und nachgestellte Kommas erlaubt).</summary>
    public static VemimaConfig Load(string path)
    {
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<VemimaConfig>(stream, JsonOptions)
            ?? throw new InvalidDataException($"Konfiguration '{path}' ist leer.");
    }

    /// <summary>Schreibt die Konfiguration erst in eine temporäre Datei und ersetzt danach die Zieldatei.</summary>
    public static void Save(string path, VemimaConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var fullPath = Path.GetFullPath(path);
        var temporaryPath = $"{fullPath}.{Guid.NewGuid():N}.tmp";

        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, config, JsonOptions);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    /// <summary>VEMIMA_CONFIG, sonst die nächste vemima.config.json oberhalb von Programm- oder Arbeitsverzeichnis.</summary>
    public static string? Locate()
    {
        var env = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(env))
        {
            return File.Exists(env) ? env : null;
        }

        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, FileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(CloudSettings.Host)
            || CloudSettings.Host.Contains('\\')
            || CloudSettings.Host.Contains('/'))
        {
            errors.Add("Cloud-Settings.Host muss ein Rechnername oder eine IP-Adresse ohne Pfad sein.");
        }

        if (string.IsNullOrWhiteSpace(CloudSettings.Share)
            || CloudSettings.Share is "." or ".."
            || CloudSettings.Share.Contains('\\')
            || CloudSettings.Share.Contains('/')
            || CloudSettings.Share.Contains(':'))
        {
            errors.Add("Cloud-Settings.Share muss ein einzelner Freigabename sein.");
        }

        if (Server.Port is < 1 or > 65535)
        {
            errors.Add("Server.Port muss zwischen 1 und 65535 liegen.");
        }

        if (Server.AllowedClientIps.Count == 0)
        {
            errors.Add("Mindestens eine erlaubte IPv4-Adresse muss eingetragen sein.");
        }
        else
        {
            foreach (var ip in Server.AllowedClientIps)
            {
                if (!IPAddress.TryParse(ip, out var address) || address.AddressFamily != AddressFamily.InterNetwork)
                {
                    errors.Add($"'{ip}' ist keine gültige IPv4-Adresse für Server.AllowedClientIps.");
                }
            }
        }

        ValidateRelativeFolder(Storage.BaseFolder, "Storage.BaseFolder", allowNested: true, errors);
        ValidateRelativeFolder(Storage.ObjectFolder, "Storage.ObjectFolder", allowNested: false, errors);
        ValidateRelativeFolder(Storage.InboxFolder, "Storage.InboxFolder", allowNested: false, errors);
        ValidateRelativeFolder(Storage.BackupFolder, "Storage.BackupFolder", allowNested: false, errors);

        if (Storage.BackupsToKeep < 1)
        {
            errors.Add("Storage.BackupsToKeep muss mindestens 1 sein.");
        }

        if (Storage.MaxFileSizeMb <= 0)
        {
            errors.Add("Storage.MaxFileSizeMb muss größer als 0 sein.");
        }

        if (Storage.AllowedExtensions.Count == 0)
        {
            errors.Add("Mindestens eine erlaubte Dateiendung muss eingetragen sein.");
        }
        else
        {
            foreach (var extension in Storage.AllowedExtensions)
            {
                if (extension.Length < 2
                    || extension[0] != '.'
                    || extension.Skip(1).Any(character => !char.IsLetterOrDigit(character)))
                {
                    errors.Add($"'{extension}' ist keine gültige Dateiendung wie '.pdf'.");
                }
            }
        }

        if (string.IsNullOrWhiteSpace(DatabasePath))
        {
            errors.Add("DatabasePath darf nicht leer sein.");
        }

        return errors;
    }

    private static void ValidateRelativeFolder(string folder, string name, bool allowNested, List<string> errors)
    {
        var segments = folder.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
        if (string.IsNullOrWhiteSpace(folder)
            || Path.IsPathRooted(folder)
            || segments.Length == 0
            || (!allowNested && segments.Length > 1)
            || segments.Any(segment => segment is "." or ".." || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
        {
            errors.Add($"{name} muss ein sicherer, relativer Ordnername innerhalb der Freigabe sein.");
        }
    }
}

public sealed class CloudSettings
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 445;
    public string Share { get; set; } = "";
    public string User { get; set; } = "";
    public string Password { get; set; } = "";

    [JsonIgnore]
    public string SharePath => $@"\\{Host}\{Share}";
}

public sealed class ServerSettings
{
    public int Port { get; set; } = 5000;
    public List<string> AllowedClientIps { get; set; } = new();
}

public sealed class StorageSettings
{
    public string BaseFolder { get; set; } = @"02_Haushalt\vemima";
    public string ObjectFolder { get; set; } = "Riesenhaus";
    public string InboxFolder { get; set; } = "Eingang";
    public string BackupFolder { get; set; } = "Datenbank";
    public int BackupsToKeep { get; set; } = 5;
    public int MaxFileSizeMb { get; set; } = 100;
    public List<string> AllowedExtensions { get; set; } = new() { ".pdf", ".jpg", ".jpeg", ".png", ".heic", ".webp" };
}
