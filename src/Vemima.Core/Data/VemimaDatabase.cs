using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Vemima.Core.Data;

public static class VemimaDatabase
{
    private const int CurrentVersion = 1;

    public static VemimaDbContext Open(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(path),
            ForeignKeys = true,
            Pooling = false
        }.ToString();
        var options = new DbContextOptionsBuilder<VemimaDbContext>().UseSqlite(connectionString).Options;
        var context = new VemimaDbContext(options);
        try
        {
            context.Database.OpenConnection();
            using var transaction = context.Database.BeginTransaction();
            using var command = context.Database.GetDbConnection().CreateCommand();
            command.Transaction = transaction.GetDbTransaction();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'";
            if (Convert.ToInt64(command.ExecuteScalar()) == 0)
            {
                context.Database.ExecuteSqlRaw(context.Database.GenerateCreateScript());
                context.SchemaVersions.Add(new SchemaVersion { Id = 1, Version = CurrentVersion });
                context.SaveChanges();
            }

            int? version;
            try
            {
                version = context.SchemaVersions.Where(x => x.Id == 1).Select(x => (int?)x.Version).SingleOrDefault();
            }
            catch (SqliteException ex)
            {
                throw new InvalidOperationException("Die Datenbank besitzt kein gültiges Vemima-Schema.", ex);
            }

            if (version != CurrentVersion)
            {
                throw new InvalidOperationException($"Nicht unterstützte Datenbankversion: {version?.ToString() ?? "unbekannt"}.");
            }

            transaction.Commit();
            return context;
        }
        catch
        {
            context.Dispose();
            throw;
        }
    }
}
