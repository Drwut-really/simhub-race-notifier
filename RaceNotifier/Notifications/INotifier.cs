using System.Threading.Tasks;
using RaceNotifier.Settings;

namespace RaceNotifier.Notifications
{
    /// <summary>
    /// A transport that can deliver a message to a destination. Discord ships in v1;
    /// Telegram drops in behind this interface in Phase 2 with no dispatcher changes.
    /// </summary>
    public interface INotifier
    {
        DestinationType Type { get; }

        /// <summary>Returns true on successful delivery.</summary>
        Task<bool> SendAsync(Destination destination, string message);
    }
}
