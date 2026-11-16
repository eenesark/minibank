namespace MiniBank.Entities;

public class Customer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CustomerNumber { get; set; } = string.Empty; // Örn: MB123456789
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string IdentityNumber { get; set; } = string.Empty; // TC Kimlik No
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Kimlik Doğrulama & Yetkilendirme
    public byte[]? PasswordHash { get; set; }
    public byte[]? PasswordSalt { get; set; }
    public string Role { get; set; } = "Customer"; // "Admin" veya "Customer"

    // Navigation property
    public ICollection<Account> Accounts { get; set; } = new List<Account>();
    public ICollection<CustomerFeature> Features { get; set; } = new List<CustomerFeature>();
    public ICollection<Card> Cards { get; set; } = new List<Card>();
}

