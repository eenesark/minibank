namespace MiniBank.Entities;

public class Account
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string AccountNumber { get; set; } = string.Empty;
    public string Iban { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public string Currency { get; set; } = "TRY"; // TRY, USD, EUR
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Hesap Türü: "Demand" (Vadesiz) veya "TimeDeposit" (Vadeli)
    public string AccountType { get; set; } = "Demand";

    // Vadeli Mevduat Bilgileri
    public int? MaturityDays { get; set; }
    public decimal? InterestRate { get; set; } // Yıllık faiz oranı (Örn: 32.0, 38.0, 41.0)
    public DateTime? MaturityDate { get; set; } // Vade Bitiş Tarihi
    public decimal? InitialDepositAmount { get; set; } // Vadeye bağlanan anapara
    public decimal? ExpectedInterestAmount { get; set; } // Vade sonu beklenen net faiz
    public bool IsMatured { get; set; } = false; // Vade günü doldu mu?
    public bool IsClosed { get; set; } = false; // Vade sonu/bozma ile kapatıldı mı?

    // Foreign Key
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    // Navigation property
    public ICollection<AccountTransaction> Transactions { get; set; } = new List<AccountTransaction>();
    public ICollection<Card> Cards { get; set; } = new List<Card>();
}

