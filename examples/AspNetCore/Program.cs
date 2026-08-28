using MonaPay;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapPost("/webhooks/monapay", async (HttpRequest request, IConfiguration configuration) =>
{
    using var memory = new MemoryStream();
    await request.Body.CopyToAsync(memory);
    WebhookResult result = Webhook.VerifyWebhook(
        memory.ToArray(),
        request.Headers["X-Mona-Timestamp"].ToString(),
        request.Headers["X-Mona-Signature"].ToString(),
        configuration["MONA_WEBHOOK_SECRET"] ?? ""
    );
    if (!result.Ok) return Results.Json(new { ok = false, reason = result.Reason }, statusCode: 401);

    // Queue result.Payload; transaction_code is the idempotency key.
    return Results.Accepted(value: new { ok = true });
});

app.Run();
