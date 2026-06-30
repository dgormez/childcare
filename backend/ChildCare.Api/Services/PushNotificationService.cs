namespace ChildCare.Api.Services;

public class PushNotificationService(IHttpClientFactory httpClientFactory, ILogger<PushNotificationService> logger)
{
    private const string ExpoEndpoint = "https://exp.host/--/api/v2/push/send";

    public async Task SendAsync(string expoPushToken, string title, string body, object? data = null)
    {
        if (string.IsNullOrWhiteSpace(expoPushToken)) return;

        var payload = new
        {
            to    = expoPushToken,
            title,
            body,
            sound = "default",
            data  = data ?? new { },
        };

        var http = httpClientFactory.CreateClient();
        http.DefaultRequestHeaders.TryAddWithoutValidation("Accept",          "application/json");
        http.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Encoding", "gzip, deflate");

        try
        {
            var response = await http.PostAsJsonAsync(ExpoEndpoint, payload);
            if (!response.IsSuccessStatusCode)
                logger.LogWarning(
                    "Expo push notification failed ({Status}) for token {Token}",
                    response.StatusCode, expoPushToken[..Math.Min(20, expoPushToken.Length)]);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send push notification to {Token}",
                expoPushToken[..Math.Min(20, expoPushToken.Length)]);
        }
    }
}
