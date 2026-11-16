using Microsoft.Extensions.Logging;
using MiniBank.Data;
using MiniBank.DTOs;
using MiniBank.Entities;
using MiniBank.Services.Generators;

namespace MiniBank.Services;

public class TimeDepositService : ITimeDepositService
{
    private const decimal MinDepositAmount = 1000m; // Minimum 1.000 TL
    private const decimal WithholdingTaxRate = 0.075m; // Stopaj Vergisi (%7.5)

    private readonly IUnitOfWork _unitOfWork;
    private readonly IBankingCodeGenerator _codeGenerator;
    private readonly IFeatureManagementService _featureService;
    private readonly ILogger<TimeDepositService> _logger;

    public TimeDepositService(
        IUnitOfWork unitOfWork,
        IBankingCodeGenerator codeGenerator,
        IFeatureManagementService featureService,
        ILogger<TimeDepositService> logger)
    {
        _unitOfWork = unitOfWork;
        _codeGenerator = codeGenerator;
        _featureService = featureService;
        _logger = logger;
    }

    public IEnumerable<TimeDepositRateTierDto> GetRateTiers()
    {
        return new List<TimeDepositRateTierDto>
        {
            new("1 - 32 Gün", 1, 32, 32.0m, "32 güne kadar %32 yıllık faiz oranı"),
            new("33 - 90 Gün", 33, 90, 38.0m, "32-90 gün arası %38 yıllık faiz oranı"),
            new("91+ Gün", 91, null, 41.0m, "90 gün ve üzeri %41 yıllık faiz oranı")
        };
    }

    public TimeDepositCalculateResultDto Calculate(TimeDepositCalculateDto dto)
    {
        if (dto.Amount < MinDepositAmount)
        {
            throw new InvalidOperationException($"Vadeli mevduat getiri hesaplaması için minimum tutar {MinDepositAmount:N0} TL olmalıdır.");
        }

        if (dto.Days < 1)
        {
            throw new InvalidOperationException("Vade gün sayısı en az 1 gün olmalıdır.");
        }

        var annualRate = GetInterestRateForDays(dto.Days);

        // Türk Bankacılık Standart Faiz Formülü: (Anapara * Faiz Oranı * Gün) / 36500
        var grossInterest = Math.Round((dto.Amount * annualRate * dto.Days) / 36500m, 2);
        var withholdingTax = Math.Round(grossInterest * WithholdingTaxRate, 2);
        var netInterest = grossInterest - withholdingTax;
        var totalMaturityAmount = dto.Amount + netInterest;
        var maturityDate = DateTime.UtcNow.AddDays(dto.Days);

        return new TimeDepositCalculateResultDto(
            PrincipalAmount: dto.Amount,
            MaturityDays: dto.Days,
            AnnualInterestRate: annualRate,
            GrossInterest: grossInterest,
            WithholdingTax: withholdingTax,
            NetInterest: netInterest,
            TotalMaturityAmount: totalMaturityAmount,
            MaturityDate: maturityDate
        );
    }

