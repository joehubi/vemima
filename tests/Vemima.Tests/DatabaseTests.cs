using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Vemima.Core.Data;
using Vemima.Core.Domain;
using Vemima.Core.Rent;

namespace Vemima.Tests;

public class DatabaseTests
{
    [Fact]
    public async Task PersistsInvoicesAndAllocatedPaymentsAcrossReopen()
    {
        var directory = Directory.CreateTempSubdirectory("vemima-db-");
        try
        {
            var path = Path.Combine(directory.FullName, "test.db");
            int leaseId;
            using (var db = VemimaDatabase.Open(path))
            {
                var property = new Property { Name = "Riesenhaus" };
                var tenant = new Tenant { Name = "Test" };
                var category = new CostCategory { Name = "Versicherung" };
                db.AddRange(property, tenant, category);
                await db.SaveChangesAsync();
                var unit = new Unit { PropertyId = property.Id, Name = "Haus", AreaSquareMetres = 125.75m };
                var item = new CostItem { CategoryId = category.Id, Name = "Haftpflicht" };
                db.AddRange(unit, item);
                await db.SaveChangesAsync();
                var lease = new Lease { UnitId = unit.Id, TenantId = tenant.Id, StartDate = new DateOnly(2026, 1, 1) };
                db.Leases.Add(lease);
                await db.SaveChangesAsync();
                leaseId = lease.Id;
                db.RentTerms.Add(new RentTerms { LeaseId = leaseId, EffectiveFrom = lease.StartDate, Rent = 650m, UtilitiesAdvance = 85m });
                var invoice = new Invoice
                {
                    PropertyId = property.Id,
                    CostItemId = item.Id,
                    Issuer = "Versicherung",
                    InvoiceDate = new DateOnly(2026, 1, 1),
                    AccountingYear = 2026,
                    Amount = 596.24m,
                    IsRecoverable = true,
                    Status = InvoiceStatus.Confirmed
                };
                db.Invoices.Add(invoice);
                var entry = new AccountEntry { LeaseId = leaseId, DueDate = new DateOnly(2026, 2, 1), Amount = 100m, Description = "Nachzahlung" };
                db.AccountEntries.Add(entry);
                await db.SaveChangesAsync();
                var firstPage = new Document { PropertyId = property.Id, RelativePath = "1.jpg" };
                var secondPage = new Document { PropertyId = property.Id, RelativePath = "2.jpg" };
                db.Documents.AddRange(firstPage, secondPage);
                db.Invoices.Add(new Invoice { PropertyId = property.Id, Status = InvoiceStatus.NeedsReview });
                await db.SaveChangesAsync();
                db.InvoiceDocuments.AddRange(
                    new InvoiceDocument { InvoiceId = invoice.Id, DocumentId = firstPage.Id },
                    new InvoiceDocument { InvoiceId = invoice.Id, DocumentId = secondPage.Id });
                await db.SaveChangesAsync();
                await new PaymentStore(db).SaveAsync(new Payment
                {
                    LeaseId = leaseId,
                    Date = new DateOnly(2026, 2, 3),
                    Amount = 835m,
                    Allocations = [new() { RentMonth = new DateOnly(2026, 2, 1), Amount = 735m },
                        new() { AccountEntryId = entry.Id, Amount = 100m }]
                });
                entry.Amount = -20m;
                await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
            }

            using (var db = VemimaDatabase.Open(path))
            {
                Assert.Equal(596.24m, (await db.Invoices.SingleAsync(x => x.Status == InvoiceStatus.Confirmed)).Amount);
                Assert.Null((await db.Invoices.SingleAsync(x => x.Status == InvoiceStatus.NeedsReview)).Amount);
                Assert.Equal(125.75m, (await db.Units.SingleAsync()).AreaSquareMetres);
                Assert.Equal(2, await db.InvoiceDocuments.CountAsync());
                var result = RentAccountCalculator.Calculate(await db.Leases.SingleAsync(x => x.Id == leaseId),
                    await db.RentTerms.ToListAsync(), await db.Payments.Include(x => x.Allocations).ToListAsync(),
                    await db.AccountEntries.ToListAsync(), new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28));
                Assert.Equal(0m, result.PeriodBalance);
                Assert.Equal(RentPaymentStatus.Paid, result.Months.Single().Status);
            }
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task RejectsInvalidConfirmationAndMoneyPrecision()
    {
        var directory = Directory.CreateTempSubdirectory("vemima-db-");
        try
        {
            using var db = VemimaDatabase.Open(Path.Combine(directory.FullName, "test.db"));
            db.Invoices.Add(new Invoice { Status = InvoiceStatus.Confirmed, Amount = 1m });
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
            db.ChangeTracker.Clear();
            db.Invoices.Add(new Invoice { Amount = 1.001m });
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task PaymentStoreRejectsOverAllocationAndForeignClaims()
    {
        var directory = Directory.CreateTempSubdirectory("vemima-db-");
        try
        {
            using var db = VemimaDatabase.Open(Path.Combine(directory.FullName, "test.db"));
            var property = new Property { Name = "Test" };
            var tenant = new Tenant { Name = "Test" };
            db.AddRange(property, tenant);
            await db.SaveChangesAsync();
            var unit = new Unit { PropertyId = property.Id, Name = "Test" };
            db.Units.Add(unit);
            await db.SaveChangesAsync();
            var lease = new Lease { UnitId = unit.Id, TenantId = tenant.Id, StartDate = new DateOnly(2026, 1, 1) };
            db.Leases.Add(lease);
            await db.SaveChangesAsync();
            var store = new PaymentStore(db);
            var payment = new Payment
            {
                LeaseId = lease.Id,
                Date = lease.StartDate,
                Amount = 100m,
                Allocations = [new() { RentMonth = lease.StartDate, Amount = 101m }]
            };
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync(payment));
            payment.Allocations = [new() { AccountEntryId = 999, Amount = 100m }];
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync(payment));
            Assert.Empty(await db.Payments.ToListAsync());
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task TrackedPaymentCanBeEditedAndDeletedWithoutLosingAllocations()
    {
        var directory = Directory.CreateTempSubdirectory("vemima-db-");
        try
        {
            var path = Path.Combine(directory.FullName, "test.db");
            int paymentId;
            using (var db = VemimaDatabase.Open(path))
            {
                var property = new Property { Name = "Test" };
                var tenant = new Tenant { Name = "Test" };
                db.AddRange(property, tenant);
                await db.SaveChangesAsync();
                var unit = new Unit { PropertyId = property.Id, Name = "Test" };
                db.Units.Add(unit);
                await db.SaveChangesAsync();
                var lease = new Lease { UnitId = unit.Id, TenantId = tenant.Id, StartDate = new DateOnly(2026, 1, 1) };
                db.Leases.Add(lease);
                await db.SaveChangesAsync();
                var store = new PaymentStore(db);
                var payment = await store.SaveAsync(new Payment
                {
                    LeaseId = lease.Id,
                    Date = lease.StartDate,
                    Amount = 100m,
                    Allocations = [new() { RentMonth = lease.StartDate, Amount = 100m }]
                });
                paymentId = payment.Id;
                payment.Amount = 80m;
                payment.Allocations.Single().Amount = 80m;
                await store.SaveAsync(payment);
                payment.Allocations.Clear();
                payment.Allocations.Add(new PaymentAllocation { RentMonth = new DateOnly(2026, 2, 1), Amount = 75m });
                await store.SaveAsync(payment);
            }
            using (var db = VemimaDatabase.Open(path))
            {
                var payment = await db.Payments.Include(x => x.Allocations).SingleAsync();
                Assert.Equal(80m, payment.Amount);
                Assert.Equal(75m, payment.Allocations.Single().Amount);
                Assert.Equal(new DateOnly(2026, 2, 1), payment.Allocations.Single().RentMonth);
                await new PaymentStore(db).DeleteAsync(paymentId);
            }
            using (var db = VemimaDatabase.Open(path))
            {
                Assert.Empty(await db.Payments.ToListAsync());
                Assert.Empty(await db.PaymentAllocations.ToListAsync());
            }
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task DirectPaymentWritesAreRejected()
    {
        var directory = Directory.CreateTempSubdirectory("vemima-db-");
        try
        {
            using var db = VemimaDatabase.Open(Path.Combine(directory.FullName, "test.db"));
            db.Payments.Add(new Payment { Amount = 100m });
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void RejectsUnknownAndIncompleteSchemasWithoutChangingThem()
    {
        var directory = Directory.CreateTempSubdirectory("vemima-db-");
        try
        {
            var path = Path.Combine(directory.FullName, "test.db");
            using (var db = VemimaDatabase.Open(path)) { }
            using (var connection = new SqliteConnection($"Data Source={path};Pooling=False"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "UPDATE SchemaVersions SET Version = 99";
                command.ExecuteNonQuery();
            }
            Assert.Throws<InvalidOperationException>(() => VemimaDatabase.Open(path));
            using (var connection = new SqliteConnection($"Data Source={path};Pooling=False"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT Version FROM SchemaVersions";
                Assert.Equal(99L, command.ExecuteScalar());
                command.CommandText = "DROP TABLE SchemaVersions";
                command.ExecuteNonQuery();
            }
            Assert.Throws<InvalidOperationException>(() => VemimaDatabase.Open(path));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
