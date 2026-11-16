using Microsoft.Extensions.Logging;
using MiniBank.Data;
using MiniBank.DTOs;
using MiniBank.Entities;

namespace MiniBank.Services;

public interface ITransactionService
{
    Task<TransactionResponseDto> DepositAsync(DepositDto dto);
    Task<TransactionResponseDto> WithdrawAsync(WithdrawDto dto);
    Task<TransactionResponseDto> TransferAsync(TransferDto dto);
    Task<IEnumerable<TransactionResponseDto>> GetHistoryAsync(Guid accountId);
}

public class TransactionService : ITransactionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TransactionService> _logger;

    public TransactionService(IUnitOfWork unitOfWork, ILogger<TransactionService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<TransactionResponseDto> DepositAsync(DepositDto dto)
    {
        var account = await _unitOfWork.Accounts.GetByIdAsync(dto.AccountId);
        if (account is null)
        {
            throw new KeyNotFoundException("Hesap bulunamadı.");
        }

        if (!account.IsActive)
        {
            throw new InvalidOperationException("Hesap pasif durumda olduğu için para yatırılamaz.");
        }

        account.Balance += dto.Amount;

        var desc = string.IsNullOrWhiteSpace(dto.Description) ? "Para Yatırma" : dto.Description;
        var category = SpendingService.ResolveCategory(desc, "", TransactionType.Deposit);

        var transaction = new AccountTransaction
        {
            AccountId = account.Id,
            Amount = dto.Amount,
            Type = TransactionType.Deposit,
            Description = desc,
            Category = category
        };

        _unitOfWork.Accounts.Update(account);
        await _unitOfWork.Transactions.AddAsync(transaction);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Para yatırıldı: {Amount} {Currency} -> Hesap: {Iban}",
            dto.Amount, account.Currency, account.Iban);

        return new TransactionResponseDto(
            transaction.Id,
            account.Id,
            transaction.Amount,
            transaction.Type.ToString(),
            transaction.Description,
            transaction.TargetIban,
            transaction.CreatedAt,
            category
        );
    }

    public async Task<TransactionResponseDto> WithdrawAsync(WithdrawDto dto)
    {
        var account = await _unitOfWork.Accounts.GetByIdAsync(dto.AccountId);
        if (account is null)
        {
            throw new KeyNotFoundException("Hesap bulunamadı.");
        }

        if (!account.IsActive)
        {
            throw new InvalidOperationException("Hesap pasif durumda.");
        }

        if (account.Balance < dto.Amount)
        {
            _logger.LogWarning("Yetersiz bakiye: Hesap {Iban}, Mevcut Bakiye: {Balance}, İstenen: {Amount}",
                account.Iban, account.Balance, dto.Amount);
            throw new InvalidOperationException($"Yetersiz bakiye! Mevcut bakiyeniz: {account.Balance} {account.Currency}");
        }

        account.Balance -= dto.Amount;

        var desc = string.IsNullOrWhiteSpace(dto.Description) ? "Para Çekme" : dto.Description;
        var category = SpendingService.ResolveCategory(desc, "", TransactionType.Withdrawal);

        var transaction = new AccountTransaction
        {
            AccountId = account.Id,
            Amount = dto.Amount,
            Type = TransactionType.Withdrawal,
            Description = desc,
            Category = category
        };

        _unitOfWork.Accounts.Update(account);
        await _unitOfWork.Transactions.AddAsync(transaction);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Para çekildi: {Amount} {Currency} <- Hesap: {Iban}",
            dto.Amount, account.Currency, account.Iban);

        return new TransactionResponseDto(
            transaction.Id,
            account.Id,
            transaction.Amount,
            transaction.Type.ToString(),
            transaction.Description,
            transaction.TargetIban,
            transaction.CreatedAt,
            category
        );
    }

    public async Task<TransactionResponseDto> TransferAsync(TransferDto dto)
    {
        var sourceAccount = await _unitOfWork.Accounts.GetByIdAsync(dto.FromAccountId);
        if (sourceAccount is null)
        {
            throw new KeyNotFoundException("Gönderici hesap bulunamadı.");
        }

        if (!sourceAccount.IsActive)
        {
            throw new InvalidOperationException("Gönderici hesap pasif durumda.");
        }

        if (sourceAccount.Iban.Equals(dto.TargetIban.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Kendi hesabınıza havale/transfer yapamazsınız.");
        }

        if (sourceAccount.Balance < dto.Amount)
        {
            throw new InvalidOperationException($"Yetersiz bakiye! Mevcut bakiyeniz: {sourceAccount.Balance} {sourceAccount.Currency}");
        }

        var targetAccount = await _unitOfWork.Accounts.GetAccountByIbanAsync(dto.TargetIban.Trim());
        if (targetAccount is null)
        {
            throw new KeyNotFoundException("Alıcı IBAN sistemde bulunamadı.");
        }

        if (!targetAccount.IsActive)
        {
            throw new InvalidOperationException("Alıcı hesap pasif durumda.");
        }

        // Bakiyeleri güncelle
        sourceAccount.Balance -= dto.Amount;
        targetAccount.Balance += dto.Amount;

        // Gönderen için çıkış hareketi
        var outDesc = $"Havale Giden: {targetAccount.Iban} ({dto.Description})";
        var outCategory = SpendingService.ResolveCategory(dto.Description, "Para Transferi & EFT", TransactionType.TransferOut);

        var outTransaction = new AccountTransaction
        {
            AccountId = sourceAccount.Id,
            Amount = dto.Amount,
            Type = TransactionType.TransferOut,
            Description = outDesc,
            Category = outCategory,
            TargetIban = targetAccount.Iban
        };

        // Alıcı için giriş hareketi
        var inDesc = $"Havale Gelen: {sourceAccount.Iban} ({dto.Description})";
        var inCategory = SpendingService.ResolveCategory(dto.Description, "Para Transferi & EFT", TransactionType.TransferIn);

        var inTransaction = new AccountTransaction
        {
            AccountId = targetAccount.Id,
            Amount = dto.Amount,
            Type = TransactionType.TransferIn,
            Description = inDesc,
            Category = inCategory,
            TargetIban = sourceAccount.Iban
        };

        _unitOfWork.Accounts.Update(sourceAccount);
        _unitOfWork.Accounts.Update(targetAccount);
        await _unitOfWork.Transactions.AddAsync(outTransaction);
        await _unitOfWork.Transactions.AddAsync(inTransaction);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Transfer başarılı: {Amount} {Currency} -> Gönderen: {Source}, Alıcı: {Target}",
            dto.Amount, sourceAccount.Currency, sourceAccount.Iban, targetAccount.Iban);

        return new TransactionResponseDto(
            outTransaction.Id,
            sourceAccount.Id,
            outTransaction.Amount,
            outTransaction.Type.ToString(),
            outTransaction.Description,
            outTransaction.TargetIban,
            outTransaction.CreatedAt,
            outCategory
        );
    }

    public async Task<IEnumerable<TransactionResponseDto>> GetHistoryAsync(Guid accountId)
    {
        var history = await _unitOfWork.Transactions.GetTransactionsByAccountIdAsync(accountId);

        return history.Select(t => new TransactionResponseDto(
            t.Id,
            t.AccountId,
            t.Amount,
            t.Type.ToString(),
            t.Description,
            t.TargetIban,
            t.CreatedAt,
            SpendingService.ResolveCategory(t.Description, t.Category, t.Type)
        ));
    }
}

