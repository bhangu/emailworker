using CloudNative.CloudEvents;
using Google.Cloud.Functions.Framework;
using Google.Events.Protobuf.Cloud.PubSub.V1;
using Microsoft.Extensions.Logging;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace EmailFunction;

public class Function : ICloudEventFunction<MessagePublishedData>
{
    private readonly SendGridClient _client;
    private readonly ILogger<Function> _logger;

    public Function(IConfiguration config, ILogger<Function> logger)
    {
        var apiKey = config["SendGrid:ApiKey"] ?? throw new InvalidOperationException("SendGrid API key missing");
        _client = new SendGridClient(apiKey);
        _logger = logger;
    }

    public async Task HandleAsync(CloudEvent cloudEvent, MessagePublishedData data, CancellationToken cancellationToken)
    {
        var payload = data.Message?.TextData;
        if (string.IsNullOrWhiteSpace(payload))
        {
            _logger.LogWarning("Empty email payload");
            return;
        }

        var notification = System.Text.Json.JsonSerializer.Deserialize<EmailNotification>(payload);
        if (notification is null)
        {
            _logger.LogWarning("Invalid email payload");
            return;
        }

        var msg = MailHelper.CreateSingleTemplateEmail(
            from: new EmailAddress(notification.FromEmail, notification.FromName),
            to: new EmailAddress(notification.ToEmail, notification.ToName),
            templateId: notification.TemplateId,
            dynamicTemplateData: notification.Data);

        var emails = notification.recipients.Select(r => new EmailAddress(r, r)).ToList();

        var msg1 = MailHelper.CreateSingleEmailToMultipleRecipients(
            from: new EmailAddress(notification.FromEmail, notification.FromName),
            tos: emails,
            subject: " ",
            plainTextContent: " ",
            htmlContent: " ");

            //templateId: notification.TemplateId,
            //dynamicTemplateData: notification.Data);

        var response = await _client.SendEmailAsync(msg, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Email send failed: {Status}", response.StatusCode);
            throw new Exception("SendGrid error");
        }
    }
}

record EmailNotification(string TemplateId, object Data, string ToEmail, string ToName, string FromEmail, string FromName, string[] recipients);