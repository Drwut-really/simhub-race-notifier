using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RaceNotifier.Settings;

namespace RaceNotifier.Notifications
{
    /// <summary>
    /// Sends a message to a Discord channel via an incoming webhook URL.
    /// POST {"content": "..."} -> 204 No Content on success.
    /// </summary>
    public class DiscordNotifier : INotifier
    {
        private readonly HttpClient _http;

        public DiscordNotifier(HttpClient http)
        {
            _http = http;
        }

        public DestinationType Type => DestinationType.Discord;

        public async Task<bool> SendAsync(Destination destination, string message)
        {
            if (destination == null || string.IsNullOrWhiteSpace(destination.DiscordWebhookUrl))
                return false;

            var payload = JsonConvert.SerializeObject(new { content = message });
            using (var content = new StringContent(payload, Encoding.UTF8, "application/json"))
            using (var resp = await _http.PostAsync(destination.DiscordWebhookUrl, content).ConfigureAwait(false))
            {
                var code = (int)resp.StatusCode;
                return code >= 200 && code < 300;
            }
        }
    }
}
