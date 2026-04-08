using FluentValidation;
using Proyecto_Progra_Web.API.DTOs;

namespace Proyecto_Progra_Web.API.Validators;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El email es requerido")
            .EmailAddress().WithMessage("El email debe ser válido")
            .MaximumLength(255).WithMessage("El email no debe exceder 255 caracteres");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("La contraseña es requerida")
            .MinimumLength(8).WithMessage("La contraseña debe tener al menos 8 caracteres")
            .MaximumLength(100).WithMessage("La contraseña no debe exceder 100 caracteres")
            .Matches(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]+$")
            .WithMessage("La contraseña debe contener mayúscula, minúscula, número y símbolo especial");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("El nombre es requerido")
            .MaximumLength(150).WithMessage("El nombre no debe exceder 150 caracteres")
            .Matches(@"^[a-zA-Z\s]+$").WithMessage("El nombre solo debe contener letras y espacios");

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(20).WithMessage("El teléfono no debe exceder 20 caracteres")
            .Matches(@"^\+?[\d\s\-\(\)]+$").WithMessage("El teléfono tiene un formato inválido");
    }
}