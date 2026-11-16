using Microsoft.Extensions.Logging;
using MiniBank.Data;
using MiniBank.DTOs;
using MiniBank.Entities;

namespace MiniBank.Services;

public interface IDashboardService
{
    /// <summary>
    /// Giriş yapan müşterinin sadeleştirilmiş ana ekran verisini döner.
    /// Ana ekranda sadece ana TL hesabı, özet bakiye, son hareketler ve sol üstteki menü ağacı yer alır.
    /// </summary>
    Task<CustomerDashboardDto> GetCustomerDashboardAsync(Guid customerId);

    /// <summary>
    /// Sol üstteki üç çizgi (☰ hamburger) menüsünün kategori ve alt seçeneklerini döner.
    /// </summary>
    Task<IEnumerable<NavigationCategoryDto>> GetNavigationMenuAsync(Guid customerId);
}

public class DashboardService : IDashboardService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DashboardService> _logger;

    public DashboardService(IUnitOfWork unitOfWork, ILogger<DashboardService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<CustomerDashboardDto> GetCustomerDashboardAsync(Guid customerId)
    {
        var customer = await _unitOfWork.Customers.GetCustomerWithAccountsByIdAsync(customerId);
        if (customer is null)
        {
            throw new KeyNotFoundException("Müşteri bilgisi bulunamadı.");
        }

        var accounts = customer.Accounts.ToList();
        var cards = (await _unitOfWork.Cards.GetByCustomerIdWithDetailsAsync(customerId)).ToList();

        // 1. Ana Vadesiz TL Hesabını bul
        var primaryAccount = accounts.FirstOrDefault(a => a.IsActive && (a.Currency == "TRY" || a.Currency == "TL") && a.AccountType == "Demand")
            ?? accounts.FirstOrDefault(a => a.IsActive && (a.Currency == "TRY" || a.Currency == "TL"))
            ?? accounts.FirstOrDefault(a => a.IsActive);

        PrimaryAccountDto? primaryAccountDto = null;
        List<TransactionResponseDto> recentTransactions = new();

        if (primaryAccount is not null)
        {
            var primaryCard = cards.FirstOrDefault(c => c.AccountId == primaryAccount.Id && c.Status == CardStatus.Active);
            var maskedCardNumber = primaryCard is not null
                ? MaskCardNumber(primaryCard.CardNumber)
                : null;

            primaryAccountDto = new PrimaryAccountDto(
                primaryAccount.Id,
                primaryAccount.AccountNumber,
                primaryAccount.Iban,
                primaryAccount.Balance,
                primaryAccount.Currency,
                primaryAccount.IsActive,
                maskedCardNumber,
                primaryCard?.Status.ToString()
            );

            // Ana hesabın son 5 hareketini getir
            var transactions = await _unitOfWork.Transactions.GetTransactionsByAccountIdAsync(primaryAccount.Id);
            recentTransactions = transactions
                .Take(5)
                .Select(t => new TransactionResponseDto(
                    t.Id,
                    t.AccountId,
                    t.Amount,
                    t.Type.ToString(),
                    t.Description,
                    t.TargetIban,
                    t.CreatedAt,
                    SpendingService.ResolveCategory(t.Description, t.Category, t.Type)
                ))
                .ToList();
        }

        // 2. Finansal Özet Hesaplamaları
        var demandTRY = accounts.Where(a => a.AccountType == "Demand" && (a.Currency == "TRY" || a.Currency == "TL")).Sum(a => a.Balance);
        var timeDepositTRY = accounts.Where(a => a.AccountType == "TimeDeposit" && !a.IsClosed).Sum(a => a.Balance);
        var foreignCount = accounts.Count(a => a.Currency != "TRY" && a.Currency != "TL");
        var activeCardsCount = cards.Count(c => c.Status == CardStatus.Active);
        var activeTimeDepositCount = accounts.Count(a => a.AccountType == "TimeDeposit" && !a.IsClosed);

        var financialSummary = new DashboardFinancialSummaryDto(
            TotalBalanceTRY: demandTRY + timeDepositTRY,
            TotalDemandTRY: demandTRY,
            TotalTimeDepositTRY: timeDepositTRY,
            TotalAccountCount: accounts.Count,
            CurrencyAccountCount: foreignCount,
            ActiveCardCount: activeCardsCount,
            ActiveTimeDepositCount: activeTimeDepositCount
        );

        // 3. Hamburger Menü Kategorilerini Oluştur
        var navigationMenu = BuildNavigationMenu(accounts, cards);

        var fullName = $"{customer.FirstName} {customer.LastName}";
        var greeting = GetGreeting(customer.FirstName);

        return new CustomerDashboardDto(
            CustomerId: customer.Id,
            CustomerNumber: customer.CustomerNumber,
            FullName: fullName,
            GreetingMessage: greeting,
            PrimaryAccount: primaryAccountDto,
            FinancialSummary: financialSummary,
            RecentTransactions: recentTransactions,
            NavigationMenu: navigationMenu
        );
    }

    public async Task<IEnumerable<NavigationCategoryDto>> GetNavigationMenuAsync(Guid customerId)
    {
        var customer = await _unitOfWork.Customers.GetCustomerWithAccountsByIdAsync(customerId);
        if (customer is null)
        {
            throw new KeyNotFoundException("Müşteri bilgisi bulunamadı.");
        }

        var accounts = customer.Accounts.ToList();
        var cards = (await _unitOfWork.Cards.GetByCustomerIdWithDetailsAsync(customerId)).ToList();

        return BuildNavigationMenu(accounts, cards);
    }

    private static List<NavigationCategoryDto> BuildNavigationMenu(List<Account> accounts, List<Card> cards)
    {
        var demandCount = accounts.Count(a => a.AccountType == "Demand" && (a.Currency == "TRY" || a.Currency == "TL"));
        var termCount = accounts.Count(a => a.AccountType == "TimeDeposit" && !a.IsClosed);
        var foreignCount = accounts.Count(a => a.Currency != "TRY" && a.Currency != "TL");
        var activeCardCount = cards.Count(c => c.Status == CardStatus.Active);

        return new List<NavigationCategoryDto>
        {
            new(
                Id: "accounts",
                Title: "Hesaplarım",
                Icon: "fa-wallet",
                Badge: $"{accounts.Count} Hesap",
                Items: new List<NavigationItemDto>
                {
                    new(
                        Id: "acc-demand",
                        Title: "Vadesiz TL Hesabım",
                        Description: "Günlük bankacılık ve ana bakiye",
                        Icon: "fa-money-bill-1-wave",
                        Route: "/accounts/demand",
                        Badge: "Ana Hesap"
                    ),
                    new(
                        Id: "acc-termdeposit",
                        Title: "Vadeli Mevduat Hesaplarım",
                        Description: "Yüksek faiz getirili birikim hesapları",
                        Icon: "fa-piggy-bank",
                        Route: "/accounts/time-deposits",
                        Badge: termCount > 0 ? $"{termCount} Aktif" : "%41 Faiz"
                    ),
                    new(
                        Id: "acc-currency",
                        Title: "Döviz & Altın Hesaplarım",
                        Description: "USD, EUR, Altın (XAU) hesaplarınız",
                        Icon: "fa-coins",
                        Route: "/accounts/currencies",
                        Badge: foreignCount > 0 ? $"{foreignCount} Hesap" : null
                    ),
                    new(
                        Id: "acc-open",
                        Title: "Yeni Hesap Açılışı",
                        Description: "Anında vadesiz, vadeli veya döviz hesabı açın",
                        Icon: "fa-circle-plus",
                        Route: "/accounts/open"
                    )
                }
            ),
            new(
                Id: "cards",
                Title: "Kartlarım",
                Icon: "fa-credit-card",
                Badge: $"{activeCardCount} Kart",
                Items: new List<NavigationItemDto>
                {
                    new(
                        Id: "card-list",
                        Title: "Banka Kartlarım (Debit)",
                        Description: "Vadesiz TL hesabınıza bağlı kartlarınız",
                        Icon: "fa-credit-card",
                        Route: "/cards/my-cards",
                        Badge: activeCardCount > 0 ? $"{activeCardCount} Aktif" : null
                    ),
                    new(
                        Id: "card-settings",
                        Title: "Kart Güvenlik & Limit Ayarları",
                        Description: "İnternet alışverişi, temassız ve limit yönetimi",
                        Icon: "fa-sliders",
                        Route: "/cards/settings"
                    )
                }
            ),
            new(
                Id: "transfers",
                Title: "Para Transferleri",
                Icon: "fa-paper-plane",
                Badge: null,
                Items: new List<NavigationItemDto>
                {
                    new(
                        Id: "transfer-send",
                        Title: "Başka Hesaba Transfer",
                        Description: "IBAN veya Hesap Numarasına Havale / FAST",
                        Icon: "fa-money-bill-transfer",
                        Route: "/transfers/send"
                    ),
                    new(
                        Id: "transfer-history",
                        Title: "Hesap Ekstresi & Geçmiş",
                        Description: "Tüm gelen ve giden işlem kayıtları",
                        Icon: "fa-clock-rotate-left",
                        Route: "/transfers/history"
                    )
                }
            ),
            new(
                Id: "spending",
                Title: "Harcama & Bütçe Yönetimi",
                Icon: "fa-chart-pie",
                Badge: "Analiz",
                Items: new List<NavigationItemDto>
                {
                    new(
                        Id: "spending-summary",
                        Title: "Aylık Harcama Dağılımı",
                        Description: "Kategorili harcama analizi ve pasta grafik",
                        Icon: "fa-chart-pie",
                        Route: "/spending/summary",
                        Badge: "Grafik"
                    ),
                    new(
                        Id: "spending-budgets",
                        Title: "Bütçe ve Limit Hedefleri",
                        Description: "Market, yemek ve fatura harcama limitleri",
                        Icon: "fa-bullseye",
                        Route: "/spending/budgets"
                    )
                }
            ),
            new(
                Id: "currencies",
                Title: "Döviz & Kıymetli Maden",
                Icon: "fa-chart-line",
                Badge: "Canlı Kur",
                Items: new List<NavigationItemDto>
                {
                    new(
                        Id: "curr-rates",
                        Title: "Canlı TCMB Piyasa Kurları",
                        Description: "Dolar, Euro ve Altın güncel piyasa fiyatları",
                        Icon: "fa-chart-simple",
                        Route: "/currency/rates",
                        Badge: "TCMB"
                    ),
                    new(
                        Id: "curr-trade",
                        Title: "Döviz Alış / Satış",
                        Description: "Komisyonsuz anlık döviz ve altın alım satımı",
                        Icon: "fa-right-left",
                        Route: "/currency/trade"
                    )
                }
            ),
            new(
                Id: "profile",
                Title: "Profil & Güvenlik",
                Icon: "fa-user-shield",
                Badge: null,
                Items: new List<NavigationItemDto>
                {
                    new(
                        Id: "prof-info",
                        Title: "Kişisel Bilgilerim",
                        Description: "İletişim, e-posta ve telefon bilgileri",
                        Icon: "fa-id-card",
                        Route: "/profile/info"
                    ),
                    new(
                        Id: "prof-password",
                        Title: "Güvenlik & Şifre Değiştir",
                        Description: "İnternet şubesi giriş şifresi güncelleme",
                        Icon: "fa-key",
                        Route: "/profile/security"
                    )
                }
            )
        };
    }

    private static string GetGreeting(string firstName)
    {
        var hour = DateTime.UtcNow.AddHours(3).Hour; // Türkiye saati (UTC+3)
        var timeGreeting = hour switch
        {
            >= 6 and < 12 => "Günaydın",
            >= 12 and < 18 => "İyi günler",
            >= 18 and < 23 => "İyi akşamlar",
            _ => "İyi geceler"
        };

        return $"{timeGreeting}, {firstName}";
    }

    private static string MaskCardNumber(string cardNumber)
    {
        var clean = cardNumber.Replace(" ", "");
        if (clean.Length < 16) return cardNumber;
        return $"{clean[..4]} {clean.Substring(4, 2)}** **** {clean[^4..]}";
    }
}

