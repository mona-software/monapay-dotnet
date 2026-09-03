using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MonaPay
{
    public abstract class MonaPayResource
    {
        protected MonaPayResource(MonaPayClient client) { Client = client; }
        protected MonaPayClient Client { get; }
    }

    public sealed class KeysResource : MonaPayResource
    {
        internal KeysResource(MonaPayClient client) : base(client) { }
        public async Task<object?> GenerateAsync(string name = "Default Key", CancellationToken cancellationToken = default(CancellationToken))
        {
            object? data = await Client.RequestAsync("POST", "/api/v1/client-keys/generate", MonaPayClient.Object("name", string.IsNullOrEmpty(name) ? "Default Key" : name), null, cancellationToken).ConfigureAwait(false);
            if (data is IDictionary<string, object?> map && map.TryGetValue("client_secret", out object? value) && value is string secret && !string.IsNullOrEmpty(secret)) Client.SetGeneratedClientSecret(secret);
            return data;
        }
        public Task<object?> ListAsync(CancellationToken cancellationToken = default(CancellationToken)) => Client.RequestAsync("GET", "/api/v1/client-keys/list", null, null, cancellationToken);
        public Task<object?> DestroyAsync(string keyId, CancellationToken cancellationToken = default(CancellationToken)) => Client.RequestAsync("DELETE", "/api/v1/client-keys/destroy/" + MonaPayClient.Segment(keyId), null, null, cancellationToken);
        public Task<object?> RevealAsync(string keyId, IDictionary<string, object?> confirmation, CancellationToken cancellationToken = default(CancellationToken)) => Client.RequestAsync("POST", "/api/v1/client-keys/" + MonaPayClient.Segment(keyId) + "/reveal", confirmation, null, cancellationToken);
        public async Task<object?> RotateAsync(string keyId, CancellationToken cancellationToken = default(CancellationToken))
        {
            object? data = await Client.RequestAsync("POST", "/api/v1/client-keys/" + MonaPayClient.Segment(keyId) + "/rotate", MonaPayClient.Object(), null, cancellationToken).ConfigureAwait(false);
            if (data is IDictionary<string, object?> map && map.TryGetValue("client_secret", out object? value) && value is string secret && !string.IsNullOrEmpty(secret)) Client.SetClientSecret(secret);
            return data;
        }
    }

    public sealed class VirtualAccountsResource : MonaPayResource
    {
        internal VirtualAccountsResource(MonaPayClient client) : base(client) { }
        public Task<object?> RegisterAsync(IDictionary<string, object?> body, CancellationToken cancellationToken = default(CancellationToken)) => Client.RequestAsync("POST", "/api/v1/acb/virtual-account/registration", body, null, cancellationToken);
        public Task<object?> VerifyAsync(string requestId, string code, CancellationToken cancellationToken = default(CancellationToken)) => Client.RequestAsync("POST", "/api/v1/acb/" + MonaPayClient.Segment(requestId) + "/virtual-account/verification", MonaPayClient.Object("code", code), null, cancellationToken);
        public Task<object?> RegisterNotificationAsync(string vaId, IDictionary<string, object?> body, CancellationToken cancellationToken = default(CancellationToken)) => Client.RequestAsync("POST", "/api/v1/acb/" + MonaPayClient.Segment(vaId) + "/notification/registration", body, null, cancellationToken);
        public Task<object?> VerifyNotificationAsync(string requestId, string code, CancellationToken cancellationToken = default(CancellationToken)) => Client.RequestAsync("POST", "/api/v1/acb/" + MonaPayClient.Segment(requestId) + "/notification/verification", MonaPayClient.Object("code", code), null, cancellationToken);
        public Task<object?> ListAsync(string bankAccountId, CancellationToken cancellationToken = default(CancellationToken)) => Client.RequestAsync("GET", "/api/v1/acb/" + MonaPayClient.Segment(bankAccountId) + "/virtual-account/retrieve", null, null, cancellationToken);
    }

    public sealed class BankAccountsResource : MonaPayResource
    {
        internal BankAccountsResource(MonaPayClient client) : base(client) { }
        public Task<object?> ListAsync(CancellationToken cancellationToken = default(CancellationToken)) => Client.RequestAsync("GET", "/api/v1/client/bank-accounts", null, null, cancellationToken);
    }

    public sealed class PaymentProfileResource : MonaPayResource
    {
        internal PaymentProfileResource(MonaPayClient client) : base(client) { }
        public Task<object?> GetAsync(CancellationToken cancellationToken = default(CancellationToken)) => Client.RequestAsync("GET", "/api/v1/payment-profile", null, null, cancellationToken);
        public Task<object?> SetAsync(IDictionary<string, object?> body, CancellationToken cancellationToken = default(CancellationToken)) => Client.RequestAsync("PUT", "/api/v1/payment-profile", body, null, cancellationToken);
        public Task<object?> RotateReturnSecretAsync(CancellationToken cancellationToken = default(CancellationToken)) => Client.RequestAsync("POST", "/api/v1/payment-profile/rotate-return-secret", MonaPayClient.Object(), null, cancellationToken);
        public Task<object?> RevealReturnSecretAsync(IDictionary<string, object?> confirmation, CancellationToken cancellationToken = default(CancellationToken)) => Client.RequestAsync("POST", "/api/v1/payment-profile/reveal-return-secret", confirmation, null, cancellationToken);
    }

    public sealed class CheckoutOptions
    {
        public string? Status { get; set; }
        public string? OrderCode { get; set; }
        public string? FromDate { get; set; }
        public string? ToDate { get; set; }
        public int? Page { get; set; }
        public int? Limit { get; set; }
    }

    public sealed class CheckoutsResource : MonaPayResource
    {
        internal CheckoutsResource(MonaPayClient client) : base(client) { }
        public Task<object?> CreateAsync(IDictionary<string, object?> body, string? idempotencyKey = null, CancellationToken cancellationToken = default(CancellationToken)) => Client.RequestAsync(
            "POST", "/api/v1/checkouts", body, null, cancellationToken,
            new Dictionary<string, string> { ["Idempotency-Key"] = string.IsNullOrEmpty(idempotencyKey) ? Guid.NewGuid().ToString() : idempotencyKey! });
        public Task<object?> GetAsync(string checkoutId, CancellationToken cancellationToken = default(CancellationToken)) => Client.RequestAsync("GET", "/api/v1/checkouts/" + MonaPayClient.Segment(checkoutId), null, null, cancellationToken);
        public Task<object?> ListAsync(CheckoutOptions? options = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            CheckoutOptions actual = options ?? new CheckoutOptions();
            return Client.RequestAsync("GET", "/api/v1/checkouts", null, MonaPayClient.Object(
                "status", actual.Status, "order_code", actual.OrderCode, "from_date", actual.FromDate,
                "to_date", actual.ToDate, "page", actual.Page, "limit", actual.Limit), cancellationToken);
        }
        public Task<object?> CancelAsync(string checkoutId, string? idempotencyKey = null, CancellationToken cancellationToken = default(CancellationToken)) => Client.RequestAsync(
            "POST", "/api/v1/checkouts/" + MonaPayClient.Segment(checkoutId) + "/cancel", MonaPayClient.Object(), null, cancellationToken,
            new Dictionary<string, string> { ["Idempotency-Key"] = string.IsNullOrEmpty(idempotencyKey) ? Guid.NewGuid().ToString() : idempotencyKey! });
    }

    public sealed class QRResource : MonaPayResource
    {
        internal QRResource(MonaPayClient client) : base(client) { }
        public Task<object?> GenerateAsync(IDictionary<string, object?> body, CancellationToken cancellationToken = default(CancellationToken)) => Client.RequestAsync("POST", "/api/v1/acb/qr-payment/generate", body, null, cancellationToken);
        public Task<object?> CancelAsync(string qrCodeId, IDictionary<string, object?>? body = null, CancellationToken cancellationToken = default(CancellationToken)) => Client.RequestAsync("DELETE", "/api/v1/acb/qr-payment/" + MonaPayClient.Segment(qrCodeId) + "/cancellation", body, null, cancellationToken);
    }

    public sealed class TransactionOptions
    {
        public int Page { get; set; } = 1;
        public int Limit { get; set; } = 100;
        public string? SinceId { get; set; }
    }

    public sealed class TransactionsResource : MonaPayResource
    {
        internal TransactionsResource(MonaPayClient client) : base(client) { }
        public Task<object?> ListAsync(string virtualAccountNumber, TransactionOptions? options = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(virtualAccountNumber)) throw new ArgumentException("virtualAccountNumber là bắt buộc", nameof(virtualAccountNumber));
            TransactionOptions actual = options ?? new TransactionOptions();
            // SinceId is intentionally client-side because the current API does not accept since_id.
            return Client.RequestAsync("GET", "/api/v1/acb/virtual-account/transactions", null, MonaPayClient.Object(
                "virtual_account_number", virtualAccountNumber,
                "page", MonaPayClient.Positive(actual.Page, 1),
                "limit", MonaPayClient.Positive(actual.Limit, 100)
            ), cancellationToken);
        }
        public TransactionIterator Iterate(string virtualAccountNumber, TransactionOptions? options = null) => new TransactionIterator(this, virtualAccountNumber, options ?? new TransactionOptions());
        public Task<object?> RetryAsync(string transactionId, string targetType, string? targetId = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            Dictionary<string, object?> body = MonaPayClient.Object("target_type", targetType);
            if (targetId != null) body["target_id"] = targetId;
            return Client.RequestAsync("POST", "/api/v1/acb/virtual-account/transactions/" + MonaPayClient.Segment(transactionId) + "/retry", body, null, cancellationToken);
        }
    }

    public sealed class TransactionIterator
    {
        private readonly TransactionsResource resource;
        private readonly string virtualAccountNumber;
        private readonly int limit;
        private readonly string? sinceId;
        private int page;
        private IList<object?> items = new List<object?>();
        private int index;
        private bool done;

        internal TransactionIterator(TransactionsResource resourceValue, string virtualAccountNumberValue, TransactionOptions options)
        {
            resource = resourceValue;
            virtualAccountNumber = virtualAccountNumberValue;
            page = MonaPayClient.Positive(options.Page, 1);
            limit = MonaPayClient.Positive(options.Limit, 100);
            sinceId = options.SinceId;
        }

        public object? Current { get; private set; }

        public async Task<bool> MoveNextAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            while (true)
            {
                if (index < items.Count)
                {
                    object? candidate = items[index++];
                    if (sinceId != null && Matches(candidate, sinceId)) { done = true; return false; }
                    Current = candidate;
                    return true;
                }
                if (done) return false;
                object? response = await resource.ListAsync(virtualAccountNumber, new TransactionOptions { Page = page, Limit = limit }, cancellationToken).ConfigureAwait(false);
                if (!(response is IDictionary<string, object?> result)) throw new MonaPayException("Response giao dịch không phải object");
                items = result.TryGetValue("data", out object? data) && data is IList<object?> list ? list : new List<object?>();
                index = 0;
                if (result.TryGetValue("has_next", out object? hasNext) && hasNext is bool next) done = !next;
                else done = page >= MonaPayClient.IntValue(result.TryGetValue("last_page", out object? lastPage) ? lastPage : null, page);
                page++;
            }
        }

        private static bool Matches(object? item, string sinceId)
        {
            if (!(item is IDictionary<string, object?> map)) return false;
            return (map.TryGetValue("id", out object? id) && Convert.ToString(id) == sinceId) ||
                   (map.TryGetValue("transaction_code", out object? code) && Convert.ToString(code) == sinceId);
        }
    }

    public sealed class WebhooksResource : MonaPayResource
    {
        internal WebhooksResource(MonaPayClient client) : base(client) { }
        public Task<object?> ListAsync(CancellationToken cancellationToken = default(CancellationToken)) => Client.RequestAsync("GET", "/api/v1/client-webhooks", null, null, cancellationToken);
        public Task<object?> CreateAsync(IDictionary<string, object?> body, CancellationToken cancellationToken = default(CancellationToken)) => Client.RequestAsync("POST", "/api/v1/client-webhooks", body, null, cancellationToken);
        public Task<object?> UpdateAsync(string configId, IDictionary<string, object?> body, CancellationToken cancellationToken = default(CancellationToken)) => Client.RequestAsync("PUT", "/api/v1/client-webhooks/" + MonaPayClient.Segment(configId), body, null, cancellationToken);
        public Task<object?> RemoveAsync(string configId, CancellationToken cancellationToken = default(CancellationToken)) => Client.RequestAsync("DELETE", "/api/v1/client-webhooks/" + MonaPayClient.Segment(configId), null, null, cancellationToken);
        public Task<object?> TestAsync(IDictionary<string, object?> body, CancellationToken cancellationToken = default(CancellationToken)) => Client.RequestAsync("POST", "/api/v1/client-webhooks/test", body, null, cancellationToken);
    }

    public sealed class WebhookLogOptions
    {
        public string? Status { get; set; }
        public string? FromDate { get; set; }
        public string? ToDate { get; set; }
        public int? Page { get; set; }
        public int? Limit { get; set; }
    }

    public sealed class WebhookLogsResource : MonaPayResource
    {
        internal WebhookLogsResource(MonaPayClient client) : base(client) { }
        public Task<object?> ListAsync(WebhookLogOptions? options = null, CancellationToken cancellationToken = default(CancellationToken)) => Client.RequestAsync("GET", "/api/v1/webhook-logs", null, Query(options), cancellationToken);
        public Task<object?> StatsAsync(WebhookLogOptions? options = null, CancellationToken cancellationToken = default(CancellationToken)) => Client.RequestAsync("GET", "/api/v1/webhook-logs/stats", null, Query(options), cancellationToken);
        private static Dictionary<string, object?> Query(WebhookLogOptions? options)
        {
            WebhookLogOptions actual = options ?? new WebhookLogOptions();
            return MonaPayClient.Object("status", actual.Status, "from_date", actual.FromDate, "to_date", actual.ToDate, "page", actual.Page, "limit", actual.Limit);
        }
    }

    public sealed class SandboxResource : MonaPayResource
    {
        internal SandboxResource(MonaPayClient client) : base(client) { }
        public Task<object?> CreateTransactionAsync(IDictionary<string, object?> body, CancellationToken cancellationToken = default(CancellationToken)) => Client.RequestAsync("POST", "/api/v1/sandbox/transactions", body, null, cancellationToken);
    }

    public sealed class EmailConfigsResource : MonaPayResource
    {
        internal EmailConfigsResource(MonaPayClient client) : base(client) { }
        public Task<object?> ListAsync(CancellationToken cancellationToken = default(CancellationToken)) => Client.RequestAsync("GET", "/api/v1/email-configs", null, null, cancellationToken);
        public Task<object?> CreateAsync(IDictionary<string, object?> body, CancellationToken cancellationToken = default(CancellationToken)) => Client.RequestAsync("POST", "/api/v1/email-configs", body, null, cancellationToken);
        public Task<object?> GetAsync(string configId, CancellationToken cancellationToken = default(CancellationToken)) => Client.RequestAsync("GET", "/api/v1/email-configs/" + MonaPayClient.Segment(configId), null, null, cancellationToken);
        public Task<object?> UpdateAsync(string configId, IDictionary<string, object?> body, CancellationToken cancellationToken = default(CancellationToken)) => Client.RequestAsync("PUT", "/api/v1/email-configs/" + MonaPayClient.Segment(configId), body, null, cancellationToken);
        public Task<object?> RemoveAsync(string configId, CancellationToken cancellationToken = default(CancellationToken)) => Client.RequestAsync("DELETE", "/api/v1/email-configs/" + MonaPayClient.Segment(configId), null, null, cancellationToken);
        public Task<object?> VerifyAsync(string configId, string email, string code, CancellationToken cancellationToken = default(CancellationToken)) => Client.RequestAsync("POST", "/api/v1/email-configs/" + MonaPayClient.Segment(configId) + "/verify", MonaPayClient.Object("email", email, "code", code), null, cancellationToken);
        public Task<object?> ResendVerificationAsync(string configId, string email, CancellationToken cancellationToken = default(CancellationToken)) => Client.RequestAsync("POST", "/api/v1/email-configs/" + MonaPayClient.Segment(configId) + "/resend-verification", MonaPayClient.Object("email", email), null, cancellationToken);
        public Task<object?> TestAsync(string configId, CancellationToken cancellationToken = default(CancellationToken)) => Client.RequestAsync("POST", "/api/v1/email-configs/" + MonaPayClient.Segment(configId) + "/test", MonaPayClient.Object(), null, cancellationToken);
    }

    public sealed class EmailLogOptions
    {
        public string? ConfigId { get; set; }
        public string? Status { get; set; }
        public string? EventType { get; set; }
        public string? FromDate { get; set; }
        public string? ToDate { get; set; }
        public int? Page { get; set; }
        public int? Limit { get; set; }
    }

    public sealed class EmailLogsResource : MonaPayResource
    {
        internal EmailLogsResource(MonaPayClient client) : base(client) { }
        public Task<object?> ListAsync(EmailLogOptions? options = null, CancellationToken cancellationToken = default(CancellationToken)) => Client.RequestAsync("GET", "/api/v1/email-logs", null, Query(options), cancellationToken);
        public Task<object?> StatsAsync(EmailLogOptions? options = null, CancellationToken cancellationToken = default(CancellationToken)) => Client.RequestAsync("GET", "/api/v1/email-logs/stats", null, StatsQuery(options), cancellationToken);
        private static Dictionary<string, object?> Query(EmailLogOptions? options)
        {
            EmailLogOptions actual = options ?? new EmailLogOptions();
            return MonaPayClient.Object("config_id", actual.ConfigId, "status", actual.Status, "event_type", actual.EventType, "from_date", actual.FromDate, "to_date", actual.ToDate, "page", actual.Page, "limit", actual.Limit);
        }
        private static Dictionary<string, object?> StatsQuery(EmailLogOptions? options)
        {
            EmailLogOptions actual = options ?? new EmailLogOptions();
            return MonaPayClient.Object("from_date", actual.FromDate, "to_date", actual.ToDate);
        }
    }

    public sealed class EmailSuppressionsResource : MonaPayResource
    {
        internal EmailSuppressionsResource(MonaPayClient client) : base(client) { }
        public Task<object?> ListAsync(CancellationToken cancellationToken = default(CancellationToken)) => Client.RequestAsync("GET", "/api/v1/email-suppressions", null, null, cancellationToken);
        public Task<object?> RemoveAsync(string email, CancellationToken cancellationToken = default(CancellationToken)) => Client.RequestAsync("DELETE", "/api/v1/email-suppressions/" + MonaPayClient.Segment(email), null, null, cancellationToken);
    }
}
