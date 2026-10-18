namespace FinCopilot.Domain.Enrichment;

public class Category
{
    public int Id { get; set; }
    public required string Slug { get; set; }
    public required string NameTr { get; set; }
    public int? ParentId { get; set; }
    public string? Icon { get; set; }
    public bool IsIncome { get; set; }
    public int SortOrder { get; set; }
}

public class Merchant
{
    public Guid Id { get; set; }
    public required string CanonicalName { get; set; }
    public required string DisplayName { get; set; }
    public string? LogoUrl { get; set; }
    public int? DefaultCategoryId { get; set; }

    public List<MerchantAlias> Aliases { get; set; } = [];
}

public class MerchantAlias
{
    public Guid Id { get; set; }
    public Guid MerchantId { get; set; }
    public required string Pattern { get; set; }
    /// <summary>exact / prefix / regex</summary>
    public required string PatternType { get; set; }
    /// <summary>seed / ml / llm / user</summary>
    public required string Source { get; set; }
}

public class RecurringItem
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    /// <summary>subscription / rent / dues / salary / bill / loan / other</summary>
    public required string Type { get; set; }
    public required string Name { get; set; }
    public string? CounterpartyKey { get; set; }
    public Guid? MerchantId { get; set; }
    public decimal ExpectedAmount { get; set; }
    public decimal AmountTolerance { get; set; }
    public int PeriodDays { get; set; }
    public int? DayOfMonth { get; set; }
    public DateTimeOffset? LastSeenAt { get; set; }
    public DateOnly? NextExpectedDate { get; set; }
    /// <summary>suggested / confirmed / rejected / manual</summary>
    public required string Status { get; set; }
    /// <summary>islAmc / pattern / manual</summary>
    public required string Source { get; set; }
}
