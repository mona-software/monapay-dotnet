using System;
using System.Security.Cryptography;
using System.Text;

namespace MonaPay
{
    public sealed class WebhookResult
    {
        internal WebhookResult(bool ok, string? reason, object? payload) { Ok = ok; Reason = reason; Payload = payload; }
        public bool Ok { get; }
        public string? Reason { get; }
        public object? Payload { get; }
    }

    public static class Webhook
    {
        public static WebhookResult VerifyWebhook(byte[] rawBody, string? timestamp, string? signature, string secret, int tolerance = 300)
        {
            if (rawBody == null) throw new ArgumentNullException(nameof(rawBody));
            if (tolerance < 0) throw new ArgumentOutOfRangeException(nameof(tolerance), "tolerance phải là số không âm");
            if (string.IsNullOrEmpty(timestamp)) return Fail("missing_timestamp");
            for (int i = 0; i < timestamp.Length; i++) if (timestamp[i] < '0' || timestamp[i] > '9') return Fail("invalid_timestamp");
            if (!long.TryParse(timestamp, out long unix)) return Fail("invalid_timestamp");
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (unix > now + tolerance || unix < now - tolerance) return Fail("timestamp_out_of_tolerance");
            if (string.IsNullOrEmpty(signature)) return Fail("missing_signature");

            byte[] expected;
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret)))
            {
                byte[] prefix = Encoding.ASCII.GetBytes(timestamp + ".");
                var message = new byte[prefix.Length + rawBody.Length];
                Buffer.BlockCopy(prefix, 0, message, 0, prefix.Length);
                Buffer.BlockCopy(rawBody, 0, message, prefix.Length, rawBody.Length);
                expected = hmac.ComputeHash(message);
            }

            var supplied = new byte[32];
            bool formatOk = signature.Length == 71 && signature.StartsWith("sha256=", StringComparison.Ordinal);
            if (formatOk)
            {
                for (int i = 0; i < supplied.Length; i++)
                {
                    int high = Hex(signature[7 + i * 2]);
                    int low = Hex(signature[8 + i * 2]);
                    if (high < 0 || low < 0) { formatOk = false; break; }
                    supplied[i] = (byte)((high << 4) | low);
                }
            }
            bool equal = FixedTimeEquals(expected, supplied);
            Array.Clear(supplied, 0, supplied.Length);
            if (!equal || !formatOk) return Fail("invalid_signature");
            try
            {
                string json = new UTF8Encoding(false, true).GetString(rawBody);
                return new WebhookResult(true, null, JsonCodec.Parse(json));
            }
            catch (Exception error) when (error is DecoderFallbackException || error is ArgumentException)
            {
                return Fail("invalid_json");
            }
        }

        private static bool FixedTimeEquals(byte[] left, byte[] right)
        {
            int difference = left.Length ^ right.Length;
            int count = Math.Min(left.Length, right.Length);
            for (int i = 0; i < count; i++) difference |= left[i] ^ right[i];
            return difference == 0;
        }

        private static int Hex(char value)
        {
            if (value >= '0' && value <= '9') return value - '0';
            if (value >= 'a' && value <= 'f') return value - 'a' + 10;
            if (value >= 'A' && value <= 'F') return value - 'A' + 10;
            return -1;
        }

        private static WebhookResult Fail(string reason) => new WebhookResult(false, reason, null);
    }
}
