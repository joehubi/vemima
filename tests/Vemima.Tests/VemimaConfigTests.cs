using Vemima.Core.Configuration;

namespace Vemima.Tests;

public class VemimaConfigTests
{
    private static VemimaConfig ValidConfig() => new()
    {
        CloudSettings = { Host = "hubercloud", Share = "hubercloud", User = "u", Password = "p" },
        Server = { Port = 5000, AllowedClientIps = ["127.0.0.1"] },
    };

    [Fact]
    public void Validate_AcceptsDefaultsWithMinimalSettings()
    {
        Assert.Empty(ValidConfig().Validate());
    }

    [Theory]
    [InlineData("..\\Riesenhaus")]
    [InlineData("C:\\Riesenhaus")]
    [InlineData("a\\b")]
    [InlineData("")]
    public void Validate_RejectsUnsafeObjectFolder(string folder)
    {
        var config = ValidConfig();
        config.Storage.ObjectFolder = folder;

        Assert.Contains(config.Validate(), error => error.StartsWith("Storage.ObjectFolder"));
    }

    [Fact]
    public void Validate_AllowsNestedBaseFolder()
    {
        var config = ValidConfig();
        config.Storage.BaseFolder = @"02_Haushalt\vemima";

        Assert.Empty(config.Validate());
    }

    [Theory]
    [InlineData("::1")]
    [InlineData("nicht-eine-ip")]
    public void Validate_RejectsNonIPv4ClientAddresses(string address)
    {
        var config = ValidConfig();
        config.Server.AllowedClientIps = [address];

        Assert.NotEmpty(config.Validate());
    }

    [Fact]
    public void SaveAndLoad_RoundTripsAndLeavesNoTemporaryFile()
    {
        var directory = Directory.CreateTempSubdirectory("vemima-config-");
        try
        {
            var path = Path.Combine(directory.FullName, VemimaConfig.FileName);
            var config = ValidConfig();
            config.Storage.BackupsToKeep = 7;

            VemimaConfig.Save(path, config);
            var loaded = VemimaConfig.Load(path);

            Assert.Equal("hubercloud", loaded.CloudSettings.Host);
            Assert.Equal(7, loaded.Storage.BackupsToKeep);
            Assert.Equal([VemimaConfig.FileName], directory.GetFiles().Select(file => file.Name));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void ExampleConfig_PassesValidation()
    {
        var path = FindUpwards("config.example.json");
        var config = VemimaConfig.Load(path);

        Assert.Empty(config.Validate());
    }

    private static string FindUpwards(string fileName)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException(fileName);
    }
}
