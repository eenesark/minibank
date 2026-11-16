namespace MiniBank.Entities;

public enum TransactionType
{
    Deposit = 1,       // Para Yatırma
    Withdrawal = 2,    // Para Çekme
    TransferIn = 3,    // Gelen Havale/EFT
    TransferOut = 4,   // Giden Havale/EFT
    FxBuy = 5,         // Döviz / Altın Alış
    FxSell = 6,        // Döviz / Altın Satış
    InterestPayment = 7,  // Vadeli Faiz Geliri
    TimeDepositOpen = 8,  // Vadeli Hesap Açılış Transferi
    TimeDepositClose = 9  // Vadeli Hesap Kapanış / Vade Sonu Dönüşü
}

public class AccountTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public decimal Amount { get; set; }
    public TransactionType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = "Diğer";
    public string? TargetIban { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Foreign Key
    public Guid AccountId { get; set; }
    public Account Account { get; set; } = null!;
}

