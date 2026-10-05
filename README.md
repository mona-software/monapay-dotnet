# MONA Pay SDK for .NET

SDK .NET zero-dependency cho MONA Pay, đóng gói NuGet `MonaPay`, target `netstandard2.0` và `net8.0`. Client dùng `HttpClient`, tự login/cache Bearer token và login lại đúng một lần khi HTTP 401.

## Xác thực cho AI agent

```bash
export MONAPAY_CLIENT_ID="client-id"
export MONAPAY_CLIENT_SECRET="client-secret"
export MONAPAY_BASE_URL="https://api.monapay.vn"
```

```csharp
using var client = MonaPayClient.FromEnvironment();
object? profile = await client.MeAsync();
object? qr = await client.QR.GenerateAsync(qrBody);
object? sandbox = await client.Sandbox.CreateTransactionAsync(MonaPayClient.Object("virtual_account_number", "MONA123", "amount", 10000, "description", "AI test"));
Console.WriteLine(profile);
```

`FromEnvironment()` ưu tiên client credentials, cache token tới gần hạn và tự lấy lại token khi gặp HTTP 401. Username/password chỉ là fallback tương thích cũ, không dùng cho AI agent vì sẽ gãy khi bật 2FA.

## Cài đặt

Sau khi package được publish:

```bash
dotnet add package MonaPay --version 0.4.0
```

## Dùng nhanh

```csharp
using var client = new MonaPayClient(new MonaPayOptions
{
    Username = Environment.GetEnvironmentVariable("MONA_USERNAME")!,
    Password = Environment.GetEnvironmentVariable("MONA_PASSWORD")!,
    ClientSecret = Environment.GetEnvironmentVariable("MONA_CLIENT_SECRET")
});

object? profile = await client.MeAsync();
object? hooks = await client.Webhooks.ListAsync();
```

Các resource: `Keys`, `PaymentProfile`, `Checkouts`, `BankAccounts`, `VA` (đăng ký + hai bước OTP), `QR`, `Transactions`, `Webhooks`, `WebhookLogs`, `Sandbox`, `EmailConfigs`, `EmailLogs`, `EmailSuppressions`. Body JSON dùng `IDictionary<string,object?>`; helper `MonaPayClient.Object(...)` giúp viết ngắn. POST/PUT/DELETE tự có `X-Client-Secret`.

## Trang thanh toán (hosted checkout)

```csharp
object? checkout = await client.Checkouts.CreateAsync(MonaPayClient.Object("amount", 250000, "order_code", "DH10234", "return_url", "https://shop.vn/payment/return"));
string url = (string)((IDictionary<string, object?>)checkout!)["checkout_url"]!;
return Results.Redirect(url);
if (eventType == "CHECKOUT_PAID")
    await FulfillOnce(eventData["order_code"]);
```

SDK tự sinh `Idempotency-Key` cho `CreateAsync` và `CancelAsync`; truyền đối số key khi anh chị cần dùng key riêng. Nguồn sự thật để giao hàng là webhook `CHECKOUT_PAID` hoặc kết quả `GetAsync`, không phải redirect trình duyệt.

```csharp
TransactionIterator iterator = client.Transactions.Iterate(
    "MONA000001",
    new TransactionOptions { Limit = 100, SinceId = "FT26240001234" }
);
while (await iterator.MoveNextAsync())
{
    object? transaction = iterator.Current;
    // Lưu transaction_code làm idempotency key.
}
```

`SinceId` là checkpoint phía SDK: iterator dừng trước item có `id` hoặc `transaction_code` trùng mốc. API hiện chưa nhận query `since_id`, nên SDK không gửi tham số này.

Webhook phải được xác thực trên raw bytes:

```csharp
WebhookResult result = Webhook.VerifyWebhook(rawBody, timestamp, signature, secret);
if (!result.Ok) { /* trả HTTP 401 */ }
```

Ví dụ ASP.NET Core minimal API: `examples/AspNetCore/Program.cs`.

Gate offline:

```bash
dotnet build MonaPay.csproj --no-restore
dotnet run --project tests/SelfCheck/SelfCheck.csproj
dotnet build examples/AspNetCore/Example.csproj
```

Docs: https://monapay.vn/docs · Hotline 1900 636 648 · info@themona.global. MONA Pay miễn phí hoàn toàn.

## English

Zero-dependency .NET SDK targeting `netstandard2.0` and `net8.0`. It covers token caching and one 401 refresh, virtual accounts and both OTP steps, VietQR, client-side `SinceId` iteration, webhook configuration/logs/retry, and constant-time HMAC verification. An ASP.NET Core minimal API example and an offline console self-check are included.

MIT © The MONA Group.

**MONA Pay is part of MONA Cloud by The MONA Group.**

**MONA Pay thuộc bộ MONA Cloud của The MONA Group.**
