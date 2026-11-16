using FluentValidation;
using MiniBank.DTOs;

namespace MiniBank.Validators;

public class LoginValidator : AbstractValidator<LoginDto>
{
    public LoginValidator()
    {
        RuleFor(x => x.IdentityNumberOrUsername)
            .NotEmpty().WithMessage("T.C. Kimlik Numarası veya Kullanıcı Adı boş bırakılamaz.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Şifre boş bırakılamaz.");
    }
}

public class RegisterValidator : AbstractValidator<RegisterDto>
{
    public RegisterValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("Ad boş bırakılamaz.")
            .MaximumLength(50).WithMessage("Ad en fazla 50 karakter olabilir.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Soyad boş bırakılamaz.")
            .MaximumLength(50).WithMessage("Soyad en fazla 50 karakter olabilir.");

        RuleFor(x => x.IdentityNumber)
            .NotEmpty().WithMessage("T.C. Kimlik Numarası boş bırakılamaz.")
            .Length(11).WithMessage("T.C. Kimlik Numarası 11 haneli olmalıdır.")
            .Matches("^[0-9]+$").WithMessage("T.C. Kimlik Numarası yalnızca rakamlardan oluşmalıdır.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("E-posta adresi boş bırakılamaz.")
            .EmailAddress().WithMessage("Geçerli bir e-posta adresi giriniz.");

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Telefon numarası boş bırakılamaz.")
            .Matches(@"^[0-9\+\s\-]+$").WithMessage("Geçerli bir telefon formatı giriniz.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Şifre boş bırakılamaz.")
            .MinimumLength(6).WithMessage("Şifre en az 6 karakter olmalıdır.");
    }
}

public class ForgotPasswordValidator : AbstractValidator<ForgotPasswordDto>
{
    public ForgotPasswordValidator()
    {
        RuleFor(x => x.IdentityNumber)
            .NotEmpty().WithMessage("T.C. Kimlik Numarası boş bırakılamaz.")
            .Length(11).WithMessage("T.C. Kimlik Numarası 11 haneli olmalıdır.")
            .Matches("^[0-9]+$").WithMessage("T.C. Kimlik Numarası yalnızca rakamlardan oluşmalıdır.");

        RuleFor(x => x.EmailOrPhone)
            .NotEmpty().WithMessage("Sistemde kayıtlı E-posta veya Telefon bilginizi giriniz.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Yeni şifre boş bırakılamaz.")
            .MinimumLength(6).WithMessage("Yeni şifre en az 6 karakter olmalıdır.");
    }
}

public class UpdateProfileValidator : AbstractValidator<UpdateProfileDto>
{
    public UpdateProfileValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("Ad boş bırakılamaz.")
            .MaximumLength(50).WithMessage("Ad en fazla 50 karakter olabilir.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Soyad boş bırakılamaz.")
            .MaximumLength(50).WithMessage("Soyad en fazla 50 karakter olabilir.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("E-posta adresi boş bırakılamaz.")
            .EmailAddress().WithMessage("Geçerli bir e-posta adresi giriniz.");

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Telefon numarası boş bırakılamaz.")
            .Matches(@"^[0-9\+\s\-]+$").WithMessage("Geçerli bir telefon formatı giriniz.");

        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Güvenlik doğrulaması için mevcut şifrenizi girmeniz zorunludur.");

        When(x => !string.IsNullOrWhiteSpace(x.NewPassword), () =>
        {
            RuleFor(x => x.NewPassword)
                .MinimumLength(6).WithMessage("Yeni şifre en az 6 karakter olmalıdır.");
        });
    }
}

