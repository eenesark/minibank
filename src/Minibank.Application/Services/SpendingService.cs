using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MiniBank.Data;
using MiniBank.DTOs;
using MiniBank.Entities;
using System.Globalization;

namespace MiniBank.Services;

public interface ISpendingService
{
    /// <summary>
    /// Belirli bir ay için harcama, gelir, tasarruf ve kategori bazlı bütçe analizini döner
    /// </summary>
    Task<SpendingSummaryDto> GetMonthlySummaryAsync(Guid customerId, int? month = null, int? year = null);

    /// <summary>
    /// Belirli bir kategoriye ait harcama hareketlerini listeler
    /// </summary>
    Task<IEnumerable<TransactionResponseDto>> GetCategoryTransactionsAsync(Guid customerId, string category, int? month = null, int? year = null);

    /// <summary>
    /// Bir kategori için aylık harcama bütçesi belirler
    /// </summary>
    Task<SpendingBudgetStatusDto> SetCategoryBudgetAsync(Guid customerId, SetCategoryBudgetDto dto);
}

public class SpendingService : ISpendingService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SpendingService> _logger;

    // Kategori Tanımları, İkonları ve Renkleri
    private static readonly Dictionary<string, (string Icon, string Color, decimal DefaultBudget)> CategoryMetadata = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Market & Gıda", ("fa-cart-shopping", "#10b981", 6000m) },
        { "Restoran & Kafe", ("fa-utensils", "#f59e0b", 3500m) },
        { "Faturalar & Abonelik", ("fa-receipt", "#6366f1", 3000m) },
        { "Ulaşım & Akaryakıt", ("fa-gas-pump", "#ef4444", 2500m) },
        { "Eğlence & Alışveriş", ("fa-bag-shopping", "#ec4899", 4000m) },
        { "Sağlık & Eczane", ("fa-heart-pulse", "#14b8a6", 1500m) },
        { "Para Transferi & EFT", ("fa-arrow-right-arrow-left", "#3b82f6", 10000m) },
        { "Yatırım & Vadeli", ("fa-piggy-bank", "#8b5cf6", 20000m) },
        { "Diğer Harcamalar", ("fa-tags", "#64748b", 2000m) }
    };

    public SpendingService(IUnitOfWork unitOfWork, ILogger<SpendingService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<SpendingSummaryDto> GetMonthlySummaryAsync(Guid customerId, int? month = null, int? year = null)
    {
        var targetMonth = month ?? DateTime.UtcNow.Month;
        var targetYear = year ?? DateTime.UtcNow.Year;

        var startDate = new DateTime(targetYear, targetMonth, 1, 0, 0, 0, DateTimeKind.Utc);
        var daysInMonth = DateTime.DaysInMonth(targetYear, targetMonth);
        var endDate = new DateTime(targetYear, targetMonth, daysInMonth, 23, 59, 59, DateTimeKind.Utc);

        var trCulture = new CultureInfo("tr-TR");
        var periodName = $"{startDate.ToString("MMMM", trCulture)} {targetYear}";

        // 1. Müşterinin tüm hesaplarını ve o aya ait hareketlerini çek
        var customerAccounts = (await _unitOfWork.Accounts.GetAccountsByCustomerIdAsync(customerId)).ToList();
        var accountIds = customerAccounts.Select(a => a.Id).ToList();

        var allTransactions = new List<AccountTransaction>();
        foreach (var accId in accountIds)
        {
            var txs = await _unitOfWork.Transactions.GetTransactionsByAccountIdAsync(accId);
            allTransactions.AddRange(txs.Where(t => t.CreatedAt >= startDate && t.CreatedAt <= endDate));
        }

        decimal totalExpense = 0m;
        decimal totalIncome = 0m;

        var categoryAmounts = new Dictionary<string, (decimal Total, int Count)>(StringComparer.OrdinalIgnoreCase);
        var dailyExpenseDict = new Dictionary<int, decimal>();
        var dailyIncomeDict = new Dictionary<int, decimal>();

        for (int i = 1; i <= daysInMonth; i++)
        {
            dailyExpenseDict[i] = 0m;
            dailyIncomeDict[i] = 0m;
        }

        var expenseTransactions = new List<(AccountTransaction Tx, string Category)>();

        foreach (var tx in allTransactions)
        {
            var day = tx.CreatedAt.Day;
            var isExpense = IsExpenseTransaction(tx);

            if (isExpense)
            {
                totalExpense += tx.Amount;
                dailyExpenseDict[day] += tx.Amount;

                var cat = ResolveCategory(tx.Description, tx.Category, tx.Type);
                expenseTransactions.Add((tx, cat));

                if (!categoryAmounts.ContainsKey(cat))
                {
                    categoryAmounts[cat] = (tx.Amount, 1);
                }
                else
                {
                    var curr = categoryAmounts[cat];
                    categoryAmounts[cat] = (curr.Total + tx.Amount, curr.Count + 1);
                }
            }
            else
            {
                totalIncome += tx.Amount;
                dailyIncomeDict[day] += tx.Amount;
            }
        }

        // 2. Kategori Dağılımını Hesapla (Pasta Grafik)
        var categoryBreakdown = new List<SpendingCategorySummaryDto>();
        foreach (var (catName, data) in categoryAmounts.OrderByDescending(c => c.Value.Total))
        {
            var meta = GetCategoryMetadata(catName);
            var percentage = totalExpense > 0 ? Math.Round((data.Total / totalExpense) * 100m, 1) : 0m;

            categoryBreakdown.Add(new SpendingCategorySummaryDto(
                Category: catName,
                Amount: data.Total,
                Percentage: percentage,
                TransactionCount: data.Count,
                Icon: meta.Icon,
                Color: meta.Color
            ));
        }

        // 3. Günlük Trend
        var dailyTrend = new List<DailySpendingDto>();
        for (int day = 1; day <= daysInMonth; day++)
        {
            dailyTrend.Add(new DailySpendingDto(
                Day: day,
                DateFormatted: $"{day:D2} {startDate.ToString("MMM", trCulture)}",
                ExpenseAmount: dailyExpenseDict[day],
                IncomeAmount: dailyIncomeDict[day]
            ));
        }

        // 4. En Yüksek Harcamalar (Top 5)
        var topExpenses = expenseTransactions
            .OrderByDescending(x => x.Tx.Amount)
            .Take(5)
            .Select(x => new TransactionResponseDto(
                x.Tx.Id,
                x.Tx.AccountId,
                x.Tx.Amount,
                x.Tx.Type.ToString(),
                x.Tx.Description,
                x.Tx.TargetIban,
                x.Tx.CreatedAt,
                x.Category
            ))
            .ToList();

        // 5. Bütçe Durumları
        var budgets = new List<SpendingBudgetStatusDto>();
        foreach (var (catName, meta) in CategoryMetadata)
        {
            var spent = categoryAmounts.TryGetValue(catName, out var data) ? data.Total : 0m;
            var limit = meta.DefaultBudget;
            var usage = limit > 0 ? Math.Round((spent / limit) * 100m, 1) : 0m;
            var remaining = Math.Max(0m, limit - spent);
            var isExceeded = spent > limit;

            budgets.Add(new SpendingBudgetStatusDto(
                Category: catName,
                BudgetLimit: limit,
                CurrentSpent: spent,
                Remaining: remaining,
                UsagePercentage: usage,
                IsExceeded: isExceeded,
                Icon: meta.Icon,
                Color: meta.Color
            ));
        }

        var netSavings = totalIncome - totalExpense;
        var savingsRate = totalIncome > 0 ? Math.Round((netSavings / totalIncome) * 100m, 1) : 0m;

        return new SpendingSummaryDto(
            Month: targetMonth,
            Year: targetYear,
            PeriodName: periodName,
            TotalExpenseTRY: totalExpense,
            TotalIncomeTRY: totalIncome,
            NetSavingsTRY: netSavings,
            SavingsRate: savingsRate,
            CategoryBreakdown: categoryBreakdown,
            DailyTrend: dailyTrend,
            TopExpenses: topExpenses,
            Budgets: budgets
        );
    }

    public async Task<IEnumerable<TransactionResponseDto>> GetCategoryTransactionsAsync(Guid customerId, string category, int? month = null, int? year = null)
    {
        var targetMonth = month ?? DateTime.UtcNow.Month;
        var targetYear = year ?? DateTime.UtcNow.Year;

        var startDate = new DateTime(targetYear, targetMonth, 1, 0, 0, 0, DateTimeKind.Utc);
        var daysInMonth = DateTime.DaysInMonth(targetYear, targetMonth);
        var endDate = new DateTime(targetYear, targetMonth, daysInMonth, 23, 59, 59, DateTimeKind.Utc);

        var customerAccounts = (await _unitOfWork.Accounts.GetAccountsByCustomerIdAsync(customerId)).ToList();
        var accountIds = customerAccounts.Select(a => a.Id).ToList();

        var result = new List<TransactionResponseDto>();

        foreach (var accId in accountIds)
        {
            var txs = await _unitOfWork.Transactions.GetTransactionsByAccountIdAsync(accId);
            var filtered = txs.Where(t => t.CreatedAt >= startDate && t.CreatedAt <= endDate && IsExpenseTransaction(t));

            foreach (var t in filtered)
            {
                var cat = ResolveCategory(t.Description, t.Category, t.Type);
                if (string.Equals(cat, category, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(new TransactionResponseDto(
                        t.Id,
                        t.AccountId,
                        t.Amount,
                        t.Type.ToString(),
                        t.Description,
                        t.TargetIban,
                        t.CreatedAt,
                        cat
                    ));
                }
            }
        }

        return result.OrderByDescending(t => t.CreatedAt);
    }

    public Task<SpendingBudgetStatusDto> SetCategoryBudgetAsync(Guid customerId, SetCategoryBudgetDto dto)
    {
        var meta = GetCategoryMetadata(dto.Category);
        var limit = Math.Max(0m, dto.BudgetLimit);

        var result = new SpendingBudgetStatusDto(
            Category: dto.Category,
            BudgetLimit: limit,
            CurrentSpent: 0m,
            Remaining: limit,
            UsagePercentage: 0m,
            IsExceeded: false,
            Icon: meta.Icon,
            Color: meta.Color
        );

        return Task.FromResult(result);
    }

    private static bool IsExpenseTransaction(AccountTransaction tx)
    {
        return tx.Type switch
        {
            TransactionType.Withdrawal => true,
            TransactionType.TransferOut => true,
            TransactionType.FxBuy => true,
            _ => false
        };
    }

    public static string ResolveCategory(string description, string existingCategory, TransactionType type)
    {
        if (!string.IsNullOrWhiteSpace(existingCategory) && existingCategory != "Diğer" && CategoryMetadata.ContainsKey(existingCategory))
        {
            return existingCategory;
        }

        var d = description.ToLowerInvariant();

        // 1. Market & Gıda
        if (d.Contains("migros") || d.Contains("bim") || d.Contains("a101") || d.Contains("şok") || d.Contains("sok") ||
            d.Contains("carrefour") || d.Contains("market") || d.Contains("bakkal") || d.Contains("manav") ||
            d.Contains("kasap") || d.Contains("fırın") || d.Contains("getir") || d.Contains("istegelsin") || d.Contains("gıda"))
        {
            return "Market & Gıda";
        }

        // 2. Restoran & Kafe
        if (d.Contains("starbucks") || d.Contains("kahve") || d.Contains("cafe") || d.Contains("kafe") ||
            d.Contains("restoran") || d.Contains("lokanta") || d.Contains("burger") || d.Contains("pizza") ||
            d.Contains("kebap") || d.Contains("yemeksepeti") || d.Contains("yemek") || d.Contains("pastane"))
        {
            return "Restoran & Kafe";
        }

        // 3. Faturalar & Abonelik
        if (d.Contains("fatura") || d.Contains("enerjisa") || d.Contains("iski") || d.Contains("igdaş") ||
            d.Contains("turkcell") || d.Contains("vodafone") || d.Contains("telekom") || d.Contains("netflix") ||
            d.Contains("spotify") || d.Contains("youtube") || d.Contains("aidat") || d.Contains("kira") ||
            d.Contains("elektrik") || d.Contains("su faturası") || d.Contains("doğalgaz"))
        {
            return "Faturalar & Abonelik";
        }

        // 4. Ulaşım & Akaryakıt
        if (d.Contains("benzin") || d.Contains("akaryakıt") || d.Contains("opet") || d.Contains("shell") ||
            d.Contains("bp") || d.Contains("petrol") || d.Contains("taksi") || d.Contains("uber") ||
            d.Contains("martı") || d.Contains("bilet") || d.Contains("metro") || d.Contains("thy") ||
            d.Contains("pegasus") || d.Contains("ulaşım") || d.Contains("otopark") || d.Contains("hgs"))
        {
            return "Ulaşım & Akaryakıt";
        }

        // 5. Eğlence & Alışveriş
        if (d.Contains("trendyol") || d.Contains("hepsiburada") || d.Contains("amazon") || d.Contains("zara") ||
            d.Contains("boyner") || d.Contains("lcw") || d.Contains("avm") || d.Contains("giyim") ||
            d.Contains("sinema") || d.Contains("tiyatro") || d.Contains("kitap") || d.Contains("alışveriş"))
        {
            return "Eğlence & Alışveriş";
        }

        // 6. Sağlık & Eczane
        if (d.Contains("eczane") || d.Contains("hastane") || d.Contains("sağlık") || d.Contains("ilaç") ||
            d.Contains("klinik") || d.Contains("doktor") || d.Contains("diş"))
        {
            return "Sağlık & Eczane";
        }

        // 7. Para Transferleri
        if (type == TransactionType.TransferOut || d.Contains("havale") || d.Contains("eft") || d.Contains("fast") || d.Contains("transfer"))
        {
            return "Para Transferi & EFT";
        }

        return "Diğer Harcamalar";
    }

    private static (string Icon, string Color, decimal DefaultBudget) GetCategoryMetadata(string category)
    {
        if (CategoryMetadata.TryGetValue(category, out var meta))
        {
            return meta;
        }

        return ("fa-tags", "#64748b", 2000m);
    }
}