    public async Task<TimeDepositAccountDto> OpenTimeDepositAsync(Guid currentUserId, OpenTimeDepositDto dto)
    {
        // 0. Dinamik Özellik / Yetki Kontrolü: Admin bu müşteriye vadeli mevduat yetkisi vermiş mi?
        var isFeatureEnabled = await _featureService.IsFeatureEnabledAsync(currentUserId, "TIME_DEPOSIT");
        if (!isFeatureEnabled)
        {
            _logger.LogWarning("Vadeli mevduat açılışı reddedildi: Müşteri ({UserId}) yetkisi kapalı.", currentUserId);
            throw new UnauthorizedAccessException("Vadeli mevduat hesabı açma özelliği hesabınız için yetkilendirilmemiştir. Lütfen banka yöneticiniz ile iletişime geçin.");
        }

        if (dto.Amount < MinDepositAmount)
        {
            throw new InvalidOperationException($"Vadeli mevduat hesabı en az {MinDepositAmount:N0} TL ile açılabilir.");
        }

        if (dto.Days < 1)
        {
            throw new InvalidOperationException("Vade gün sayısı en az 1 gün olmalıdır.");
        }

        var targetSourceId = dto.SourceAccountId != Guid.Empty
            ? dto.SourceAccountId
            : (dto.DemandAccountId ?? Guid.Empty);

        var sourceAccount = await _unitOfWork.Accounts.GetByIdAsync(targetSourceId);
        if (sourceAccount is null)
        {
            throw new KeyNotFoundException("Kaynak banka hesabı bulunamadı.");
        }

        if (sourceAccount.CustomerId != currentUserId)
        {
            throw new UnauthorizedAccessException("Yalnızca kendi adınıza kayıtlı vadesiz hesaptan vadeli hesap açabilirsiniz.");
        }

        if (!sourceAccount.IsActive)
        {
            throw new InvalidOperationException("Pasif durumdaki hesaptan vadeli hesap açılamaz.");
        }

        if (sourceAccount.Currency.ToUpperInvariant() != "TRY")
        {
            throw new InvalidOperationException("Vadeli mevduat hesabı yalnızca Türk Lirası (TRY) hesabı üzerinden açılabilir.");
        }

        if (sourceAccount.Balance < dto.Amount)
        {
            throw new InvalidOperationException($"Yetersiz bakiye! Vadesiz hesabınızda {sourceAccount.Balance:N2} TL bulunmaktadır, vadeli için gereken: {dto.Amount:N2} TL.");
        }

        // Getiri hesaplaması
        var calc = Calculate(new TimeDepositCalculateDto(dto.Amount, dto.Days));

        // 1. Kaynak hesaptan anaparayı düş
        sourceAccount.Balance -= dto.Amount;

        // 2. Yeni vadeli hesap oluştur
        var accountNumber = _codeGenerator.GenerateAccountNumber();
        var iban = _codeGenerator.GenerateIban(accountNumber);

        var timeDepositAccount = new Account
        {
            CustomerId = currentUserId,
            AccountNumber = accountNumber,
            Iban = iban,
            Currency = "TRY",
            Balance = dto.Amount,
            IsActive = true,
            AccountType = "TimeDeposit",
            MaturityDays = dto.Days,
            InterestRate = calc.AnnualInterestRate,
            MaturityDate = calc.MaturityDate,
            InitialDepositAmount = dto.Amount,
            ExpectedInterestAmount = calc.NetInterest,
            IsMatured = false,
            IsClosed = false,
            CreatedAt = DateTime.UtcNow
        };

        // 3. Hesap hareketlerini kaydet
        var sourceTx = new AccountTransaction
        {
            AccountId = sourceAccount.Id,
            Amount = dto.Amount,
            Type = TransactionType.TimeDepositOpen,
            Description = $"Vadeli Mevduat Açılışı ({dto.Days} Gün, %{calc.AnnualInterestRate:N0} Yıllık Faiz)",
            TargetIban = iban
        };

        var timeDepositTx = new AccountTransaction
        {
            AccountId = timeDepositAccount.Id,
            Amount = dto.Amount,
            Type = TransactionType.Deposit,
            Description = $"Vadeli Hesap Açılış Anaparası (Vade: {calc.MaturityDate:yyyy-MM-dd}, Net Getiri: {calc.NetInterest:N2} TL)",
            TargetIban = sourceAccount.Iban
        };

        _unitOfWork.Accounts.Update(sourceAccount);
        await _unitOfWork.Accounts.AddAsync(timeDepositAccount);
        await _unitOfWork.Transactions.AddAsync(sourceTx);
        await _unitOfWork.Transactions.AddAsync(timeDepositTx);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Vadeli mevduat hesabı açıldı: Müşteri {UserId} - Tutar: {Amt:N2} TL - Vade: {Days} Gün (%{Rate:N0}) - IBAN: {Iban}",
            currentUserId, dto.Amount, dto.Days, calc.AnnualInterestRate, iban);

