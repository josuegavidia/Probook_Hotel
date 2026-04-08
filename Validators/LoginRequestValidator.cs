using FluentValidation;
using Proyecto_Progra_Web.API.DTOs;

namespace Proyecto_Progra_Web.API.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El email es requerido")
            .EmailAddress().WithMessage("El email debe ser válido")
            .MaximumLength(255).WithMessage("El email no debe exceder 255 caracteres");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("La contraseña es requerida")
            .MinimumLength(6).WithMessage("La contraseña debe tener al menos 6 caracteres")
            .MaximumLength(100).WithMessage("La contraseña no debe exceder 100 caracteres");
    }
}