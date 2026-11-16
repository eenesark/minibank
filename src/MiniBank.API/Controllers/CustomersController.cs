using Microsoft.AspNetCore.Mvc;
using MiniBank.Common;
using MiniBank.DTOs;
using MiniBank.Services;

namespace MiniBank.Controllers;

public class CustomersController : BaseApiController
{
    private readonly ICustomerService _customerService;

    public CustomersController(ICustomerService customerService)
    {
        _customerService = customerService;
    }

    /// <summary>
    /// Tüm kayıtlı müşterileri listeler
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IEnumerable<CustomerResponseDto>>>> GetAll()
    {
        var customers = await _customerService.GetAllAsync();
        return Success(customers, "Müşteri listesi başarıyla getirildi.");
    }

    /// <summary>
    /// Id'ye göre müşteri detayını getirir
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<CustomerResponseDto>>> GetById(Guid id)
    {
        var customer = await _customerService.GetByIdAsync(id);
        if (customer is null)
        {
            return ErrorNotFound<CustomerResponseDto>("Müşteri bulunamadı.");
        }

        return Success(customer, "Müşteri detayları getirildi.");
    }

    /// <summary>
    /// Yeni müşteri kaydı oluşturur
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<CustomerResponseDto>>> Create([FromBody] CreateCustomerDto dto)
    {
        try
        {
            var created = await _customerService.CreateAsync(dto);
            return CreatedSuccess(created, "Yeni müşteri kaydı başarıyla tamamlandı.");
        }
        catch (InvalidOperationException ex)
        {
            return ErrorBadRequest<CustomerResponseDto>(ex.Message);
        }
    }

    /// <summary>
    /// Var olan bir müşterinin bilgilerini (CustomerNumber, Ad, Soyad vb.) manuel günceller
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<CustomerResponseDto>>> Update(Guid id, [FromBody] UpdateCustomerDto dto)
    {
        try
        {
            var updated = await _customerService.UpdateAsync(id, dto);
            return Success(updated, "Müşteri bilgileri başarıyla güncellendi.");
        }
        catch (KeyNotFoundException ex)
        {
            return ErrorNotFound<CustomerResponseDto>(ex.Message);
        }
    }
}
