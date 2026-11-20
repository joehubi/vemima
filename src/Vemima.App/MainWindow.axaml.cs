using Avalonia.Controls;
using Microsoft.AspNetCore.Builder;
using Vemima.Core.Configuration;
using Vemima.Web;

namespace Vemima.App;

public partial class MainWindow : Window
{
    private WebApplication? webHost;

    public MainWindow()
    {
        InitializeComponent();
        Opened += async (_, _) => await StartAsync();
        Closing += async (_, _) => await StopAsync();
    }

    /// <summary>Lädt die Konfiguration und startet den eingebetteten Webserver.</summary>
    private async Task StartAsync()
    {
        var path = VemimaConfig.Locate();
        if (path is null)
        {
            StatusText.Text = $"Keine {VemimaConfig.FileName} gefunden (Vorlage: config.example.json).";
            return;
        }

        try
        {
            var config = VemimaConfig.Load(path);
            var errors = config.Validate();
            if (errors.Count > 0)
            {
                StatusText.Text = $"Konfiguration ungültig: {errors[0]}";
                return;
            }

            webHost = VemimaWebHost.Build(config);
            await webHost.StartAsync();
            StatusText.Text = $"Webserver läuft auf Port {config.Server.Port} · Konfiguration: {path}";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Start fehlgeschlagen: {ex.Message}";
        }
    }

    private async Task StopAsync()
    {
        if (webHost is null)
        {
            return;
        }

        await webHost.StopAsync();
        await webHost.DisposeAsync();
        webHost = null;
    }
}
