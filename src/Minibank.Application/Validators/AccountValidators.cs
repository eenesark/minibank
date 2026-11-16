using FluentValidation;
using MiniBank.DTOs;

namespace MiniBank.Validators;

public class CreateAccountValidator : AbstractValidator<CreateAccountDto>
{
    private static readonly string[] AllowedCurrencies = { "TRY", "USD", "EUR", "GBP", "XAU" };

    public CreateAccountValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage("Geçerli bir müşteri seçilmelidir.");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("Para birimi belirtilmelidir.")
            .Must(c => AllowedCurrencies.Contains(c.ToUpper()))
            .WithMessage("Desteklenen para birimleri: TRY, USD, EUR, GBP, XAU (Gram Altın).");

        RuleFor(x => x.InitialDeposit)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Açılış bakiyesi negatif olamaz.");
    }
}

