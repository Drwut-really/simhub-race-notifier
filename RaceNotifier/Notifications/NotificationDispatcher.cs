using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using RaceNotifier.Settings;

namespace RaceNotifier.Notifications
{
    /// <summary>
    /// Owns the HttpClient and a single background worker. Button callbacks enqueue work
    /// here so sending never blocks SimHub's UI/data threads. Applies per-slot cooldown,
    /// retries once on failure, then reports status + fires events.
    /// </summary>
    public class NotificationDispatcher : IDisposable
    {
        private readonly Func<RaceNotifierSettings> _getSettings;
        private readonly Dictionary<DestinationType, INotifier> _notifiers;
        private readonly HttpClient _http;
        private readonly BlockingCollection<Job> _queue = new BlockingCollection<Job>(new ConcurrentQueue<Job>());
        private readonly Thread _worker;
        private readonly Dictionary<int, DateTime> _lastFireUtc = new Dictionary<int, DateTime>();
        private readonly object _cooldownLock = new object();

        /// <summary>Invoked (on the worker thread) with the message text after a successful send.</summary>
        public Action<string> OnSent;

        /// <summary>Invoked (on the worker thread) with the message text after a failed send.</summary>
        public Action<string> OnFailed;

        public string LastStatus { get; private set; } = "";
        public string LastMessage { get; private set; } = "";

        private class Job
        {
            public string Message;
            public List<Destination> Destinations;
        }

        public NotificationDispatcher(Func<RaceNotifierSettings> getSettings)
        {
            _getSettings = getSettings;
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            _notifiers = new Dictionary<DestinationType, INotifier>
            {
                { DestinationType.Discord, new DiscordNotifier(_http) }
                // Phase 2: { DestinationType.Telegram, new TelegramNotifier(_http) }
            };
            _worker = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = "RaceNotifierSender"
            };
            _worker.Start();
        }

        /// <summary>Called from the SimHub action callback. Slot is 1-based.</summary>
        public void Fire(int slot)
        {
            var settings = _getSettings();
            if (settings == null)
                return;
            settings.EnsureInitialized();

            int idx = slot - 1;
            if (idx < 0 || idx >= settings.Slots.Count)
                return;

            var s = settings.Slots[idx];
            if (!s.Enabled || string.IsNullOrWhiteSpace(s.Text))
                return;

            // Per-slot cooldown.
            lock (_cooldownLock)
            {
                if (_lastFireUtc.TryGetValue(slot, out var last) &&
                    (DateTime.UtcNow - last).TotalSeconds < s.CooldownSeconds)
                {
                    return;
                }
                _lastFireUtc[slot] = DateTime.UtcNow;
            }

            var targets = settings.Destinations
                .Where(d => s.TargetDestinationIds.Contains(d.Id))
                .ToList();
            if (targets.Count == 0)
                return;

            _queue.Add(new Job { Message = ApplyPrefix(settings, s.Text), Destinations = targets });
        }

        /// <summary>Fire-and-forget a one-off test message to a single destination.</summary>
        public void SendTest(Destination destination, string message)
        {
            if (destination == null)
                return;
            _queue.Add(new Job
            {
                Message = ApplyPrefix(_getSettings(), message),
                Destinations = new List<Destination> { destination }
            });
        }

        private static string ApplyPrefix(RaceNotifierSettings settings, string text)
        {
            if (settings != null && settings.PrefixSenderName && !string.IsNullOrWhiteSpace(settings.SenderName))
                return "[" + settings.SenderName + "] " + text;
            return text;
        }

        private void WorkerLoop()
        {
            foreach (var job in _queue.GetConsumingEnumerable())
            {
                try
                {
                    ProcessJob(job);
                }
                catch (Exception ex)
                {
                    SimHub.Logging.Current.Info("[RaceNotifier] Unexpected send error: " + ex);
                }
            }
        }

        private void ProcessJob(Job job)
        {
            bool allOk = true;
            foreach (var dest in job.Destinations)
            {
                if (!_notifiers.TryGetValue(dest.Type, out var notifier))
                {
                    allOk = false;
                    continue;
                }
                allOk &= TrySendWithRetry(notifier, dest, job.Message);
            }

            LastMessage = job.Message;
            if (allOk)
            {
                LastStatus = "OK";
                OnSent?.Invoke(job.Message);
            }
            else
            {
                LastStatus = "Failed";
                OnFailed?.Invoke(job.Message);
            }
        }

        private bool TrySendWithRetry(INotifier notifier, Destination dest, string message)
        {
            if (TrySendOnce(notifier, dest, message))
                return true;
            Thread.Sleep(1000); // one quick retry
            return TrySendOnce(notifier, dest, message);
        }

        private bool TrySendOnce(INotifier notifier, Destination dest, string message)
        {
            try
            {
                return notifier.SendAsync(dest, message).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                SimHub.Logging.Current.Info("[RaceNotifier] Send failed to '" + dest.Name + "': " + ex.Message);
                return false;
            }
        }

        public void Dispose()
        {
            try { _queue.CompleteAdding(); } catch { }
            try { _worker.Join(2000); } catch { }
            try { _http.Dispose(); } catch { }
        }
    }
}
