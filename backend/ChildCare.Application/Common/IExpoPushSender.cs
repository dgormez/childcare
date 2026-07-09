namespace ChildCare.Application.Common;

/// <summary>Thin port over the Expo Push Notification Service HTTP API (constitution's Technology Stack Constraints).</summary>
public interface IExpoPushSender
{
    Task SendAsync(string pushToken, string title, string body, CancellationToken cancellationToken = default);
}
