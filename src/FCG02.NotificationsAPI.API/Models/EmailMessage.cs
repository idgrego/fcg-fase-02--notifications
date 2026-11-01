namespace FCG02.NotificationsAPI.API.Models;

public record EmailMessage(
    string To,
    string Subject,
    string Body,
    DateTime SentAt
);