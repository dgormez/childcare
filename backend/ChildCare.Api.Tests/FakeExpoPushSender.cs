using System.Collections.Concurrent;
using ChildCare.Application.Common;

namespace ChildCare.Api.Tests;

/// <summary>
/// Test double for IExpoPushSender — registered Singleton in OrganisationOnboardingWebAppFactory,
/// overriding Program.cs's real ExpoPushSender so tests never make a real outbound HTTP call to
/// Expo's push service (mirrors FakeGoogleTokenValidator/FakeEmailSender's pattern). Records
/// every attempted send so a test can assert dispatch happened (or didn't) without depending on
/// an external service, and lets a test opt a send into throwing to prove a transport failure
/// never fails the caller (FR-011a).
/// </summary>
public class FakeExpoPushSender : IExpoPushSender
{
    public record SentPush(string PushToken, string Title, string Body);

    public ConcurrentBag<SentPush> Sent { get; } = [];
    public bool ThrowOnSend { get; set; }

    public Task SendAsync(string pushToken, string title, string body, CancellationToken cancellationToken = default)
    {
        if (ThrowOnSend)
            throw new HttpRequestException("Simulated Expo push transport failure (test).");

        Sent.Add(new SentPush(pushToken, title, body));
        return Task.CompletedTask;
    }
}
