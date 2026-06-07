using System.Threading.Tasks;
using RaceNotifier.Settings;

namespace RaceNotifier.Notifications
{
    /// <summary>
    /// A transport that can deliver a message to a destination (Discord, custom webhook).
    /// New transports drop in behind this interface with no dispatcher changes.
    /// </summary>
    public interface INotifier
    {
        DestinationType Type { get; }

        /// <summary>
        /// Delivers a message and reports the outcome: success, a transient failure worth retrying,
        /// or a permanent failure that a retry cannot fix.
        /// </summary>
        Task<SendOutcome> SendAsync(Destination destination, string message);
    }
}
