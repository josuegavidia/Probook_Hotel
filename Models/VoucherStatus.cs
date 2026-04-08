namespace Proyecto_Progra_Web.API.Models
{
    /// <summary>
    /// VoucherStatus define los estados posibles de un voucher de reserva.
    /// </summary>
    public enum VoucherStatus
    {
        /// <summary>
        /// Reserva creada, esperando que el cliente suba el voucher.
        /// </summary>
        Pending = 0,

        /// <summary>
        /// Admin revisó y aprobó el voucher. Reserva confirmada.
        /// </summary>
        Approved = 1,

        /// <summary>
        /// Admin revisó y rechazó el voucher. Cliente debe reintentarlo.
        /// </summary>
        Rejected = 2
    }
}