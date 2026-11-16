using FluentValidation;
using MiniBank.DTOs;

namespace MiniBank.Validators;

public class CreateCustomerValidator : AbstractValidator<CreateCustomerDto>
{
    public CreateCustomerValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("Müşteri adı boş bırakılamaz.")
            .MaximumLength(50).WithMessage("Müşteri adı en fazla 50 karakter olabilir.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Müşteri soyadı boş bırakılamaz.")
            .MaximumLength(50).WithMessage("Müşteri soyadı en fazla 50 karakter olabilir.");

        RuleFor(x => x.IdentityNumber)
            .NotEmpty().WithMessage("T.C. Kimlik Numarası boş bırakılamaz.")
            .Length(11).WithMessage("T.C. Kimlik Numarası tam olarak 11 haneli olmalıdır.")
            .Matches("^[0-9]+$").WithMessage("T.C. Kimlik Numarası yalnızca rakamlardan oluşmalıdır.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("E-posta adresi boş bırakılamaz.")
            .EmailAddress().WithMessage("Geçerli bir e-posta adresi giriniz.");

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Telefon numarası boş bırakılamaz.")
            .Matches(@"^[0-9\+\s\-]+$").WithMessage("Geçerli bir telefon formatı giriniz.");
    }
}

