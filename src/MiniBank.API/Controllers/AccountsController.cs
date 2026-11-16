using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniBank.Common;
using MiniBank.DTOs;
using MiniBank.Services;

namespace MiniBank.Controllers;

public class AccountsController : BaseApiController
{
    private readonly IAccountService _accountService;

    public AccountsController(IAccountService accountService)
    {
        _accountService = accountService;
    }

    /// <summary>
    /// Tüm banka hesaplarını listeler
    /// </summary>
    [HttpGet]
    [HttpGet("get-all")]
    public async Task<ActionResult<ApiResponse<IEnumerable<AccountResponseDto>>>> GetAll()
    {
        var accounts = await _accountService.GetAllAsync();
        return Success(accounts, "Tüm banka hesapları başarıyla listelendi.");
    }

    /// <summary>
    /// Hesap detayını getirir
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<AccountResponseDto>>> GetById(Guid id)
    {
        var account = await _accountService.GetByIdAsync(id);
        if (account is null)
        {
            return ErrorNotFound<AccountResponseDto>("Banka hesabı bulunamadı.");
        }

        return Success(account, "Hesap bilgisi başarıyla getirildi.");
    }

    /// <summary>
    /// Belirli bir müşterinin hesaplarını listeler
    /// </summary>
    [HttpGet("customer/{customerId:guid}")]
    [HttpGet("get-customer/{customerId:guid}")]
    public async Task<ActionResult<ApiResponse<IEnumerable<AccountResponseDto>>>> GetByCustomerId(Guid customerId)
    {
        var accounts = await _accountService.GetByCustomerIdAsync(customerId);
        return Success(accounts, "Müşteri hesapları başarıyla listelendi.");
    }

    /// <summary>
    /// Giriş yapan müşterinin hesaplarını menü seçimine veya kategorisine göre listeler (demand, currency, term-deposit, all)
    /// </summary>
    [Authorize]
    [HttpGet("my-accounts")]
    public async Task<ActionResult<ApiResponse<IEnumerable<AccountResponseDto>>>> GetMyAccounts([FromQuery] string? category = null)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return StatusCode(StatusCodes.Status401Unauthorized, ApiResponse<IEnumerable<AccountResponseDto>>.Fail("Oturum bilgisi doğrulanamadı.", 401));
        }

        var accounts = (await _accountService.GetByCustomerIdAsync(currentUserId)).ToList();

        if (!string.IsNullOrWhiteSpace(category))
        {
            var cleanCat = category.Trim().ToLowerInvariant();
            accounts = cleanCat switch
            {
                "demand" => accounts.Where(a => a.AccountType == "Demand" && (a.Currency == "TRY" || a.Currency == "TL")).ToList(),
                "currency" => accounts.Where(a => a.Currency != "TRY" && a.Currency != "TL").ToList(),
                "term-deposit" or "timedeposit" or "term" => accounts.Where(a => a.AccountType == "TimeDeposit").ToList(),
                _ => accounts
            };
        }

        return Success(accounts.AsEnumerable(), "Hesaplar başarıyla listelendi.");
    }

    /// <summary>
    /// Giriş yapan müşterinin ana ekranında tekil olarak gösterilecek birincil Vadesiz TL hesabını getirir
    /// </summary>
    [Authorize]
    [HttpGet("primary")]
    public async Task<ActionResult<ApiResponse<AccountResponseDto>>> GetMyPrimaryAccount()
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return StatusCode(StatusCodes.Status401Unauthorized, ApiResponse<AccountResponseDto>.Fail("Oturum bilgisi doğrulanamadı.", 401));
        }

        var accounts = (await _accountService.GetByCustomerIdAsync(currentUserId)).ToList();
        var primary = accounts.FirstOrDefault(a => a.IsActive && (a.Currency == "TRY" || a.Currency == "TL") && a.AccountType == "Demand")
            ?? accounts.FirstOrDefault(a => a.IsActive && (a.Currency == "TRY" || a.Currency == "TL"))
            ?? accounts.FirstOrDefault(a => a.IsActive);

        if (primary is null)
        {
            return ErrorNotFound<AccountResponseDto>("Kullanıcıya ait aktif vadesiz TL hesabı bulunamadı.");
        }

        return Success(primary, "Ana vadesiz TL hesabı başarıyla getirildi.");
    }

    /// <summary>
    /// Yeni banka hesabı (IBAN) açar
    /// </summary>
    [HttpPost]
    [HttpPost("create")]
    public async Task<ActionResult<ApiResponse<AccountResponseDto>>> Create([FromBody] CreateAccountDto dto)
    {
        try
        {
            var created = await _accountService.CreateAsync(dto);
            return CreatedSuccess(created, "Yeni banka hesabı başarıyla açıldı.");
        }
        catch (KeyNotFoundException ex)
        {
            return ErrorNotFound<AccountResponseDto>(ex.Message);
        }
    }

    /// <summary>
    /// Var olan bir banka hesabının bilgilerini (IBAN, Hesap No, Para Birimi vb.) manuel günceller
    /// </summary>
    [HttpPut("{id:guid}")]
    [HttpPut("update/{id:guid}")]
    public async Task<ActionResult<ApiResponse<AccountResponseDto>>> Update(Guid id, [FromBody] UpdateAccountDto dto)
    {
        try
        {
            var updated = await _accountService.UpdateAsync(id, dto);
            return Success(updated, "Hesap bilgileri başarıyla güncellendi.");
        }
        catch (KeyNotFoundException ex)
        {
            return ErrorNotFound<AccountResponseDto>(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return ErrorBadRequest<AccountResponseDto>(ex.Message);
        }
    }

    /// <summary>
    /// Giriş yapan müşterinin kendisine yeni bir döviz veya altın hesabı (USD, EUR, GBP, XAU) açmasını sağlar
    /// </summary>
    [Authorize]
    [HttpPost("open-currency-account")]
    public async Task<ActionResult<ApiResponse<AccountResponseDto>>> OpenCurrencyAccount([FromBody] OpenCurrencyAccountDto dto)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return StatusCode(StatusCodes.Status401Unauthorized, ApiResponse<AccountResponseDto>.Fail("Oturum bilgisi doğrulanamadı.", 401));
        }

        try
        {
            var created = await _accountService.CreateAsync(new CreateAccountDto(
                CustomerId: currentUserId,
                Currency: dto.Currency.ToUpper(),
                InitialDeposit: dto.InitialDeposit
            ));
            return CreatedSuccess(created, $"{dto.Currency.ToUpper()} hesabı başarıyla açıldı.");
        }
        catch (InvalidOperationException ex)
        {
            return ErrorBadRequest<AccountResponseDto>(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return ErrorNotFound<AccountResponseDto>(ex.Message);
        }
    }
}
