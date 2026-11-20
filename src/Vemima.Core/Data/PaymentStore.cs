using Microsoft.EntityFrameworkCore;
using Vemima.Core.Domain;

namespace Vemima.Core.Data;

public sealed class PaymentStore(VemimaDbContext context)
{
    public async Task<Payment> SaveAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payment);
        var allocations = payment.Allocations.Select(a => new PaymentAllocation
        {
            RentMonth = a.RentMonth,
            AccountEntryId = a.AccountEntryId,
            Amount = a.Amount
        }).ToList();
        if (payment.Amount <= 0 || payment.Allocations.Any(a => a.Amount <= 0 ||
            (a.RentMonth.HasValue == a.AccountEntryId.HasValue) ||
            (a.RentMonth.HasValue && a.RentMonth.Value.Day != 1)) ||
            payment.Allocations.Sum(a => a.Amount) > payment.Amount)
        {
            throw new InvalidOperationException("Zahlung oder Zahlungszuordnung ist ungültig.");
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var lease = await context.Leases.SingleAsync(x => x.Id == payment.LeaseId, cancellationToken);
        foreach (var allocation in payment.Allocations)
        {
            if (allocation.RentMonth is { } month &&
                (new DateOnly(month.Year, month.Month, DateTime.DaysInMonth(month.Year, month.Month)) < lease.StartDate || month > lease.EndDate))
            {
                throw new InvalidOperationException("Die Zahlungszuordnung liegt außerhalb des Mietvertrags.");
            }
            if (allocation.AccountEntryId is { } id &&
                !await context.AccountEntries.AnyAsync(x => x.Id == id && x.LeaseId == payment.LeaseId && x.Amount > 0, cancellationToken))
            {
                throw new InvalidOperationException("Die Forderung gehört nicht zum Mietvertrag oder ist keine Nachzahlung.");
            }
        }

        Payment stored;
        if (payment.Id == 0)
        {
            stored = payment;
            context.Payments.Add(stored);
        }
        else
        {
            stored = await context.Payments.Include(x => x.Allocations)
                .SingleAsync(x => x.Id == payment.Id, cancellationToken);
            if (stored.LeaseId != payment.LeaseId)
            {
                throw new InvalidOperationException("Der Mietvertrag einer Zahlung kann nicht geändert werden.");
            }
            stored.Date = payment.Date;
            stored.Amount = payment.Amount;
            stored.Comment = payment.Comment;
            context.ChangeTracker.DetectChanges();
            foreach (var existing in stored.Allocations.ToList())
            {
                var entry = context.Entry(existing);
                if (entry.State == EntityState.Added)
                {
                    entry.State = EntityState.Detached;
                }
                else
                {
                    context.PaymentAllocations.Remove(existing);
                }
            }
            stored.Allocations.Clear();
            foreach (var allocation in allocations)
            {
                stored.Allocations.Add(allocation);
            }
        }

        await context.SavePaymentChangesAsync(stored, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return stored;
    }

    public async Task DeleteAsync(int paymentId, CancellationToken cancellationToken = default)
    {
        var payment = await context.Payments.SingleAsync(x => x.Id == paymentId, cancellationToken);
        context.Payments.Remove(payment);
        await context.SavePaymentChangesAsync(payment, cancellationToken);
    }
}
