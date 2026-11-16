namespace MiniBank.Entities;

public enum CardStatus
{
    Active = 1,
    Blocked = 2,
    Cancelled = 3
}

public class Card
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    public Guid AccountId { get; set; }
    public Account Account { get; set; } = null!;

    public string CardNumber { get; set; } = string.Empty;
    public string CardHolderName { get; set; } = string.Empty;
    public int ExpiryMonth { get; set; }
    public int ExpiryYear { get; set; }
    public string Cvv { get; set; } = string.Empty;

    public string CardType { get; set; } = "Debit"; // Debit (Banka Kartı)
    public CardStatus Status { get; set; } = CardStatus.Active;

    // Kart Ayarları & Güvenlik
    public bool IsInternationalAllowed { get; set; } = true;
    public bool IsOnlineShoppingAllowed { get; set; } = true;
    public bool IsContactlessAllowed { get; set; } = true;

    // Harcama & Çekim Limitleri
    public decimal DailyLimit { get; set; } = 15000.00m;
    public decimal DailyAtmWithdrawLimit { get; set; } = 5000.00m;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

