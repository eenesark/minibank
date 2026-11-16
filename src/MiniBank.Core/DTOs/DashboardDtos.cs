namespace MiniBank.DTOs;

/// <summary>
/// Giriş yapan müşterinin sadeleştirilmiş ana ekran (dashboard) özeti
/// Ana ekranda kalabalık yaratmadan yalnızca ana TL hesabı, genel bakiye ve sol üstteki hamburger menü ağacını sunar
/// </summary>
public record CustomerDashboardDto(
    Guid CustomerId,
    string CustomerNumber,
    string FullName,
    string GreetingMessage,
    PrimaryAccountDto? PrimaryAccount,
    DashboardFinancialSummaryDto FinancialSummary,
    List<TransactionResponseDto> RecentTransactions,
    List<NavigationCategoryDto> NavigationMenu
);

/// <summary>
/// Ana ekranda gösterilecek birincil Vadesiz TL hesabı
/// </summary>
public record PrimaryAccountDto(
    Guid Id,
    string AccountNumber,
    string Iban,
    decimal Balance,
    string Currency,
    bool IsActive,
    string? MaskedCardNumber,
    string? CardStatus
);

/// <summary>
/// Varlık ve finansal dağılım özeti
/// </summary>
public record DashboardFinancialSummaryDto(
    decimal TotalBalanceTRY,
    decimal TotalDemandTRY,
    decimal TotalTimeDepositTRY,
    int TotalAccountCount,
    int CurrencyAccountCount,
    int ActiveCardCount,
    int ActiveTimeDepositCount
);

/// <summary>
/// Sol üstteki üç çizgi (☰ hamburger) menüsündeki ana kategori
/// </summary>
public record NavigationCategoryDto(
    string Id,
    string Title,
    string Icon,
    string? Badge,
    List<NavigationItemDto> Items
);

/// <summary>
/// Hamburger menüsünün alt öğesi (Tıklandığında ilgili ekrana/moda götürür)
/// </summary>
public record NavigationItemDto(
    string Id,
    string Title,
    string Description,
    string Icon,
    string Route,
    string? Badge = null,
    bool IsEnabled = true
);