        return MapToDto(timeDepositAccount);
    }

    public async Task<CloseTimeDepositResultDto> CloseTimeDepositAsync(Guid currentUserId, Guid timeDepositAccountId, Guid? targetDemandAccountId = null)
    {
        var timeDeposit = await _unitOfWork.Accounts.GetByIdAsync(timeDepositAccountId);
        if (timeDeposit is null)
        {
            throw new KeyNotFoundException("Vadeli mevduat hesabı bulunamadı.");
        }

        if (timeDeposit.CustomerId != currentUserId)
        {
            throw new UnauthorizedAccessException("Bu vadeli hesabı kapatma yetkiniz bulunmamaktadır.");
        }

        if (timeDeposit.AccountType != "TimeDeposit")
        {
            throw new InvalidOperationException("Seçilen hesap bir vadeli mevduat hesabı değildir.");
        }

        if (timeDeposit.IsClosed)
        {
            throw new InvalidOperationException("Bu vadeli hesap zaten daha önce kapatılmıştır.");
        }

        // Hedef vadesiz hesabı belirle (Para buraya aktarılacak)
        Account? targetDemandAccount = null;
        if (targetDemandAccountId.HasValue)
        {
            targetDemandAccount = await _unitOfWork.Accounts.GetByIdAsync(targetDemandAccountId.Value);
            if (targetDemandAccount is null || targetDemandAccount.CustomerId != currentUserId || targetDemandAccount.Currency != "TRY" || targetDemandAccount.AccountType != "Demand")
            {
                throw new InvalidOperationException("Belirtilen hedef vadesiz hesap geçerli değildir.");
            }
        }
        else
        {
            var customerAccounts = await _unitOfWork.Accounts.GetAccountsByCustomerIdAsync(currentUserId);
            targetDemandAccount = customerAccounts.FirstOrDefault(a => a.Currency == "TRY" && a.AccountType == "Demand" && a.IsActive);
            if (targetDemandAccount is null)
            {
                throw new InvalidOperationException("Paranın aktarılacağı aktif bir vadesiz TL hesabınız bulunamadı.");
            }
        }

        var isMatured = DateTime.UtcNow >= timeDeposit.MaturityDate;
        decimal returnedPrincipal = timeDeposit.Balance;
        decimal earnedInterest = 0m;
        decimal totalTransferred = 0m;
        string message;

        if (isMatured)
        {
            // Vade Günü Dolmuş: Anapara + Net Faiz ödenir
            earnedInterest = timeDeposit.ExpectedInterestAmount ?? 0m;
            totalTransferred = returnedPrincipal + earnedInterest;

            var interestTx = new AccountTransaction
            {
                AccountId = timeDeposit.Id,
                Amount = earnedInterest,
                Type = TransactionType.InterestPayment,
                Description = $"Vade Sonu Net Faiz Getirisi (%{timeDeposit.InterestRate:N0} Yıllık Faiz)",
                TargetIban = targetDemandAccount.Iban
            };

            var transferTx = new AccountTransaction
            {
                AccountId = targetDemandAccount.Id,
                Amount = totalTransferred,
                Type = TransactionType.TimeDepositClose,
                Description = $"Vadeli Hesap Vade Sonu Dönüşü ({timeDeposit.Iban}): {returnedPrincipal:N2} TL Anapara + {earnedInterest:N2} TL Faiz",
                TargetIban = timeDeposit.Iban
            };

            await _unitOfWork.Transactions.AddAsync(interestTx);
            await _unitOfWork.Transactions.AddAsync(transferTx);

            timeDeposit.IsMatured = true;
            message = $"Vadeli hesabınızın vadesi dolmuştur. {returnedPrincipal:N2} TL anapara ve {earnedInterest:N2} TL net faiz getirisi olmak üzere toplam {totalTransferred:N2} TL vadesiz hesabınıza aktarıldı.";
        }
        else
        {
            // Vadeden Önce Bozuldu (Erken Kapanış): Faiz kaybedilir, sadece anapara aktarılır
            earnedInterest = 0m;
            totalTransferred = returnedPrincipal;

            var earlyTx = new AccountTransaction
            {
                AccountId = targetDemandAccount.Id,
                Amount = totalTransferred,
                Type = TransactionType.TimeDepositClose,
                Description = $"Vadeli Hesap Erken Kapanış / Vade Bozma İadesi ({timeDeposit.Iban})",
                TargetIban = timeDeposit.Iban
            };

            await _unitOfWork.Transactions.AddAsync(earlyTx);

            timeDeposit.IsMatured = false;
            message = $"Vadeli hesabınız vadesinden önce bozuldu. Faiz hakkı kaybedildi; {totalTransferred:N2} TL tutarındaki anaparanız vadesiz hesabınıza aktarıldı.";
        }

        // Bakiyeleri ve durumları güncelle
        targetDemandAccount.Balance += totalTransferred;
        timeDeposit.Balance = 0;
        timeDeposit.IsClosed = true;
        timeDeposit.IsActive = false;

        _unitOfWork.Accounts.Update(targetDemandAccount);
        _unitOfWork.Accounts.Update(timeDeposit);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Vadeli hesap kapatıldı: {Id} - Aktarılan: {Tot:N2} TL - Erken mi: {Early}",
            timeDepositAccountId, totalTransferred, !isMatured);

        return new CloseTimeDepositResultDto(
            Success: true,
            Message: message,
            TargetAccountId: targetDemandAccount.Id,
            TargetIban: targetDemandAccount.Iban,
            ReturnedPrincipal: returnedPrincipal,
            EarnedInterest: earnedInterest,
            TotalTransferred: totalTransferred,
            WasMaturedEarly: !isMatured
        );
    }

    public async Task<IEnumerable<TimeDepositAccountDto>> GetMyTimeDepositsAsync(Guid currentUserId)
    {
        var allAccounts = await _unitOfWork.Accounts.GetAccountsByCustomerIdAsync(currentUserId);
        var timeDeposits = allAccounts
            .Where(a => a.AccountType == "TimeDeposit")
            .OrderByDescending(a => a.CreatedAt)
            .Select(MapToDto);

        return timeDeposits;
    }

    private static decimal GetInterestRateForDays(int days)
    {
        if (days <= 32) return 32.0m;
        if (days <= 90) return 38.0m;
        return 41.0m;
    }

    private static TimeDepositAccountDto MapToDto(Account a)
    {
        var now = DateTime.UtcNow;
        var maturityDate = a.MaturityDate ?? a.CreatedAt.AddDays(a.MaturityDays ?? 32);
        var remainingDays = (int)Math.Ceiling((maturityDate - now).TotalDays);
        if (remainingDays < 0) remainingDays = 0;

        var isMatured = a.IsMatured || (now >= maturityDate && !a.IsClosed);

        string status;
        if (a.IsClosed)
        {
            status = "Kapatıldı";
        }
        else if (isMatured)
        {
            status = "Vade Doldu - Ödemeye Hazır";
        }
        else
        {
            status = $"Aktif - {remainingDays} Gün Kaldı";
        }

        var principal = a.InitialDepositAmount ?? a.Balance;
        var netInterest = a.ExpectedInterestAmount ?? 0m;

        return new TimeDepositAccountDto(
            Id: a.Id,
            AccountNumber: a.AccountNumber,
            Iban: a.Iban,
            PrincipalAmount: principal,
            CurrentBalance: a.Balance,
            InterestRate: a.InterestRate ?? 32.0m,
            MaturityDays: a.MaturityDays ?? 32,
            StartDate: a.CreatedAt,
            MaturityDate: maturityDate,
            RemainingDays: a.IsClosed ? 0 : remainingDays,
            ExpectedNetInterest: netInterest,
            TotalMaturityAmount: principal + netInterest,
            IsMatured: isMatured,
            IsClosed: a.IsClosed,
            Status: status,
            CustomerId: a.CustomerId
        );
    }
}

