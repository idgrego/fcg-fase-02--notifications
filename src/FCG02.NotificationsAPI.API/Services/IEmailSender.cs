namespace FCG02.NotificationsAPI.API.Models;

public interface IEmailSender
{
    Task SendEmailAsync(EmailMessage message, CancellationToken cancellationToken = default);
}