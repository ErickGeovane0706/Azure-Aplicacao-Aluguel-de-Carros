using System;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;

namespace fnSBRentProcess;

public class ProcessaLocacao
{
    private readonly ILogger<ProcessaLocacao> _logger;
    private readonly IConfiguration _configuration;

    public ProcessaLocacao(ILogger<ProcessaLocacao> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    [Function(nameof(ProcessaLocacao))]
    public async Task Run(
        [ServiceBusTrigger("queue-locacoes", Connection = "ServiceBusConnection")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions)
    {
        _logger.LogInformation("Message ID: {id}", message.MessageId);

        var body = message.Body.ToString();
        _logger.LogInformation("Message Body: {body}", body);
        _logger.LogInformation("Message Content-Type: {contentType}", message.ContentType);

        RentModel? rentModel = null;

        try
        {
            rentModel = JsonSerializer.Deserialize<RentModel>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (rentModel is null)
            {
                _logger.LogError("Mensagem mal formatada ou nula.");
                await messageActions.DeadLetterMessageAsync(
                    message,
                    deadLetterReason: "MensagemInvalida",
                    deadLetterErrorDescription: "O JSON não pôde ser convertido para RentModel"
                );
                return;
            }

            string? connectionString = _configuration["SqlConnectionString"];
            using var connection = new SqlConnection(connectionString);

            await connection.OpenAsync();
            _logger.LogInformation("Conexão com o banco de dados Azure SQL aberta com sucesso!");

            string query = @"INSERT INTO Locacao (Nome, Email, Modelo, Ano, TempoAluguel, Data) 
                             VALUES (@Nome, @Email, @Modelo, @Ano, @TempoAluguel, @Data)";

            using var command = new SqlCommand(query, connection);

            command.Parameters.AddWithValue("@Nome", rentModel.nome);
            command.Parameters.AddWithValue("@Email", rentModel.email);
            command.Parameters.AddWithValue("@Modelo", rentModel.modelo);
            command.Parameters.AddWithValue("@Ano", rentModel.ano);
            command.Parameters.AddWithValue("@TempoAluguel", rentModel.tempoAluguel);
            command.Parameters.AddWithValue("@Data", rentModel.data);

            await command.ExecuteNonQueryAsync();
            _logger.LogInformation("Dados inseridos na tabela Locacao com sucesso!");

            // --- INÍCIO DO CÓDIGO NOVO (PASSA O BASTÃO PARA A PRÓXIMA FILA) ---
            string? sbConn = _configuration["ServiceBusConnection"];
            if (!string.IsNullOrEmpty(sbConn))
            {
                await using var clientBus = new ServiceBusClient(sbConn);
                ServiceBusSender sender = clientBus.CreateSender("queue-payments");

                string jsonPagamento = JsonSerializer.Serialize(rentModel);
                await sender.SendMessageAsync(new ServiceBusMessage(jsonPagamento));

                _logger.LogInformation("Mensagem repassada para a queue-payments com sucesso!");
            }
            else
            {
                _logger.LogWarning("ServiceBusConnection não encontrada. Não foi possível enviar para a fila de pagamentos.");
            }
            // --- FIM DO CÓDIGO NOVO ---

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao processar a mensagem ou conectar ao banco.");
            await messageActions.AbandonMessageAsync(message);
            return;
        }

        await messageActions.CompleteMessageAsync(message);
        _logger.LogInformation("Mensagem processada e removida da fila inicial com sucesso!");
    }
}