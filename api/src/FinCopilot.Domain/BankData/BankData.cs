namespace FinCopilot.Domain.BankData;

public class Account
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ConsentId { get; set; }
    public required string HhsKod { get; set; }
    public required string HspRef { get; set; }
    public byte[]? HspNoEnc { get; set; }
    public string? HspNoMasked { get; set; }
    public string? HspShb { get; set; }
    public string? SubeAdi { get; set; }
    public string? KisaAd { get; set; }
    public string? UrunAdi { get; set; }
    public required string ParaBirimi { get; set; }
    public string? HspTur { get; set; }
    public string? HspTip { get; set; }
    public string? HspDrm { get; set; }
    public DateTimeOffset? AcilisTrh { get; set; }
    public bool IsActive { get; set; } = true;
}

public class BalanceSnapshot
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public decimal BkyTtr { get; set; }
    public decimal? BlkTtr { get; set; }
    public required string ParaBirimi { get; set; }
    public decimal? KulKrdTtr { get; set; }
    public string? KrdDhlGstr { get; set; }
    public DateTimeOffset BkyZmn { get; set; }
    /// <summary>sync / event</summary>
    public required string Source { get; set; }
}

public class Transaction
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public required string IslNo { get; set; }
    public string? RefNo { get; set; }
    public decimal Amount { get; set; }
    public required string Currency { get; set; }
    /// <summary>B: borç, A: alacak, N: nötr.</summary>
    public required string Direction { get; set; }
    public string? IslTur { get; set; }
    public string? IslAmc { get; set; }
    public string? Kanal { get; set; }
    public string? DescriptionRaw { get; set; }
    public string? OhkMsj { get; set; }
    public string? CounterpartyName { get; set; }
    public string? CounterpartyIbanMasked { get; set; }
    public DateTimeOffset OccurredAt { get; set; }

    public Guid? MerchantId { get; set; }
    public int? CategoryId { get; set; }
    public string? CategorySource { get; set; }
    public decimal? CategoryConfidence { get; set; }
    public Guid? RecurringItemId { get; set; }
}

public class Card
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ConsentId { get; set; }
    public required string HhsKod { get; set; }
    public required string KartRef { get; set; }
    public string? SonNo { get; set; }
    public required string KartTipi { get; set; }
    public string? AltKartTipi { get; set; }
    public string? KartFormu { get; set; }
    public string? KartTuru { get; set; }
    public string? KartStatu { get; set; }
    public string? KartSema { get; set; }
    public string? UrunAdi { get; set; }
    public bool IsActive { get; set; } = true;
}

public class CardStatement
{
    public Guid Id { get; set; }
    public Guid CardId { get; set; }
    public DateOnly StatementDate { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal PeriodDebt { get; set; }
    public decimal MinPayment { get; set; }
    public decimal? CreditLimit { get; set; }
    public decimal? UsedLimit { get; set; }
    public decimal? CashLimit { get; set; }
    public decimal? UsedCashLimit { get; set; }
    public required string Currency { get; set; }
    public DateTimeOffset FetchedAt { get; set; }
}

public class CardTransaction
{
    public Guid Id { get; set; }
    public Guid CardId { get; set; }
    public required string IslemNo { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public decimal Amount { get; set; }
    public required string Currency { get; set; }
    public int InstallmentCount { get; set; } = 1;
    public string? IslTuru { get; set; }
    public string? MerchantRaw { get; set; }
    public string? DescriptionRaw { get; set; }
    public string? FxInfo { get; set; }

    public Guid? MerchantId { get; set; }
    public int? CategoryId { get; set; }
    public string? CategorySource { get; set; }
    public decimal? CategoryConfidence { get; set; }
    public Guid? RecurringItemId { get; set; }
}

public class Installment
{
    public Guid Id { get; set; }
    public Guid CardTransactionId { get; set; }
    public Guid CardId { get; set; }
    public int InstallmentNo { get; set; }
    public int TotalInstallments { get; set; }
    public decimal Amount { get; set; }
    /// <summary>Taksitin düştüğü ay (ayın 1'i).</summary>
    public DateOnly DueMonth { get; set; }
}
