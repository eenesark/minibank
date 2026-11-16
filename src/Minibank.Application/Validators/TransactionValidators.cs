using FluentValidation;
using MiniBank.DTOs;

namespace MiniBank.Validators;

public class DepositValidator : AbstractValidator<DepositDto>
{
    public DepositValidator()
    {
        RuleFor(x => x.AccountId)
            .NotEmpty().WithMessage("Hesap ID belirtilmelidir.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Yatırılacak tutar 0'dan büyük olmalıdır.");
    }
}

public class WithdrawValidator : AbstractValidator<WithdrawDto>
{
    public WithdrawValidator()
    {
        RuleFor(x => x.AccountId)
            .NotEmpty().WithMessage("Hesap ID belirtilmelidir.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Çekilecek tutar 0'dan büyük olmalıdır.");
    }
}

public class TransferValidator : AbstractValidator<TransferDto>
{
    public TransferValidator()
    {
        RuleFor(x => x.FromAccountId)
            .NotEmpty().WithMessage("Gönderen hesap ID belirtilmelidir.");

        RuleFor(x => x.TargetIban)
            .NotEmpty().WithMessage("Alıcı IBAN boş olamaz.")
            .MinimumLength(10).WithMessage("Geçerli bir IBAN giriniz.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Transfer tutarı 0'dan büyük olmalıdır.");
    }
}

