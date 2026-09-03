using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MonaPay
{
    public sealed class MonaPayOptions
    {
        public string ClientId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? ClientSecret { get; set; }
        public string BaseUrl { get; set; } = MonaPayClient.DefaultBaseUrl;
        public HttpClient? HttpClient { get; set; }
        public IMonaPayTransport? Transport { get; set; }
    }

    public sealed class MonaPayRequest
    {
        public MonaPayRequest(string method, string url, IDictionary<string, string> headers, string? body)
        {
            Method = method;
            Url = url;
            Headers = new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase);
            Body = body;
        }
        public string Method { get; }
        public string Url { get; }
        public IReadOnlyDictionary<string, string> Headers { get; }
        public string? Body { get; }
    }

    public sealed class MonaPayResponse
    {
        public MonaPayResponse(int statusCode, string? body) { StatusCode = statusCode; Body = body ?? string.Empty; }
        public int StatusCode { get; }
        public string Body { get; }
    }

    public interface IMonaPayTransport
    {
        Task<MonaPayResponse> SendAsync(MonaPayRequest request, CancellationToken cancellationToken);
    }

    public sealed class MonaPayClient : IDisposable
    {
        public const string DefaultBaseUrl = "https://api.monapay.vn";

        private readonly string username;
        private readonly string password;
        private readonly string clientId;
        private readonly string baseUrl;
        private readonly IMonaPayTransport transport;
        private readonly IDisposable? ownedTransport;
        private readonly SemaphoreSlim authLock = new SemaphoreSlim(1, 1);
        private string? accessToken;
        private string? clientSecret;
        private DateTimeOffset tokenExpiresAt;

        public MonaPayClient(MonaPayOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            bool hasClientCredentials = !string.IsNullOrWhiteSpace(options.ClientId) && !string.IsNullOrWhiteSpace(options.ClientSecret);
            bool hasPasswordCredentials = !string.IsNullOrWhiteSpace(options.Username) && !string.IsNullOrWhiteSpace(options.Password);
            if (!hasClientCredentials && !hasPasswordCredentials) throw new ArgumentException("Cần client ID + client secret hoặc username + password; không dùng password cho AI agent vì sẽ gãy khi bật 2FA");
            clientId = options.ClientId;
            username = options.Username;
            password = options.Password;
            clientSecret = options.ClientSecret;
            baseUrl = TrimBaseUrl(options.BaseUrl);
            if (options.Transport != null) transport = options.Transport;
            else
            {
                var httpTransport = new HttpClientTransport(options.HttpClient);
                transport = httpTransport;
                ownedTransport = httpTransport;
            }

            Keys = new KeysResource(this);
            VA = new VirtualAccountsResource(this);
            BankAccounts = new BankAccountsResource(this);
            QR = new QRResource(this);
            Transactions = new TransactionsResource(this);
            Webhooks = new WebhooksResource(this);
            WebhookLogs = new WebhookLogsResource(this);
            Sandbox = new SandboxResource(this);
            EmailConfigs = new EmailConfigsResource(this);
            EmailLogs = new EmailLogsResource(this);
            EmailSuppressions = new EmailSuppressionsResource(this);
        }

        public static MonaPayClient FromEnvironment()
        {
            return new MonaPayClient(new MonaPayOptions
            {
                ClientId = Environment.GetEnvironmentVariable("MONAPAY_CLIENT_ID") ?? string.Empty,
                ClientSecret = Environment.GetEnvironmentVariable("MONAPAY_CLIENT_SECRET"),
                Username = Environment.GetEnvironmentVariable("MONAPAY_USERNAME") ?? string.Empty,
                Password = Environment.GetEnvironmentVariable("MONAPAY_PASSWORD") ?? string.Empty,
                BaseUrl = Environment.GetEnvironmentVariable("MONAPAY_BASE_URL") ?? DefaultBaseUrl
            });
        }

        public KeysResource Keys { get; }
        public VirtualAccountsResource VA { get; }
        public BankAccountsResource BankAccounts { get; }
        public QRResource QR { get; }
        public TransactionsResource Transactions { get; }
        public WebhooksResource Webhooks { get; }
        public WebhookLogsResource WebhookLogs { get; }
        public SandboxResource Sandbox { get; }
        public EmailConfigsResource EmailConfigs { get; }
        public EmailLogsResource EmailLogs { get; }
        public EmailSuppressionsResource EmailSuppressions { get; }

        public void SetClientSecret(string value) { clientSecret = value; }
        internal void SetGeneratedClientSecret(string value)
        {
            if (string.IsNullOrEmpty(clientSecret)) clientSecret = value;
        }
        public Task<object?> MeAsync(CancellationToken cancellationToken = default(CancellationToken)) => RequestAsync("GET", "/api/v1/client/me", null, null, cancellationToken);

        internal async Task<object?> RequestAsync(string method, string path, object? body, IDictionary<string, object?>? query, CancellationToken cancellationToken)
        {
            await LoginAsync(cancellationToken).ConfigureAwait(false);
            string usedToken = accessToken!;
            try
            {
                return await SendAsync(method, path, body, query, usedToken, clientSecret, cancellationToken).ConfigureAwait(false);
            }
            catch (MonaPayException error) when (error.StatusCode == 401)
            {
                await authLock.WaitAsync(cancellationToken).ConfigureAwait(false);
                try { if (accessToken == usedToken) { accessToken = null; tokenExpiresAt = default(DateTimeOffset); } }
                finally { authLock.Release(); }
                await LoginAsync(cancellationToken).ConfigureAwait(false);
                return await SendAsync(method, path, body, query, accessToken, clientSecret, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task LoginAsync(CancellationToken cancellationToken)
        {
            await authLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!string.IsNullOrEmpty(accessToken) && DateTimeOffset.UtcNow < tokenExpiresAt) return;
                bool usingClientCredentials = !string.IsNullOrEmpty(clientId) && !string.IsNullOrEmpty(clientSecret);
                string path = usingClientCredentials ? "/api/v1/oauth/token" : "/api/v1/client/login";
                object body = usingClientCredentials
                    ? Object("grant_type", "client_credentials", "client_id", clientId, "client_secret", clientSecret)
                    : Object("username", username, "password", password);
                object? data = await SendAsync("POST", path, body, null, null, null, cancellationToken).ConfigureAwait(false);
                if (!(data is IDictionary<string, object?> map) || !(map.TryGetValue("access_token", out object? value)) || !(value is string token) || string.IsNullOrEmpty(token))
                    throw new MonaPayException("Response đăng nhập không có access_token", body: data);
                accessToken = token;
                double expiresIn = usingClientCredentials ? 3600 : 86400;
                if (map.TryGetValue("expires_in", out object? rawExpires))
                {
                    if (rawExpires is long integer) expiresIn = integer;
                    else if (rawExpires is double number) expiresIn = number;
                }
                tokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(0, expiresIn - 60));
            }
            finally { authLock.Release(); }
        }

        private async Task<object?> SendAsync(string method, string path, object? body, IDictionary<string, object?>? query, string? token, string? secret, CancellationToken cancellationToken)
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Accept"] = "application/json" };
            if (!string.IsNullOrEmpty(token)) headers["Authorization"] = "Bearer " + token;
            if (!string.IsNullOrEmpty(token) && method != "GET" && !string.IsNullOrEmpty(secret)) headers["X-Client-Secret"] = secret!;
            string? encodedBody = null;
            if (body != null)
            {
                headers["Content-Type"] = "application/json";
                encodedBody = JsonCodec.Serialize(body);
            }

            MonaPayResponse response;
            try { response = await transport.SendAsync(new MonaPayRequest(method, BuildUrl(path, query), headers, encodedBody), cancellationToken).ConfigureAwait(false); }
            catch (MonaPayException) { throw; }
            catch (Exception error) { throw new MonaPayException("Không kết nối được MONA Pay", innerException: error); }

            object? parsed;
            try { parsed = JsonCodec.Parse(string.IsNullOrEmpty(response.Body) ? "{}" : response.Body); }
            catch (ArgumentException) { throw new MonaPayException("MONA Pay trả response không phải JSON (HTTP " + response.StatusCode + ")", response.StatusCode, response.Body); }
            if (!(parsed is IDictionary<string, object?> envelope)) throw new MonaPayException("MONA Pay trả response không phải object JSON", response.StatusCode, parsed);
            bool failed = response.StatusCode < 200 || response.StatusCode >= 300 || (envelope.TryGetValue("success", out object? success) && success is bool successBool && !successBool);
            if (failed)
            {
                string? message = envelope.TryGetValue("message", out object? rawMessage) ? rawMessage as string : null;
                if (string.IsNullOrEmpty(message)) message = envelope.TryGetValue("detail", out object? detail) ? detail as string : null;
                if (string.IsNullOrEmpty(message)) message = "MONA Pay API lỗi HTTP " + response.StatusCode;
                throw new MonaPayException(message!, response.StatusCode, envelope);
            }
            envelope.TryGetValue("data", out object? data);
            return data;
        }

        private string BuildUrl(string path, IDictionary<string, object?>? query)
        {
            var result = new StringBuilder(baseUrl).Append(path);
            bool first = true;
            if (query != null)
            {
                foreach (KeyValuePair<string, object?> item in query)
                {
                    if (item.Value == null) continue;
                    result.Append(first ? '?' : '&');
                    first = false;
                    result.Append(Uri.EscapeDataString(item.Key)).Append('=').Append(Uri.EscapeDataString(Convert.ToString(item.Value, CultureInfo.InvariantCulture)!));
                }
            }
            return result.ToString();
        }

        private static string TrimBaseUrl(string? value)
        {
            string result = string.IsNullOrEmpty(value) ? DefaultBaseUrl : value!;
            result = result.TrimEnd('/');
            if (!Uri.TryCreate(result, UriKind.Absolute, out _)) throw new ArgumentException("base URL không hợp lệ");
            return result;
        }

        internal static string Segment(string value) => Uri.EscapeDataString(value ?? string.Empty);
        internal static int Positive(int value, int fallback) => value > 0 ? value : fallback;
        internal static int IntValue(object? value, int fallback) => value is long integer ? checked((int)integer) : value is double number ? checked((int)number) : fallback;

        public static Dictionary<string, object?> Object(params object?[] keyValues)
        {
            if (keyValues.Length % 2 != 0) throw new ArgumentException("key/value phải đi theo cặp");
            var result = new Dictionary<string, object?>();
            for (int i = 0; i < keyValues.Length; i += 2) result[Convert.ToString(keyValues[i], CultureInfo.InvariantCulture)!] = keyValues[i + 1];
            return result;
        }

        public void Dispose()
        {
            ownedTransport?.Dispose();
            authLock.Dispose();
        }

        private sealed class HttpClientTransport : IMonaPayTransport, IDisposable
        {
            private readonly HttpClient client;
            private readonly bool ownsClient;
            public HttpClientTransport(HttpClient? supplied)
            {
                client = supplied ?? new HttpClient();
                ownsClient = supplied == null;
            }
            public async Task<MonaPayResponse> SendAsync(MonaPayRequest request, CancellationToken cancellationToken)
            {
                using (var message = new HttpRequestMessage(new HttpMethod(request.Method), request.Url))
                {
                    if (request.Body != null) message.Content = new StringContent(request.Body, Encoding.UTF8, "application/json");
                    foreach (KeyValuePair<string, string> header in request.Headers)
                    {
                        if (header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)) continue;
                        message.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                    using (HttpResponseMessage response = await client.SendAsync(message, cancellationToken).ConfigureAwait(false))
                    {
                        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        return new MonaPayResponse((int)response.StatusCode, body);
                    }
                }
            }
            public void Dispose() { if (ownsClient) client.Dispose(); }
        }
    }
}
