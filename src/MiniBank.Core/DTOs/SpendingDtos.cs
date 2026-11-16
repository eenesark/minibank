namespace MiniBank.DTOs;

/// <summary>
/// Belirli bir ay için harcama, gelir, tasarruf ve kategori dağılımı özeti
/// </summary>
public record SpendingSummaryDto(
    int Month,
    int Year,
    string PeriodName,
    decimal TotalExpenseTRY,
    decimal TotalIncomeTRY,
    decimal NetSavingsTRY,
    decimal SavingsRate,
    List<SpendingCategorySummaryDto> CategoryBreakdown,
    List<DailySpendingDto> DailyTrend,
    List<TransactionResponseDto> TopExpenses,
    List<SpendingBudgetStatusDto> Budgets
);

/// <summary>
/// Kategori bazlı harcama özeti (Pasta/Halka grafik ve liste için)
/// </summary>
public record SpendingCategorySummaryDto(
    string Category,
    decimal Amount,
    decimal Percentage,
    int TransactionCount,
    string Icon,
    string Color
);

/// <summary>
/// Günlük harcama grafiği için veri noktası (Trend/Bar grafik)
/// </summary>
public record DailySpendingDto(
    int Day,
    string DateFormatted,
    decimal ExpenseAmount,
    decimal IncomeAmount
);

/// <summary>
/// Belirli bir kategori için bütçe hedefi ve güncel harcama durumu
/// </summary>
public record SpendingBudgetStatusDto(
    string Category,
    decimal BudgetLimit,
    decimal CurrentSpent,
    decimal Remaining,
    decimal UsagePercentage,
    bool IsExceeded,
    string Icon,
    string Color
);

/// <summary>
/// Kullanıcının bir kategoriye aylık bütçe limiti koyması için model
/// </summary>
public record SetCategoryBudgetDto(
    string Category,
    decimal BudgetLimit
);

