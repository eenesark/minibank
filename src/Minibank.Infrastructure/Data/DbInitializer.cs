using Microsoft.EntityFrameworkCore;
using MiniBank.Entities;
using MiniBank.Services.Security;

namespace MiniBank.Data;

public class DbInitializer : IDbInitializer
{
    private readonly MiniBankDbContext _context;
    private readonly IPasswordHasher _passwordHasher;

    public DbInitializer(MiniBankDbContext context, IPasswordHasher passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    public async Task SeedAsync()
    {
        // 0. Varsa Customers tablosundaki eski admin kaydını temizle
        var oldAdmins = await _context.Customers
            .Where(c => c.Role == "Admin" || c.IdentityNumber == "admin")
            .ToListAsync();

        if (oldAdmins.Any())
        {
            var oldAdminIds = oldAdmins.Select(a => a.Id).ToList();
            var adminAccounts = await _context.Accounts
                .Where(a => oldAdminIds.Contains(a.CustomerId))
                .ToListAsync();

            if (adminAccounts.Any())
            {
                _context.Accounts.RemoveRange(adminAccounts);
            }
            _context.Customers.RemoveRange(oldAdmins);
            await _context.SaveChangesAsync();
        }

        // 1. Bağımsız Admins tablosu: Admin kullanıcısı yoksa oluştur
        var adminExists = await _context.Admins.AnyAsync();
        if (!adminExists)
        {
            _passwordHasher.CreatePasswordHash("Admin123*", out var adminHash, out var adminSalt);

            var adminUser = new AdminUser
            {
                Id = Guid.NewGuid(),
                Username = "admin",
                FullName = "Sistem Yöneticisi",
                Email = "admin@minibank.com",
                PhoneNumber = "+90 555 000 00 00",
                PasswordHash = adminHash,
                PasswordSalt = adminSalt
            };

            await _context.Admins.AddAsync(adminUser);
            await _context.SaveChangesAsync();
        }

        // 2. Başlangıç Müşterileri (Ahmet ve Ayşe)
        var hasCustomers = await _context.Customers.AnyAsync(c => c.Role == "Customer");
        if (!hasCustomers)
        {
            _passwordHasher.CreatePasswordHash("123456", out var passHash, out var passSalt);

            var customer1 = new Customer
            {
                Id = Guid.NewGuid(),
                CustomerNumber = "MB100000001",
                FirstName = "Ahmet",
                LastName = "Yılmaz",
                IdentityNumber = "12345678901",
                Email = "ahmet.yilmaz@minibank.com",
                PhoneNumber = "+90 555 111 22 33",
                Role = "Customer",
                PasswordHash = passHash,
                PasswordSalt = passSalt
            };

            var customer2 = new Customer
            {
                Id = Guid.NewGuid(),
                CustomerNumber = "MB100000002",
                FirstName = "Ayşe",
                LastName = "Demir",
                IdentityNumber = "98765432109",
                Email = "ayse.demir@minibank.com",
                PhoneNumber = "+90 555 444 55 66",
                Role = "Customer",
                PasswordHash = passHash,
                PasswordSalt = passSalt
            };

            await _context.Customers.AddRangeAsync(customer1, customer2);

            var account1 = new Account
            {
                Id = Guid.NewGuid(),
                CustomerId = customer1.Id,
                AccountNumber = "1000000001",
                Iban = "TR110006201000000001",
                Balance = 25000.00m,
                Currency = "TRY",
                IsActive = true
            };

            account1.Transactions.Add(new AccountTransaction
            {
                Amount = 25000.00m,
                Type = TransactionType.Deposit,
                Description = "Açılış Başlangıç Bakiyesi"
            });

            var account2 = new Account
            {
                Id = Guid.NewGuid(),
                CustomerId = customer2.Id,
                AccountNumber = "1000000002",
                Iban = "TR220006201000000002",
                Balance = 10000.00m,
                Currency = "TRY",
                IsActive = true
            };

            account2.Transactions.Add(new AccountTransaction
            {
                Amount = 10000.00m,
                Type = TransactionType.Deposit,
                Description = "Açılış Başlangıç Bakiyesi"
            });

            await _context.Accounts.AddRangeAsync(account1, account2);
            await _context.SaveChangesAsync();
        }

        // 3. Şifresi henüz atanmamış müşterilere varsayılan şifre tanımla (123456)
        var customersWithoutPassword = await _context.Customers
            .Where(c => c.PasswordHash == null || c.PasswordSalt == null)
            .ToListAsync();

        if (customersWithoutPassword.Any())
        {
            _passwordHasher.CreatePasswordHash("123456", out var defHash, out var defSalt);
            foreach (var cust in customersWithoutPassword)
            {
                cust.PasswordHash = defHash;
                cust.PasswordSalt = defSalt;
                if (string.IsNullOrWhiteSpace(cust.Role)) cust.Role = "Customer";
            }
            await _context.SaveChangesAsync();
        }

        // 4. Hesabı olmayan müşteriler için otomatik hesap oluştur
        var customersWithoutAccount = await _context.Customers
            .Include(c => c.Accounts)
            .Where(c => !c.Accounts.Any())
            .ToListAsync();

        if (customersWithoutAccount.Any())
        {
            var random = new Random();
            foreach (var customer in customersWithoutAccount)
            {
                var accountNumber = random.NextInt64(1000000000, 9999999999).ToString();
                var iban = $"TR{random.Next(10, 99)}000620{accountNumber}";

                var account = new Account
                {
                    CustomerId = customer.Id,
                    AccountNumber = accountNumber,
                    Iban = iban,
                    Currency = "TRY",
                    Balance = 0,
                    IsActive = true
                };

                await _context.Accounts.AddAsync(account);
            }
            await _context.SaveChangesAsync();
        }

        // 4.1. Varsa TL dışındaki (USD, EUR, XAU vb.) veya Vadesiz (Demand) olmayan hesaplara bağlanmış kartları temizle
        var invalidCards = await _context.Cards
            .Include(c => c.Account)
            .Where(c => (c.Account.Currency != "TRY" && c.Account.Currency != "TL") || c.Account.AccountType != "Demand")
            .ToListAsync();

        if (invalidCards.Any())
        {
            _context.Cards.RemoveRange(invalidCards);
            await _context.SaveChangesAsync();
        }

        // 5. Kartı olmayan yalnızca aktif Vadesiz (Demand) TL/TRY hesapları için otomatik Banka Kartı (Debit Card) oluştur
        var accountsWithoutCards = await _context.Accounts
            .Include(a => a.Customer)
            .Include(a => a.Cards)
            .Where(a => a.IsActive && a.AccountType == "Demand" && (a.Currency == "TRY" || a.Currency == "TL") && !a.Cards.Any())
            .ToListAsync();

        if (accountsWithoutCards.Any())
        {
            var rand = new Random();
            foreach (var acc in accountsWithoutCards)
            {
                var cust = acc.Customer;
                if (cust is null) continue;

                var cardHolder = $"{cust.FirstName} {cust.LastName}".ToUpperInvariant();
                var cardNumber = $"454362{rand.Next(10, 99)}{rand.Next(1000, 9999)}{rand.Next(1000, 9999)}";
                var cvv = rand.Next(100, 999).ToString();

                var card = new Card
                {
                    Id = Guid.NewGuid(),
                    CustomerId = cust.Id,
                    AccountId = acc.Id,
                    CardNumber = cardNumber,
                    CardHolderName = cardHolder,
                    ExpiryMonth = DateTime.UtcNow.Month,
                    ExpiryYear = (DateTime.UtcNow.Year % 100) + 4,
                    Cvv = cvv,
                    CardType = "Debit",
                    Status = CardStatus.Active,
                    IsInternationalAllowed = true,
                    IsOnlineShoppingAllowed = true,
                    IsContactlessAllowed = true,
                    DailyLimit = 15000.00m,
                    DailyAtmWithdrawLimit = 5000.00m,
                    CreatedAt = DateTime.UtcNow
                };

                await _context.Cards.AddAsync(card);
            }
            await _context.SaveChangesAsync();
        }

        // 6. Yusuf Enes Arık kullanıcısının vadeli mevduat hesabını garantiye al
        var yusuf = await _context.Customers
            .Include(c => c.Accounts)
            .FirstOrDefaultAsync(c => c.FirstName.ToLower().Contains("yusuf") || c.LastName.ToLower().Contains("arık") || c.LastName.ToLower().Contains("arik"));

        if (yusuf != null)
        {
            var hasActiveTerm = yusuf.Accounts.Any(a => a.AccountType == "TimeDeposit" && !a.IsClosed);
            if (!hasActiveTerm)
            {
                var rand = new Random();
                var termAccNum = rand.NextInt64(1000000000, 9999999999).ToString();
                var termIban = $"TR{rand.Next(10, 99)}000620{termAccNum}";

                var termAccount = new Account
                {
                    Id = Guid.NewGuid(),
                    CustomerId = yusuf.Id,
                    AccountNumber = termAccNum,
                    Iban = termIban,
                    Currency = "TRY",
                    Balance = 50000.00m,
                    InitialDepositAmount = 50000.00m,
                    AccountType = "TimeDeposit",
                    MaturityDays = 32,
                    InterestRate = 41.0m,
                    MaturityDate = DateTime.UtcNow.AddDays(28),
                    ExpectedInterestAmount = 1664.38m,
                    IsActive = true,
                    IsMatured = false,
                    IsClosed = false,
                    CreatedAt = DateTime.UtcNow.AddDays(-4)
                };

                await _context.Accounts.AddAsync(termAccount);
                await _context.SaveChangesAsync();
            }
        }

        // 7. Harcama Analizi için gerçekçi örnek hareketler (Migros, Starbucks, Shell, Netflix vb.)
        var primaryDemandAccount = await _context.Accounts
            .Include(a => a.Transactions)
            .FirstOrDefaultAsync(a => a.IsActive && a.Currency == "TRY" && a.AccountType == "Demand");

        if (primaryDemandAccount != null && primaryDemandAccount.Transactions.Count < 5)
        {
            var now = DateTime.UtcNow;
            var sampleTransactions = new List<AccountTransaction>
            {
                new()
                {
                    AccountId = primaryDemandAccount.Id,
                    Amount = 1450.50m,
                    Type = TransactionType.Withdrawal,
                    Description = "Migros Ticaret A.Ş. Market Alışverişi",
                    Category = "Market & Gıda",
                    CreatedAt = now.AddDays(-1)
                },
                new()
                {
                    AccountId = primaryDemandAccount.Id,
                    Amount = 285.00m,
                    Type = TransactionType.Withdrawal,
                    Description = "Starbucks Coffee Caddebostan",
                    Category = "Restoran & Kafe",
                    CreatedAt = now.AddDays(-2)
                },
                new()
                {
                    AccountId = primaryDemandAccount.Id,
                    Amount = 1250.00m,
                    Type = TransactionType.Withdrawal,
                    Description = "Shell Akaryakıt & Benzin",
                    Category = "Ulaşım & Akaryakıt",
                    CreatedAt = now.AddDays(-3)
                },
                new()
                {
                    AccountId = primaryDemandAccount.Id,
                    Amount = 890.00m,
                    Type = TransactionType.Withdrawal,
                    Description = "Enerjisa Elektrik Fatura Ödemesi",
                    Category = "Faturalar & Abonelik",
                    CreatedAt = now.AddDays(-4)
                },
                new()
                {
                    AccountId = primaryDemandAccount.Id,
                    Amount = 229.99m,
                    Type = TransactionType.Withdrawal,
                    Description = "Netflix Aylık Abonelik",
                    Category = "Faturalar & Abonelik",
                    CreatedAt = now.AddDays(-5)
                },
                new()
                {
                    AccountId = primaryDemandAccount.Id,
                    Amount = 2150.00m,
                    Type = TransactionType.Withdrawal,
                    Description = "Trendyol Giyim & Ayakkabı Alışverişi",
                    Category = "Eğlence & Alışveriş",
                    CreatedAt = now.AddDays(-6)
                },
                new()
                {
                    AccountId = primaryDemandAccount.Id,
                    Amount = 380.00m,
                    Type = TransactionType.Withdrawal,
                    Description = "Merkez Eczanesi İlaç Alımı",
                    Category = "Sağlık & Eczane",
                    CreatedAt = now.AddDays(-8)
                },
                new()
                {
                    AccountId = primaryDemandAccount.Id,
                    Amount = 45000.00m,
                    Type = TransactionType.Deposit,
                    Description = "Maaş Ödemesi - Kasım 2026",
                    Category = "Maaş & Gelir",
                    CreatedAt = now.AddDays(-10)
                }
            };

            await _context.AccountTransactions.AddRangeAsync(sampleTransactions);
            await _context.SaveChangesAsync();
        }
    }

    // Geriye dönük uyumluluk için statik köprü metodu
    public static void Seed(MiniBankDbContext context)
    {
        var seeder = new DbInitializer(context, new PasswordHasher());
        seeder.SeedAsync().GetAwaiter().GetResult();
    }
}
