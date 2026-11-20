using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Vemima.Core.Domain;

namespace Vemima.Core.Data;

public sealed class VemimaDbContext(DbContextOptions<VemimaDbContext> options) : DbContext(options)
{
    private Payment? paymentBeingSaved;
    public DbSet<Property> Properties => Set<Property>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Lease> Leases => Set<Lease>();
    public DbSet<RentTerms> RentTerms => Set<RentTerms>();
    public DbSet<CostCategory> CostCategories => Set<CostCategory>();
    public DbSet<CostItem> CostItems => Set<CostItem>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceDocument> InvoiceDocuments => Set<InvoiceDocument>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentAllocation> PaymentAllocations => Set<PaymentAllocation>();
    public DbSet<AccountEntry> AccountEntries => Set<AccountEntry>();
    internal DbSet<SchemaVersion> SchemaVersions => Set<SchemaVersion>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<SchemaVersion>().HasKey(x => x.Id);
        model.Entity<Property>().Property(x => x.Name).IsRequired();
        model.Entity<Unit>().HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Lease>().HasOne<Unit>().WithMany().HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Lease>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<RentTerms>().HasOne<Lease>().WithMany().HasForeignKey(x => x.LeaseId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<RentTerms>().HasIndex(x => new { x.LeaseId, x.EffectiveFrom }).IsUnique();
        model.Entity<CostItem>().HasOne<CostCategory>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Document>().HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Invoice>().HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Invoice>().HasOne<CostItem>().WithMany().HasForeignKey(x => x.CostItemId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<InvoiceDocument>().HasKey(x => new { x.InvoiceId, x.DocumentId });
        model.Entity<InvoiceDocument>().HasOne<Invoice>().WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        model.Entity<InvoiceDocument>().HasOne<Document>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Payment>().HasOne<Lease>().WithMany().HasForeignKey(x => x.LeaseId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Payment>().HasMany(x => x.Allocations).WithOne().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Cascade);
        model.Entity<PaymentAllocation>().HasOne<AccountEntry>().WithMany().HasForeignKey(x => x.AccountEntryId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<AccountEntry>().HasOne<Lease>().WithMany().HasForeignKey(x => x.LeaseId).OnDelete(DeleteBehavior.Restrict);

        var cents = new ValueConverter<decimal, long>(value => (long)(value * 100), value => value / 100m);
        foreach (var entity in model.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties().Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
            {
                property.SetValueConverter(cents);
            }
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ValidateChanges();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ValidateChanges();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    internal async Task<int> SavePaymentChangesAsync(Payment payment, CancellationToken cancellationToken)
    {
        paymentBeingSaved = payment;
        try
        {
            return await SaveChangesAsync(cancellationToken);
        }
        finally
        {
            paymentBeingSaved = null;
        }
    }

    private void ValidateChanges()
    {
        ChangeTracker.DetectChanges();
        if (ChangeTracker.Entries().Any(e =>
                e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted &&
                (e.Entity is Payment payment && payment != paymentBeingSaved ||
                 e.Entity is PaymentAllocation allocation && (paymentBeingSaved is null ||
                     !paymentBeingSaved.Allocations.Contains(allocation) &&
                     (paymentBeingSaved.Id == 0 || allocation.PaymentId != paymentBeingSaved.Id)))))
        {
            throw new InvalidOperationException("Zahlungen und Zuordnungen müssen über PaymentStore gespeichert oder gelöscht werden.");
        }
        foreach (var entry in ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            foreach (var property in entry.Properties.Where(p => p.Metadata.ClrType == typeof(decimal) || p.Metadata.ClrType == typeof(decimal?)))
            {
                if (property.CurrentValue is decimal value && (value != decimal.Round(value, 2) || value > long.MaxValue / 100m || value < long.MinValue / 100m))
                {
                    throw new InvalidOperationException($"{entry.Metadata.ClrType.Name}.{property.Metadata.Name} muss auf zwei Dezimalstellen darstellbar sein.");
                }
            }

            switch (entry.Entity)
            {
                case Lease lease when lease.EndDate < lease.StartDate || lease.Deposit < 0:
                    throw new InvalidOperationException("Mietvertrag: Zeitraum oder Kaution ungültig.");
                case RentTerms terms when terms.EffectiveFrom.Day != 1 || terms.Rent < 0 || terms.UtilitiesAdvance < 0:
                    throw new InvalidOperationException("Mietwerte müssen ab einem Monatsersten gelten und dürfen nicht negativ sein.");
                case Payment payment when payment.Amount <= 0:
                    throw new InvalidOperationException("Der Zahlungsbetrag muss positiv sein.");
                case PaymentAllocation allocation when allocation.Amount <= 0 ||
                    (allocation.RentMonth.HasValue == allocation.AccountEntryId.HasValue) ||
                    (allocation.RentMonth.HasValue && allocation.RentMonth.Value.Day != 1):
                    throw new InvalidOperationException("Eine positive Zuordnung muss genau einem Monat oder einer Forderung gelten.");
                case AccountEntry accountEntry when accountEntry.Amount == 0:
                    throw new InvalidOperationException("Eine Forderung oder Gutschrift darf nicht null sein.");
                case AccountEntry accountEntry when entry.State == EntityState.Modified &&
                    (entry.Property(nameof(AccountEntry.LeaseId)).IsModified || accountEntry.Amount < 0) &&
                    PaymentAllocations.Any(x => x.AccountEntryId == accountEntry.Id):
                    throw new InvalidOperationException("Eine zugeordnete Forderung kann nicht einem anderen Mietvertrag zugeordnet oder zur Gutschrift werden.");
                case Unit unit when unit.AreaSquareMetres <= 0:
                    throw new InvalidOperationException("Die Wohnfläche muss positiv sein.");
                case Invoice invoice when invoice.Amount < 0 || invoice.PeriodEnd < invoice.PeriodStart:
                    throw new InvalidOperationException("Rechnung: Betrag oder Zeitraum ungültig.");
                case Invoice invoice when !Enum.IsDefined(invoice.Status):
                    throw new InvalidOperationException("Der Rechnungsstatus ist ungültig.");
                case Invoice invoice when invoice.Status == InvoiceStatus.Confirmed &&
                    (invoice.CostItemId is null || string.IsNullOrWhiteSpace(invoice.Issuer) ||
                     invoice.InvoiceDate is null || invoice.Amount is null || invoice.AccountingYear is null ||
                     invoice.IsRecoverable is null):
                    throw new InvalidOperationException("Eine bestätigte Rechnung benötigt Rechnungssteller, Datum, Betrag, Abrechnungsjahr, Posten und Umlagefähigkeit.");
            }
        }
    }
}

internal sealed class SchemaVersion
{
    public int Id { get; set; }
    public int Version { get; set; }
}
