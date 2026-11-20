using Vemima.Core.Configuration;
using Vemima.Core.Storage;

namespace Vemima.Tests;

public class ShareLayoutTests
{
    private static ShareLayout Layout() => new(
        new CloudSettings { Host = "hubercloud", Share = "hubercloud" },
        new StorageSettings());

    [Fact]
    public void Paths_FollowTheDocumentedFolderStructure()
    {
        var layout = Layout();

        Assert.Equal(@"\\hubercloud\hubercloud\02_Haushalt\vemima", layout.BasePath);
        Assert.Equal(@"\\hubercloud\hubercloud\02_Haushalt\vemima\Riesenhaus\Eingang", layout.InboxPath);
        Assert.Equal(@"\\hubercloud\hubercloud\02_Haushalt\vemima\Datenbank", layout.BackupPath);
        Assert.Equal(@"\\hubercloud\hubercloud\02_Haushalt\vemima\Riesenhaus\2025", layout.YearPath(2025));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1800)]
    [InlineData(9999)]
    public void YearPath_RejectsImplausibleYears(int year)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Layout().YearPath(year));
    }
}
