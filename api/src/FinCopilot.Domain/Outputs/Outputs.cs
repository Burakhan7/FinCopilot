namespace FinCopilot.Domain.Outputs;

public class Alert
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required string Type { get; set; }
    public required string Severity { get; set; }
    public required string Title { get; set; }
    public required string Payload { get; set; }
    /// <summary>Aynı uyarının iki kez gitmemesi için (user_id, dedupe_key) tekil.</summary>
    public required string DedupeKey { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
}

public class SavingsEvent
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    /// <summary>kmh / late_fee / interest / subscription / card_float / fee</summary>
    public required string Type { get; set; }
    public decimal AmountSaved { get; set; }
    public Guid? AlertId { get; set; }
    public string? Evidence { get; set; }
    public DateTimeOffset DetectedAt { get; set; }
}

public class Goal
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required string Name { get; set; }
    public decimal TargetAmount { get; set; }
    public DateOnly TargetDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
