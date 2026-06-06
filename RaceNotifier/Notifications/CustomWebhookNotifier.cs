using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RaceNotifier.Settings;

namespace RaceNotifier.Notifications
{
    /// <summary>
    /// Sends a message to a user-defined HTTP endpoint. The body is either Discord-style
    /// JSON ({"content": "..."}) or the raw text, per the destination's WebhookBodyFormat.
    /// Success = any 2xx response. Logs the status code (never the URL) on non-2xx so
    /// setup failures (400/401/415) are visible.
    /// </summary>
    public class CustomWebhookNotifier : INotifier
    {
        private readonly HttpClient _http;

        public CustomWebhookNotifier(HttpClient http)
        {
            _http = http;
        }

        public DestinationType Type => DestinationType.CustomWebhook;

        public async Task<bool> SendAsync(Destination destination, string message)
        {
            if (destination == null || string.IsNullOrWhiteSpace(destination.WebhookUrl))
                return false;

            StringContent content;
            if (destination.WebhookBodyFormat == WebhookBodyFormat.Json)
            {
                var payload = JsonConvert.SerializeObject(new { content = message });
                content = new StringContent(payload, Encoding.UTF8, "application/json");
            }
            else
            {
                content = new StringContent(message ?? "", Encoding.UTF8, "text/plain");
            }

            using (content)
            using (var resp = await _http.PostAsync(destination.WebhookUrl, content).ConfigureAwait(false))
            {
                var code = (int)resp.StatusCode;
                if (code >= 200 && code < 300)
                    return true;

                SimHub.Logging.Current.Info(
                    "[RaceNotifier] Custom webhook '" + (destination.Name ?? "") + "' returned HTTP " + code + ".");
                return false;
            }
        }
    }
}
