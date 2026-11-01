using System.Text;
using System.Text.Json;
using FCG02.NotificationsAPI.API.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace FCG02.NotificationsAPI.API.Services;

public class RabbitMqConsumerService : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<RabbitMqConsumerService> _logger;
    private IConnection? _connection;
    private IChannel? _channel;

    public RabbitMqConsumerService(
        IConfiguration configuration,
        IEmailSender emailSender,
        ILogger<RabbitMqConsumerService> logger
    )
    {
        this._configuration = configuration;
        this._emailSender = emailSender;
        this._logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var hostName = _configuration["RabbitMQ:HostName"] ?? "localhost";
        var port = int.Parse(_configuration["RabbitMQ:Port"] ?? "5672");
        var userName = _configuration["RabbitMQ:UserName"] ?? "guest";
        var password = _configuration["RabbitMQ:Password"] ?? "guest";
        var exchange = _configuration["RabbitMQ:Exchange"] ?? "fcg.users.events";
        var queue = _configuration["RabbitMQ:Queue"] ?? "notifications.user-created";
        var routingKey = _configuration["RabbitMQ:RoutingKey"] ?? "user.created";

        _logger.LogInformation("⏳ Conectando o consumidor ao RabbitMQ em {Host}:{Port}...", hostName, port);

        var factory = new ConnectionFactory
        {
            HostName = hostName,
            Port = port,
            UserName = userName,
            Password = password,
            AutomaticRecoveryEnabled = true
        };

        // 🔄 LOOP DE RESILIÊNCIA: Tenta conectar até o RabbitMQ ficar pronto
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                _connection = await factory.CreateConnectionAsync(cancellationToken);
                _channel = await _connection.CreateChannelAsync(null, cancellationToken);

                // garante que a EXCHANGE existe (tópico e durável)
                await _channel.ExchangeDeclareAsync(
                    exchange: exchange,
                    type: ExchangeType.Topic,
                    durable: true,
                    autoDelete: false,
                    cancellationToken: cancellationToken
                );

                // garante que a FILA existe
                await _channel.QueueDeclareAsync(
                    queue: queue,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null,
                    cancellationToken: cancellationToken
                );

                // Vincula a fila à exchange com routing key desejada
                await _channel.QueueBindAsync(
                    queue: queue,
                    exchange: exchange,
                    routingKey: routingKey,
                    cancellationToken: cancellationToken
                );

                // Configura o consumidor assíncrono com confirmação manual
                var consumer = new AsyncEventingBasicConsumer(_channel);
                consumer.ReceivedAsync += this.ReceivedAyncs;

                await _channel.BasicConsumeAsync(
                    queue: queue,
                    autoAck: false, // confirmação manual
                    consumer: consumer,
                    cancellationToken: cancellationToken
                );

                _logger.LogInformation("✅ Consumidor do RabbitMQ ativo e aguardando eventos na fila '{Queue}'...", queue);

                // Conexão e binding concluídos com sucesso! Sai do loop de tentativas
                break;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("🛑 Encerrando o consumidor do RabbitMQ...");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ RabbitMQ ainda indisponível em {Host}:{Port}. Nova tentativa em segundos...", hostName, port);
                try
                {
                    await Task.Delay(5000, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        // Mantém o BackgroundService ativo escutando mensagens até o encerramento do Pod
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("🛑 Encerrando o consumidor do RabbitMQ...");
        }
    }

    private async Task ReceivedAyncs(object sender, BasicDeliverEventArgs ea)
    {
        var body = ea.Body.ToArray();
        var messageJson = Encoding.UTF8.GetString(body);
        try
        {
            _logger.LogInformation("📩 Mensagem recebida da fila '{Queue}'. RoutingKey: {RoutingKey}", ea.Exchange, ea.RoutingKey);

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var userCreatedEvent = JsonSerializer.Deserialize<UserCreatedIntegrationEvent>(messageJson, options);
            if (userCreatedEvent is not null)
            {
                var email = new EmailMessage(
                    To: userCreatedEvent.Email,
                    Subject: $"Bem-vindo ao Fiap Cloud Games {userCreatedEvent.Name}!",
                    Body: $"Olá, {userCreatedEvent.Name}!\n\nSua conta com perfil '{userCreatedEvent.Role}' foi criada com sucesso.\nAproveite o nosso catálogo de jogos em nuvem!",
                    SentAt: DateTime.UtcNow
                );

                await _emailSender.SendEmailAsync(email, ea.CancellationToken);

                // confirma o processamento com sucesso
                await _channel!.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false, ea.CancellationToken);
            }

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao processar mensagem do RabbitMQ: {Payload}", messageJson);
            // Rejeita a mensagem sem requeue imediato para evitar loop infinito
            await _channel!.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
        }

    }

    public override void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
    }
}