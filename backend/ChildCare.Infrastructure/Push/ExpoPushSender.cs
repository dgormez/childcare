using System.Net.Http.Json;
using ChildCare.Application.Common;

namespace ChildCare.Infrastructure.Push;

/// <summary>Thin client over Expo's HTTP Push Notification API (constitution's Technology Stack Constraints).</summary>
public class ExpoPushSender(IHttpClientFactory httpClientFactory) : IExpoPushSender
{
    private const string PushEndpoint = "https://exp.host/--/api/v2/push/send";

    public async Task SendAsync(string pushToken, string title, string body, CancellationToken cancellationToken = default)
    {
        var http = httpClientFactory.CreateClient();
        var response = await http.PostAsJsonAsync(
            PushEndpoint,
            new { to = pushToken, title, body },
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
