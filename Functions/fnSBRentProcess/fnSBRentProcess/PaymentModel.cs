using System;
namespace fnSBRentProcess;

public class PaymentModel
{
    public string? LocacaoId { get; set; }
    public string? Cliente { get; set; }
    public decimal Valor { get; set; }
    public DateTime DataSolicitacao { get; set; }
}
