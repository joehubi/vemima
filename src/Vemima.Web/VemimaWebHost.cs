using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Vemima.Core.Configuration;

namespace Vemima.Web;

public static class VemimaWebHost
{
    /// <summary>Baut den Webserver (nur IPv4, nur freigegebene Geräte). Gestartet wird er von der App.</summary>
    public static WebApplication Build(VemimaConfig config)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://0.0.0.0:{config.Server.Port}");

        var app = builder.Build();
        var policy = new ClientIpPolicy(config.Server.AllowedClientIps);

        app.Use(async (context, next) =>
        {
            if (!policy.IsAllowed(context.Connection.RemoteIpAddress))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            await next();
        });

        app.MapGet("/", () => Results.Text(ReadEmbedded("index.html"), "text/html; charset=utf-8"));
        app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

        return app;
    }

    private static string ReadEmbedded(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Eingebettete Ressource '{name}' fehlt.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
