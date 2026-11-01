namespace FCG02.NotificationsAPI.API.Models;

public record UserCreatedIntegrationEvent
(
    Guid EventId,
    DateTime OccurredOn,
    Guid UserId,
    string Name,
    string Email,
    string Role
);