using Google.Cloud.PubSub.V1;
using Microsoft.AspNetCore.Mvc;
using SendGrid;
using SendGrid.Helpers.Mail;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(new SendGridClient(builder.Configuration["SendGrid:ApiKey"]));
builder.Services.AddHttpClient();

var app = builder.Build();

app.MapPost("/run", async ([FromBody] PubsubMessageEnvelope envelope, SendGridClient client) =>
{
    var payload = envelope.Message?.Data?.ToStringUtf8();
    if (string.IsNullOrWhiteSpace(payload)) return Results.BadRequest();

    // Deserialize your existing notification payload with template id + tokens
    var notification = System.Text.Json.JsonSerializer.Deserialize<EmailNotification>(payload);

    var msg = MailHelper.CreateSingleTemplateEmail(
        from: new EmailAddress(notification.FromEmail, notification.FromName),
        to: new EmailAddress(notification.ToEmail, notification.ToName),
        templateId: notification.TemplateId,
        dynamicTemplateData: notification.Data);

    var response = await client.SendEmailAsync(msg);
    return response.IsSuccessStatusCode ? Results.Ok() : Results.StatusCode(500);
});

app.Run();

record PubsubMessageEnvelope(PubsubMessage Message);
record EmailNotification(string TemplateId, object Data, string ToEmail, string ToName, string FromEmail, string FromName);