using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MiniBank.Data;
using MiniBank.DTOs;
using MiniBank.Entities;
using MiniBank.Services.Generators;

namespace MiniBank.Services;

public class CardService : ICardService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBankingCodeGenerator _codeGenerator;
    private readonly ILogger<CardService> _logger;

    public CardService(
        IUnitOfWork unitOfWork,
        IBankingCodeGenerator codeGenerator,
        ILogger<CardService> logger)
    {
        _unitOfWork = unitOfWork;
        _codeGenerator = codeGenerator;
        _logger = logger;
    }

    public async Task<IEnumerable<CardResponseDto>> GetAllAsync()
    {
        var cards = await _unitOfWork.Cards.Query()
            .Include(c => c.Customer)
            .Include(c => c.Account)
            .Where(c => c.Account == null || c.Account.AccountType == "Demand")
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        return cards.Select(MapToDto);
    }

    public async Task<IEnumerable<CardResponseDto>> GetByCustomerIdAsync(Guid customerId)
    {
        var cards = await _unitOfWork.Cards.Query()
            .Include(c => c.Customer)
            .Include(c => c.Account)
            .Where(c => c.CustomerId == customerId)
            .Where(c => c.CustomerId == customerId && (c.Account == null || c.Account.AccountType == "Demand"))
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        return cards.Select(MapToDto);
    }

    public async Task<IEnumerable<CardResponseDto>> GetByAccountIdAsync(Guid accountId)
    {
        var cards = await _unitOfWork.Cards.Query()
            .Include(c => c.Customer)
            .Include(c => c.Account)
            .Where(c => c.AccountId == accountId)
            .Where(c => c.AccountId == accountId && (c.Account == null || c.Account.AccountType == "Demand"))
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        return cards.Select(MapToDto);
    }

    public async Task<CardResponseDto?> GetByIdAsync(Guid cardId, Guid? requestingCustomerId = null)
    {
        var card = await _unitOfWork.Cards.Query()
            .Include(c => c.Customer)
            .Include(c => c.Account)
            .FirstOrDefaultAsync(c => c.Id == cardId);

        if (card is null) return null;

        if (requestingCustomerId.HasValue && card.CustomerId != requestingCustomerId.Value)
        {
            throw new UnauthorizedAccessException("Bu karta erişim yetkiniz bulunmamaktadır.");
        }

        return MapToDto(card);
    }

    public async Task<CardResponseDto> CreateCardAsync(Guid customerId, CreateCardDto dto)
    {
        var customer = await _unitOfWork.Customers.GetByIdAsync(customerId);
        if (customer is null)
        {
            throw new KeyNotFoundException("Müşteri bulunamadı.");
        }

        var account = await _unitOfWork.Accounts.GetByIdAsync(dto.AccountId);
        if (account is null)
        {
            throw new KeyNotFoundException("Kartın bağlanacağı banka hesabı bulunamadı.");
        }

        if (account.CustomerId != customerId)
        {
            throw new InvalidOperationException("Yalnızca kendinize ait hesaplar için kart çıkartabilirsiniz.");
        }

        if (!account.IsActive)
        {
            throw new InvalidOperationException("Pasif durumdaki bir hesap için kart açılamaz.");
        }

        if (!string.Equals(account.AccountType, "Demand", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Banka kartı (Debit kart) yalnızca Vadesiz (Demand) hesaplar için çıkartılabilir. Vadeli mevduat hesaplarına kart tanımlanamaz.");
        }

        if (!string.Equals(account.Currency, "TRY", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(account.Currency, "TL", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Banka kartı yalnızca Türk Lirası (TRY) hesapları için çıkartılabilir. Döviz, kripto ve altın hesaplarına kart tanımlanamaz.");
        }

        var (expiryMonth, expiryYear) = _codeGenerator.GenerateExpiryDate(4);
        var cardNumber = _codeGenerator.GenerateCardNumber();
        var cvv = _codeGenerator.GenerateCvv();
        var cardHolder = $"{customer.FirstName} {customer.LastName}".ToUpperInvariant();

        var card = new Card
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            AccountId = account.Id,
            CardNumber = cardNumber,
            CardHolderName = cardHolder,
            ExpiryMonth = expiryMonth,
            ExpiryYear = expiryYear,
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

        await _unitOfWork.Cards.AddAsync(card);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Yeni kart oluşturuldu: {MaskedCardNumber} Müşteri: {Customer}", 
            MaskCardNumber(cardNumber), cardHolder);

        // Map için ilişkileri yükleyelim
        card.Customer = customer;
        card.Account = account;

        return MapToDto(card);
    }

    public async Task<CardResponseDto> UpdateCardSettingsAsync(Guid cardId, UpdateCardSettingsDto dto, Guid? requestingCustomerId = null)
    {
        var card = await _unitOfWork.Cards.Query()
            .Include(c => c.Customer)
            .Include(c => c.Account)
            .FirstOrDefaultAsync(c => c.Id == cardId);

        if (card is null)
        {
            throw new KeyNotFoundException("Kart bulunamadı.");
        }

        if (requestingCustomerId.HasValue && card.CustomerId != requestingCustomerId.Value)
        {
            throw new UnauthorizedAccessException("Bu kartın ayarlarını güncelleme yetkiniz bulunmamaktadır.");
        }

        if (dto.IsInternationalAllowed.HasValue)
            card.IsInternationalAllowed = dto.IsInternationalAllowed.Value;

        if (dto.IsOnlineShoppingAllowed.HasValue)
            card.IsOnlineShoppingAllowed = dto.IsOnlineShoppingAllowed.Value;

        if (dto.IsContactlessAllowed.HasValue)
            card.IsContactlessAllowed = dto.IsContactlessAllowed.Value;

        if (dto.DailyLimit.HasValue)
        {
            if (dto.DailyLimit.Value < 0)
                throw new InvalidOperationException("Günlük işlem limiti 0'dan küçük olamaz.");
            card.DailyLimit = dto.DailyLimit.Value;
        }

        if (dto.DailyAtmWithdrawLimit.HasValue)
        {
            if (dto.DailyAtmWithdrawLimit.Value < 0)
                throw new InvalidOperationException("Günlük ATM para çekme limiti 0'dan küçük olamaz.");
            card.DailyAtmWithdrawLimit = dto.DailyAtmWithdrawLimit.Value;
        }

        card.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.Cards.Update(card);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Kart ayarları güncellendi: {CardId}", card.Id);
        return MapToDto(card);
    }

    public async Task<CardResponseDto> ToggleCardStatusAsync(Guid cardId, Guid? requestingCustomerId = null)
    {
        var card = await _unitOfWork.Cards.Query()
            .Include(c => c.Customer)
            .Include(c => c.Account)
            .FirstOrDefaultAsync(c => c.Id == cardId);

        if (card is null)
        {
            throw new KeyNotFoundException("Kart bulunamadı.");
        }

        if (requestingCustomerId.HasValue && card.CustomerId != requestingCustomerId.Value)
        {
            throw new UnauthorizedAccessException("Bu kartın durumunu değiştirme yetkiniz bulunmamaktadır.");
        }

        if (card.Status == CardStatus.Active)
        {
            card.Status = CardStatus.Blocked;
        }
        else if (card.Status == CardStatus.Blocked)
        {
            card.Status = CardStatus.Active;
        }
        else
        {
            throw new InvalidOperationException("İptal edilmiş kartlar yeniden aktif edilemez.");
        }

        card.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.Cards.Update(card);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Kart durumu güncellendi: {CardId} Yeni Durum: {Status}", card.Id, card.Status);
        return MapToDto(card);
    }

    public async Task<int> EnsureAllAccountsHaveCardsAsync()
    {
        // Kartı olmayan yalnızca aktif TL/TRY hesaplarını bul
        // Kartı olmayan yalnızca aktif Vadesiz (Demand) TL/TRY hesaplarını bul
        var accountsWithoutCard = await _unitOfWork.Accounts.Query()
            .Include(a => a.Customer)
            .Include(a => a.Cards)
            .Where(a => a.IsActive && (a.Currency == "TRY" || a.Currency == "TL") && !a.Cards.Any())
            .Where(a => a.IsActive && a.AccountType == "Demand" && (a.Currency == "TRY" || a.Currency == "TL") && !a.Cards.Any())
            .ToListAsync();

        if (!accountsWithoutCard.Any()) return 0;

        int createdCount = 0;
        foreach (var account in accountsWithoutCard)
        {
            var customer = account.Customer;
            if (customer is null) continue;

            var (expiryMonth, expiryYear) = _codeGenerator.GenerateExpiryDate(4);
            var cardNumber = _codeGenerator.GenerateCardNumber();
            var cvv = _codeGenerator.GenerateCvv();
            var cardHolder = $"{customer.FirstName} {customer.LastName}".ToUpperInvariant();

            var card = new Card
            {
                Id = Guid.NewGuid(),
                CustomerId = customer.Id,
                AccountId = account.Id,
                CardNumber = cardNumber,
                CardHolderName = cardHolder,
                ExpiryMonth = expiryMonth,
                ExpiryYear = expiryYear,
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

            await _unitOfWork.Cards.AddAsync(card);
            createdCount++;
        }

        if (createdCount > 0)
        {
            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("{Count} adet hesaba otomatik yeni banka kartı tanımlandı.", createdCount);
        }

        return createdCount;
    }

    private static CardResponseDto MapToDto(Card c)
    {
        var expiryFormatted = $"{c.ExpiryMonth:D2}/{c.ExpiryYear % 100:D2}";
        var customerFullName = c.Customer != null ? $"{c.Customer.FirstName} {c.Customer.LastName}".Trim() : c.CardHolderName;
        var accountBalance = c.Account != null ? c.Account.Balance : 0m;
        var accountNumber = c.Account != null ? c.Account.AccountNumber : string.Empty;
        var iban = c.Account != null ? c.Account.Iban : string.Empty;
        var accountType = c.Account != null ? c.Account.AccountType : "Demand";
        var currency = c.Account != null ? c.Account.Currency : "TRY";

        return new CardResponseDto(
            c.Id,
            c.CustomerId,
            customerFullName,
            c.AccountId,
            accountNumber,
            iban,
            iban,
            accountType,
            currency,
            accountBalance,
            FormatCardNumber(c.CardNumber),
            MaskCardNumber(c.CardNumber),
            c.CardHolderName,
            c.ExpiryMonth,
            c.ExpiryYear,
            expiryFormatted,
            c.Cvv,
            c.CardType,
            c.Status.ToString(),
            c.IsInternationalAllowed,
            c.IsOnlineShoppingAllowed,
            c.IsContactlessAllowed,
            c.DailyLimit,
            c.DailyAtmWithdrawLimit,
            c.CreatedAt
        );
    }

    private static string FormatCardNumber(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (digits.Length == 16)
        {
            return $"{digits[..4]} {digits.Substring(4, 4)} {digits.Substring(8, 4)} {digits.Substring(12, 4)}";
        }
        return raw;
    }

    private static string MaskCardNumber(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (digits.Length == 16)
        {
            return $"{digits[..4]} **** **** {digits.Substring(12, 4)}";
        }
        return "**** **** **** ****";
    }
}

