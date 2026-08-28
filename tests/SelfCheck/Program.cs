using System.Security.Cryptography;
using System.Text;
using MonaPay;

static void Check(bool value, string message)
{
    if (!value) throw new Exception("Self-check failed: " + message);
}

static MonaPayResponse Ok(string data) => new(200, "{\"success\":true,\"data\":" + data + "}");

byte[] raw = Encoding.UTF8.GetBytes("{\"amount\":2500000,\"transaction_code\":\"FT1\"}");
string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
string signature;
using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes("test-secret")))
    signature = "sha256=" + Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(timestamp + ".").Concat(raw).ToArray())).ToLowerInvariant();
Check(Webhook.VerifyWebhook(raw, timestamp, signature, "test-secret").Ok, "valid webhook");
Check(Webhook.VerifyWebhook(raw, timestamp, "sha256=" + new string('0', 64), "test-secret").Reason == "invalid_signature", "bad signature");
Check(Webhook.VerifyWebhook(raw, (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 301).ToString(), signature, "test-secret").Reason == "timestamp_out_of_tolerance", "old timestamp");

int logins = 0;
int meCalls = 0;
var calls = new List<MonaPayRequest>();
var transport = new FakeTransport(request =>
{
    calls.Add(request);
    if (request.Url.EndsWith("/api/v1/client/login")) return Ok("{\"access_token\":\"token-" + (++logins) + "\"}");
    if (request.Url.EndsWith("/api/v1/client-webhooks"))
    {
        Check(request.Headers["X-Client-Secret"] == "secret", "client secret header");
        return Ok("{\"id\":\"hook-1\"}");
    }
    meCalls++;
    if (meCalls == 1) return new MonaPayResponse(401, "{\"detail\":\"expired\"}");
    Check(request.Headers["Authorization"] == "Bearer token-2", "refreshed bearer");
    Check(!request.Headers.ContainsKey("X-Client-Secret"), "GET has no secret");
    return Ok("{\"username\":\"user\"}");
});
using (var client = new MonaPayClient(new MonaPayOptions { Username = "user", Password = "pass", ClientSecret = "secret", BaseUrl = "https://example.test/", Transport = transport }))
{
    await client.Webhooks.CreateAsync(MonaPayClient.Object("name", "Shop"));
    await client.MeAsync();
}
Check(logins == 2 && calls.Count == 5, "one refresh after 401");

int pageCalls = 0;
var pageTransport = new FakeTransport(request =>
{
    if (request.Url.EndsWith("/api/v1/client/login")) return Ok("{\"access_token\":\"token\"}");
    Check(!request.Url.Contains("since_id"), "since_id stays client-side");
    pageCalls++;
    return request.Url.Contains("page=1")
        ? Ok("{\"data\":[{\"id\":\"tx-3\"},{\"id\":\"tx-2\"}],\"last_page\":2}")
        : Ok("{\"data\":[{\"id\":\"tx-1\"}],\"last_page\":2}");
});
var ids = new List<string>();
using (var client = new MonaPayClient(new MonaPayOptions { Username = "user", Password = "pass", BaseUrl = "https://example.test", Transport = pageTransport }))
{
    TransactionIterator iterator = client.Transactions.Iterate("MONA 01", new TransactionOptions { Limit = 2, SinceId = "tx-1" });
    while (await iterator.MoveNextAsync()) ids.Add(Convert.ToString(((IDictionary<string, object?>)iterator.Current!)["id"])!);
}
Check(string.Join(",", ids) == "tx-3,tx-2" && pageCalls == 2, "iterator + since id");

Console.WriteLine("MONA Pay .NET self-check: PASS");

sealed class FakeTransport : IMonaPayTransport
{
    private readonly Func<MonaPayRequest, MonaPayResponse> handler;
    public FakeTransport(Func<MonaPayRequest, MonaPayResponse> value) { handler = value; }
    public Task<MonaPayResponse> SendAsync(MonaPayRequest request, CancellationToken cancellationToken) => Task.FromResult(handler(request));
}
