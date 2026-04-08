using FluentValidation;
using Proyecto_Progra_Web.API.DTOs;

namespace Proyecto_Progra_Web.API.Validators;

public class VoucherUploadValidator : AbstractValidator<VoucherUploadRequest>
{
    public VoucherUploadValidator()
    {
        RuleFor(x => x.File)
            .NotNull().WithMessage("El archivo es requerido");

        RuleFor(x => x.ReservationId)
            .NotEmpty().WithMessage("El ID de reserva es requerido");

        RuleFor(x => x.VoucherType)
            .NotEmpty().WithMessage("El tipo de voucher es requerido")
            .Must(x => new[] { "PAYMENT", "CONFIRMATION", "RECEIPT" }.Contains(x))
            .WithMessage("El tipo de voucher debe ser: PAYMENT, CONFIRMATION o RECEIPT");
    }
}