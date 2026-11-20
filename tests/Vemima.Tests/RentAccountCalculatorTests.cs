using Vemima.Core.Domain;
using Vemima.Core.Rent;

namespace Vemima.Tests;

public class RentAccountCalculatorTests
{
    private static readonly Lease Lease = new() { Id = 1, StartDate = new DateOnly(2026, 1, 1) };
    private static readonly RentTerms[] Terms =
    [
        new() { LeaseId = 1, EffectiveFrom = new DateOnly(2026, 1, 1), Rent = 650m, UtilitiesAdvance = 85m },
        new() { LeaseId = 1, EffectiveFrom = new DateOnly(2027, 1, 1), Rent = 650m, UtilitiesAdvance = 115m }
    ];

    [Fact]
    public void CalculatesMonthlyStatusChangesAndExcludesFutureMonths()
    {
        var payments = new[]
        {
            new Payment
            {
                LeaseId = 1, Date = new DateOnly(2026, 12, 3), Amount = 1100m,
                Allocations = [new() { RentMonth = new DateOnly(2026, 12, 1), Amount = 800m },
                    new() { RentMonth = new DateOnly(2027, 1, 1), Amount = 300m }]
            }
        };
        var result = RentAccountCalculator.Calculate(Lease, Terms, payments, [],
            new DateOnly(2026, 12, 1), new DateOnly(2027, 2, 1), new DateOnly(2026, 12, 15));

        Assert.Equal(735m, result.Months[0].Due);
        Assert.Equal(65m, result.Months[0].Difference);
        Assert.Equal(RentPaymentStatus.Paid, result.Months[0].Status);
        Assert.Equal(765m, result.Months[1].Due);
        Assert.Equal(RentPaymentStatus.Future, result.Months[1].Status);
        Assert.Equal(300m, result.Months[1].Paid);
        Assert.Equal(RentPaymentStatus.Future, result.Months[2].Status);
        Assert.Equal(65m, result.PeriodBalance);
    }

    [Fact]
    public void CalculatesChargesCreditsAndUnallocatedPaymentsAsOfDate()
    {
        var entries = new[]
        {
            new AccountEntry { Id = 1, LeaseId = 1, DueDate = new DateOnly(2026, 3, 1), Amount = 100m },
            new AccountEntry { Id = 2, LeaseId = 1, DueDate = new DateOnly(2026, 3, 1), Amount = -20m },
            new AccountEntry { Id = 3, LeaseId = 1, DueDate = new DateOnly(2026, 4, 1), Amount = 200m }
        };
        var payments = new[]
        {
            new Payment { LeaseId = 1, Date = new DateOnly(2026, 3, 3), Amount = 100m,
                Allocations = [new() { AccountEntryId = 1, Amount = 40m }] },
            new Payment { LeaseId = 1, Date = new DateOnly(2026, 4, 1), Amount = 100m }
        };
        var result = RentAccountCalculator.Calculate(Lease, Terms, payments, entries,
            new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 15));

        Assert.Equal(RentPaymentStatus.Open, result.Months.Single().Status);
        Assert.Equal(-775m, result.PeriodBalance);
        Assert.Equal(60m, result.UnallocatedPayments);
    }

    [Fact]
    public void RejectsPaymentsAllocatedMoreThanOnce()
    {
        var payments = new[] { new Payment { LeaseId = 1, Amount = 100m,
            Date = new DateOnly(2026, 1, 2),
            Allocations = [new() { RentMonth = new DateOnly(2026, 1, 1), Amount = 101m }] } };

        Assert.Throws<InvalidOperationException>(() => RentAccountCalculator.Calculate(Lease, Terms, payments, [],
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31)));
    }

    [Fact]
    public void RejectsMissingTermsAndPartialLeaseMonthsInsteadOfGuessing()
    {
        Assert.Throws<InvalidOperationException>(() => RentAccountCalculator.Calculate(Lease, [], [], [],
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31)));
        var partial = new Lease { Id = 1, StartDate = new DateOnly(2026, 1, 15) };
        Assert.Throws<InvalidOperationException>(() => RentAccountCalculator.Calculate(partial, Terms, [], [],
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31)));
    }

    [Fact]
    public void PeriodBalanceExcludesChargesOutsideSelectedPeriod()
    {
        var entries = new[]
        {
            new AccountEntry { Id = 1, LeaseId = 1, DueDate = new DateOnly(2026, 1, 1), Amount = 300m },
            new AccountEntry { Id = 2, LeaseId = 1, DueDate = new DateOnly(2026, 2, 1), Amount = -20m }
        };
        var result = RentAccountCalculator.Calculate(Lease, Terms, [], entries,
            new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28));
        Assert.Equal(-715m, result.PeriodBalance);
        Assert.Single(result.Entries);
    }
}
