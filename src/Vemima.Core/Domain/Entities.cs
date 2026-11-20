namespace Vemima.Core.Domain;

public sealed class Property
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string Owner { get; set; } = "";
}

public sealed class Unit
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public string Name { get; set; } = "";
    public decimal? AreaSquareMetres { get; set; }
}

public sealed class Tenant
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string ContactDetails { get; set; } = "";
}

public sealed class Lease
{
    public int Id { get; set; }
    public int UnitId { get; set; }
    public int TenantId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public decimal Deposit { get; set; }
}

public sealed class RentTerms
{
    public int Id { get; set; }
    public int LeaseId { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public decimal Rent { get; set; }
    public decimal UtilitiesAdvance { get; set; }
}

public sealed class CostCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public sealed class CostItem
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public string Name { get; set; } = "";
}

public sealed class Document
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public string RelativePath { get; set; } = "";
    public string Category { get; set; } = "";
    public DateOnly? DocumentDate { get; set; }
}

public enum InvoiceStatus
{
    NeedsReview,
    Confirmed
}

public sealed class Invoice
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public int? CostItemId { get; set; }
    public string Issuer { get; set; } = "";
    public string Reference { get; set; } = "";
    public DateOnly? InvoiceDate { get; set; }
    public DateOnly? PeriodStart { get; set; }
    public DateOnly? PeriodEnd { get; set; }
    public int? AccountingYear { get; set; }
    public decimal? Amount { get; set; }
    public string Comment { get; set; } = "";
    public bool? IsRecoverable { get; set; }
    public bool Section35A { get; set; }
    public InvoiceStatus Status { get; set; }
}

public sealed class InvoiceDocument
{
    public int InvoiceId { get; set; }
    public int DocumentId { get; set; }
}

public sealed class Payment
{
    public int Id { get; set; }
    public int LeaseId { get; set; }
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public string Comment { get; set; } = "";
    public List<PaymentAllocation> Allocations { get; set; } = [];
}

public sealed class PaymentAllocation
{
    public int Id { get; set; }
    public int PaymentId { get; set; }
    public DateOnly? RentMonth { get; set; }
    public int? AccountEntryId { get; set; }
    public decimal Amount { get; set; }
}

// A positive amount is a charge; a negative amount is a credit.
public sealed class AccountEntry
{
    public int Id { get; set; }
    public int LeaseId { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = "";
}
