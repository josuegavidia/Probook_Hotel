using FluentValidation;
using Proyecto_Progra_Web.API.DTOs;

namespace Proyecto_Progra_Web.API.Validators;

public class ReservationRequestValidator : AbstractValidator<ReservationRequest>
{
    public ReservationRequestValidator()
    {
        RuleFor(x => x.RoomNumber)
            .NotEmpty().WithMessage("El número de habitación es requerido")
            .MaximumLength(10).WithMessage("El número de habitación no debe exceder 10 caracteres");

        RuleFor(x => x.CheckInDate)
            .NotEmpty().WithMessage("La fecha de entrada es requerida")
            .GreaterThanOrEqualTo(DateTime.Today).WithMessage("La fecha de entrada no puede ser en el pasado");

        RuleFor(x => x.CheckOutDate)
            .NotEmpty().WithMessage("La fecha de salida es requerida")
            .GreaterThan(x => x.CheckInDate).WithMessage("La fecha de salida debe ser después de la entrada");

        RuleFor(x => x.GuestName)
            .NotEmpty().WithMessage("El nombre del huésped es requerido")
            .MaximumLength(150).WithMessage("El nombre no debe exceder 150 caracteres")
            .Matches(@"^[a-zA-Z\s]+$").WithMessage("El nombre solo debe contener letras");

        RuleFor(x => x.GuestEmail)
            .NotEmpty().WithMessage("El email del huésped es requerido")
            .EmailAddress().WithMessage("El email debe ser válido");

        RuleFor(x => x.NumberOfGuests)
            .GreaterThan(0).WithMessage("Debe haber al menos 1 huésped")
            .LessThanOrEqualTo(10).WithMessage("No pueden haber más de 10 huéspedes por habitación");

        RuleFor(x => x.SpecialRequests)
            .MaximumLength(500).WithMessage("Los comentarios no deben exceder 500 caracteres");
    }
}