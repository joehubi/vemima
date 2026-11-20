using Vemima.Core.Domain;

namespace Vemima.Core.Rent;

public enum RentPaymentStatus
{
    Open,
    Partial,
    Paid,
    Future
}

public sealed record RentMonth(
    DateOnly Month,
    decimal Rent,
    decimal UtilitiesAdvance,
    decimal Paid,
    RentPaymentStatus Status)
{
    public decimal Due => Rent + UtilitiesAdvance;
    public decimal Difference => Paid - Due;
}

public sealed record AccountEntryBalance(AccountEntry Entry, decimal Paid)
{
    public decimal Difference => Paid - Entry.Amount;
}

public sealed record RentAccount(
    IReadOnlyList<RentMonth> Months,
    IReadOnlyList<AccountEntryBalance> Entries,
    decimal PeriodBalance,
    decimal UnallocatedPayments);

public static class RentAccountCalculator
{
    public static RentAccount Calculate(
        Lease lease,
        IEnumerable<RentTerms> terms,
        IEnumerable<Payment> payments,
        IEnumerable<AccountEntry> accountEntries,
        DateOnly fromMonth,
        DateOnly throughMonth,
        DateOnly asOf)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(terms);
        ArgumentNullException.ThrowIfNull(payments);
        ArgumentNullException.ThrowIfNull(accountEntries);
        if (fromMonth.Day != 1 || throughMonth.Day != 1 || fromMonth > throughMonth)
        {
            throw new ArgumentException("Der Abfragezeitraum muss aus gültigen Monatsanfängen bestehen.");
        }

        var termList = terms.Where(x => x.LeaseId == lease.Id).OrderBy(x => x.EffectiveFrom).ToList();
        if (termList.Any(x => x.EffectiveFrom.Day != 1 || x.Rent < 0 || x.UtilitiesAdvance < 0) ||
            termList.GroupBy(x => x.EffectiveFrom).Any(group => group.Count() > 1))
        {
            throw new InvalidOperationException("Der Mietverlauf enthält ungültige oder doppelte Einträge.");
        }

        var entryList = accountEntries.Where(x => x.LeaseId == lease.Id).ToList();
        if (entryList.Any(x => x.Amount == 0) || entryList.Select(x => x.Id).Distinct().Count() != entryList.Count)
        {
            throw new InvalidOperationException("Die Forderungen enthalten ungültige oder doppelte Einträge.");
        }
        var entriesById = entryList.ToDictionary(x => x.Id);
        var paymentList = payments.Where(x => x.LeaseId == lease.Id && x.Date <= asOf).ToList();
        foreach (var payment in paymentList)
        {
            if (payment.Amount <= 0 || payment.Allocations.Any(a => a.Amount <= 0 ||
                (a.RentMonth.HasValue == a.AccountEntryId.HasValue) ||
                (a.RentMonth.HasValue && a.RentMonth.Value.Day != 1) ||
                (a.AccountEntryId.HasValue && (!entriesById.TryGetValue(a.AccountEntryId.Value, out var target) || target.Amount <= 0))) ||
                payment.Allocations.Sum(a => a.Amount) > payment.Amount)
            {
                throw new InvalidOperationException("Zahlung oder Zahlungszuordnung ist ungültig.");
            }
        }

        var allocations = paymentList.SelectMany(x => x.Allocations).ToList();
        var months = new List<RentMonth>();
        var currentMonth = new DateOnly(asOf.Year, asOf.Month, 1);
        for (var month = fromMonth; ; month = month.AddMonths(1))
        {
            var lastDay = new DateOnly(month.Year, month.Month, DateTime.DaysInMonth(month.Year, month.Month));
            if (lease.StartDate > lastDay || lease.EndDate < month)
            {
                if (month == throughMonth) break;
                continue;
            }
            if (lease.StartDate > month || lease.EndDate < lastDay)
            {
                throw new InvalidOperationException("Für einen angebrochenen Mietmonat ist die Berechnung noch nicht festgelegt.");
            }

            var applicable = termList.LastOrDefault(x => x.EffectiveFrom <= month)
                ?? throw new InvalidOperationException($"Für {month:MM.yyyy} fehlen Mietwerte.");
            var paid = allocations.Where(x => x.RentMonth == month).Sum(x => x.Amount);
            var due = applicable.Rent + applicable.UtilitiesAdvance;
            var status = month > currentMonth ? RentPaymentStatus.Future
                : paid >= due ? RentPaymentStatus.Paid
                : paid > 0 ? RentPaymentStatus.Partial
                : RentPaymentStatus.Open;
            months.Add(new RentMonth(month, applicable.Rent, applicable.UtilitiesAdvance, paid, status));
            if (month == throughMonth) break;
        }

        var lastDayOfPeriod = new DateOnly(throughMonth.Year, throughMonth.Month, DateTime.DaysInMonth(throughMonth.Year, throughMonth.Month));
        var balances = entryList
            .Where(x => x.DueDate >= fromMonth && x.DueDate <= lastDayOfPeriod)
            .OrderBy(x => x.DueDate)
            .Select(x => new AccountEntryBalance(x, allocations.Where(a => a.AccountEntryId == x.Id).Sum(a => a.Amount)))
            .ToList();
        var balance = months.Where(x => x.Month <= currentMonth).Sum(x => x.Difference)
            + balances.Where(x => x.Entry.DueDate <= asOf).Sum(x => x.Difference);
        var unallocated = paymentList.Sum(x => x.Amount - x.Allocations.Sum(a => a.Amount));
        return new RentAccount(months, balances, balance, unallocated);
    }
}
