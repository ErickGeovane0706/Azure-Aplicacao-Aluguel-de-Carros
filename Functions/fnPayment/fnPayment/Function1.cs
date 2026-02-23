using System;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Azure.Functions.Worker.Extensions.CosmosDB; // Biblioteca do Cosmos adicionada

namespace fnPayment
{
    public class ProcessaPagamento
    {
        private readonly ILogger<ProcessaPagamento> _logger;
        private readonly IConfiguration _configuration;
        private readonly string[] StatusList = { "Aprovado", "Reprovado", "Em análise" };
        private readonly Random random = new Random();

        public ProcessaPagamento(ILogger<ProcessaPagamento> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        [Function(nameof(ProcessaPagamento))]
        [CosmosDBOutput("%CosmosDb%", "%CosmosContainerOut%", Connection = "CosmosDBConnection", CreateIfNotExists = true)]
        public async Task<object?> Run(
            [ServiceBusTrigger("queue-payments", Connection = "ServiceBusConnection")]
            ServiceBusReceivedMessage message,
            ServiceBusMessageActions messageActions)
        {
            _logger.LogInformation("Processando pagamento. ID da Mensagem: {id}", message.MessageId);

            PaymentModel pagamento = null; // Nome corrigido para PaymentModel
            try
            {
                // Converte o JSON da fila para a nossa classe
                pagamento = JsonSerializer.Deserialize<PaymentModel>(message.Body.ToString(), new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (pagamento == null)
                {
                    await messageActions.DeadLetterMessageAsync(message, null, "Mensagem inválida");
                    return null;
                }

                // Sorteio do Status
                int index = random.Next(StatusList.Length);
                string status = StatusList[index];
                pagamento.status = status;

                // Se aprovado, manda para a terceira fila
                if (status == "Aprovado")
                {
                    pagamento.dataAprovacao = DateTime.UtcNow;
                    await EnviarParaNotificacao(pagamento);
                }

                _logger.LogInformation("Pagamento {status} para o cliente {nome}", status, pagamento.nome);

                // Salva no Cosmos DB
                return pagamento;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro interno.");
                await messageActions.DeadLetterMessageAsync(message, null, "Falha no processamento.");
                return null;
            }
            finally
            {
                // Remove a mensagem da fila
                await messageActions.CompleteMessageAsync(message);
            }
        }

        private async Task EnviarParaNotificacao(PaymentModel pagamento)
        {
            // Conecta na terceira fila para avisar por email
            var connection = _configuration["ServiceBusConnection"];
            var queue = "queue-notifications";

            await using var client = new ServiceBusClient(connection);
            ServiceBusSender sender = client.CreateSender(queue);

            var message = new ServiceBusMessage(JsonSerializer.Serialize(pagamento));
            await sender.SendMessageAsync(message);
        }
    }
}