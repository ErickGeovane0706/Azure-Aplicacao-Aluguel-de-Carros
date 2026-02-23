using System;

namespace fnPayment
{
    public class PaymentModel
    {
        // O Cosmos DB exige um campo "id" minúsculo para gerar o registro
        public string id { get; set; } = Guid.NewGuid().ToString();

        // Colocamos o "?" para o C# parar de reclamar de valores nulos
        public string? nome { get; set; }
        public string? email { get; set; }
        public string? modelo { get; set; }
        public decimal valor { get; set; }

        public string? status { get; set; }
        public DateTime? dataAprovacao { get; set; }
    }
}