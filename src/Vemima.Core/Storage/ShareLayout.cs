using System.Globalization;
using Vemima.Core.Configuration;

namespace Vemima.Core.Storage;

/// <summary>Berechnet die Ordner auf der Freigabe (Eingang, Jahresordner, Backup) aus der Konfiguration.</summary>
public sealed class ShareLayout(CloudSettings cloud, StorageSettings storage)
{
    public string BasePath => Path.Combine(cloud.SharePath, storage.BaseFolder);

    public string ObjectPath => Path.Combine(BasePath, storage.ObjectFolder);

    public string InboxPath => Path.Combine(ObjectPath, storage.InboxFolder);

    public string BackupPath => Path.Combine(BasePath, storage.BackupFolder);

    /// <summary>Jahresordner des Objekts, z. B. ...\Riesenhaus\2025.</summary>
    public string YearPath(int year)
    {
        if (year is < 1900 or > 2200)
        {
            throw new ArgumentOutOfRangeException(nameof(year), year, "Jahr außerhalb des gültigen Bereichs.");
        }

        return Path.Combine(ObjectPath, year.ToString(CultureInfo.InvariantCulture));
    }
}
