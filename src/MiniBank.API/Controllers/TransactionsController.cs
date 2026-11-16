using Microsoft.AspNetCore.Mvc;
using MiniBank.Common;
using MiniBank.DTOs;
using MiniBank.Services;

namespace MiniBank.Controllers;

public class TransactionsController : BaseApiController
{
    private readonly ITransactionService _transactionService;

    public TransactionsController(ITransactionService transactionService)
    {
        _transactionService = transactionService;
    }

    /// <summary>
    /// Hesaba nakit para yatırır
    /// </summary>
    [HttpPost("deposit")]
    public async Task<ActionResult<ApiResponse<TransactionResponseDto>>> Deposit([FromBody] DepositDto dto)
    {
        try
        {
            var result = await _transactionService.DepositAsync(dto);
            return Success(result, "Para yatırma işlemi başarıyla tamamlandı.");
        }
        catch (KeyNotFoundException ex)
        {
            return ErrorNotFound<TransactionResponseDto>(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return ErrorBadRequest<TransactionResponseDto>(ex.Message);
        }
    }

    /// <summary>
    /// Hesaptan nakit para çeker
    /// </summary>
    [HttpPost("withdraw")]
    public async Task<ActionResult<ApiResponse<TransactionResponseDto>>> Withdraw([FromBody] WithdrawDto dto)
    {
        try
        {
            var result = await _transactionService.WithdrawAsync(dto);
            return Success(result, "Para çekme işlemi başarıyla tamamlandı.");
        }
        catch (KeyNotFoundException ex)
        {
            return ErrorNotFound<TransactionResponseDto>(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return ErrorBadRequest<TransactionResponseDto>(ex.Message);
        }
    }

    /// <summary>
    /// Başka bir IBAN'a para transferi (Havale/EFT) yapar
    /// </summary>
    [HttpPost("transfer")]
    public async Task<ActionResult<ApiResponse<TransactionResponseDto>>> Transfer([FromBody] TransferDto dto)
    {
        try
        {
            var result = await _transactionService.TransferAsync(dto);
            return Success(result, "Para transferi başarıyla gerçekleştirildi.");
        }
        catch (KeyNotFoundException ex)
        {
            return ErrorNotFound<TransactionResponseDto>(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return ErrorBadRequest<TransactionResponseDto>(ex.Message);
        }
    }

    /// <summary>
    /// Bir hesabın tüm geçmiş hareketlerini ve hesap ekstresini listeler
    /// </summary>
    [HttpGet("history/{accountId:guid}")]
    public async Task<ActionResult<ApiResponse<IEnumerable<TransactionResponseDto>>>> GetHistory(Guid accountId)
    {
        var history = await _transactionService.GetHistoryAsync(accountId);
        return Success(history, "Hesap hareketleri başarıyla listelendi.");
    }
}
