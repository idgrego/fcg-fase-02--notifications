namespace FCG02.NotificationsAPI.API.Models;

public class ConsoleEmailSender : IEmailSender
{
    private readonly ILogger<ConsoleEmailSender> _logger;
    public ConsoleEmailSender(ILogger<ConsoleEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendEmailAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "\n======================================================\n" +
            "📧 [SIMULAÇÃO DE ENVIO DE E-MAIL]\n" +
            "Para:       {To}\n" +
            "Assunto:    {Subject}\n" +
            "Data/Hora:  {SentAt:yyyy-MM-dd HH:mm:ss} UTC\n" +
            "Mensagem:\n{Body}\n" +
            "======================================================",
            message.To,
            message.Subject,
            message.SentAt,
            message.Body
        );

        return Task.CompletedTask;
    }
}