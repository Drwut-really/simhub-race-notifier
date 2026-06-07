using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using RaceNotifier.Settings;
using RaceNotifier.Telemetry;

namespace RaceNotifier.Notifications
{
    /// <summary>
    /// Owns the HttpClient and a single background worker. Button callbacks enqueue work
    /// here so sending never blocks SimHub's UI/data threads. Applies per-preset cooldown,
    /// retries once on failure, then reports status + fires events.
    /// </summary>
    public class NotificationDispatcher : IDisposable
    {
        private readonly Func<RaceNotifierSettings> _getSettings;
        private readonly Func<TelemetrySnapshot> _getTelemetry;
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

        public NotificationDispatcher(Func<RaceNotifierSettings> getSettings, Func<TelemetrySnapshot> getTelemetry)
        {
            _getSettings = getSettings;
            _getTelemetry = getTelemetry;
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            _notifiers = new Dictionary<DestinationType, INotifier>
            {
                { DestinationType.Discord, new DiscordNotifier(_http) },
                { DestinationType.CustomWebhook, new CustomWebhookNotifier(_http) }
            };
            _worker = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = "RaceNotifierSender"
            };
            _worker.Start();
        }

        /// <summary>
        /// Called from the SimHub input callback. <paramref name="idx"/> is the preset's ActionIndex.
        /// <paramref name="phase"/> is "press" today (the input mapping's release is a no-op); it is
        /// included for logging. The per-preset cooldown below guards against rapid repeat presses.
        /// </summary>
        public void FireByActionIndex(int idx, string phase = "press")
        {
            var settings = _getSettings();
            if (settings == null)
            {
                SimHub.Logging.Current.Info("[RaceNotifier] SendMessage" + idx + " fired, but settings are null — ignored.");
                return;
            }
            settings.EnsureInitialized();

            // One line per press/release so the whole path is visible in SimHub.txt.
            SimHub.Logging.Current.Info("[RaceNotifier] SendMessage" + idx + " fired (" + phase + ").");

            // Master switch: when off, drop everything (the plugin stays loaded).
            if (!settings.PluginEnabled)
            {
                SimHub.Logging.Current.Info("[RaceNotifier]   -> ignored: plugin is OFF (master switch).");
                return;
            }

            var preset = settings.Presets.FirstOrDefault(p => p != null && p.ActionIndex == idx);
            if (preset == null)
            {
                SimHub.Logging.Current.Info("[RaceNotifier]   -> ignored: no message has ActionIndex " + idx + ".");
                return;
            }
            if (!preset.Enabled)
            {
                SimHub.Logging.Current.Info("[RaceNotifier]   -> ignored: message " + idx + " (\"" + preset.Name + "\") is NOT enabled. Turn on its Enabled toggle.");
                return;
            }
            if (string.IsNullOrWhiteSpace(preset.Text))
            {
                SimHub.Logging.Current.Info("[RaceNotifier]   -> ignored: message " + idx + " has no text.");
                return;
            }

            // Per-preset cooldown, keyed by the stable ActionIndex.
            lock (_cooldownLock)
            {
                if (_lastFireUtc.TryGetValue(idx, out var last) &&
                    (DateTime.UtcNow - last).TotalSeconds < preset.CooldownSeconds)
                {
                    SimHub.Logging.Current.Info("[RaceNotifier]   -> ignored: message " + idx + " is on cooldown (" + preset.CooldownSeconds + "s).");
                    return;
                }
                _lastFireUtc[idx] = DateTime.UtcNow;
            }

            var targets = settings.Destinations
                .Where(d => preset.TargetDestinationIds.Contains(d.Id))
                .ToList();
            if (targets.Count == 0)
            {
                SimHub.Logging.Current.Info("[RaceNotifier]   -> ignored: message " + idx + " has no destination selected (tick a box under \"Send to\").");
                return;
            }

            SimHub.Logging.Current.Info("[RaceNotifier]   -> enqueued message " + idx + " to " + targets.Count + " destination(s).");
            var rendered = MessageVariables.Render(preset.Text, Telemetry());
            _queue.Add(new Job { Message = ApplyPrefix(settings, rendered), Destinations = targets });
        }

        /// <summary>
        /// Fire-and-forget a one-off test message to a single destination.
        /// Honors the master switch: when the plugin is disabled, tests are muted too.
        /// </summary>
        public void SendTest(Destination destination, string message)
        {
            if (destination == null)
                return;

            var settings = _getSettings();
            if (settings != null && !settings.PluginEnabled)
                return;

            _queue.Add(new Job
            {
                Message = ApplyPrefix(settings, MessageVariables.Render(message, Telemetry())),
                Destinations = new List<Destination> { destination }
            });
        }

        /// <summary>Latest telemetry snapshot, never null even if the accessor is missing or throws.</summary>
        private TelemetrySnapshot Telemetry()
        {
            try { return _getTelemetry?.Invoke() ?? TelemetrySnapshot.Empty; }
            catch { return TelemetrySnapshot.Empty; }
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
                SimHub.Logging.Current.Info("[RaceNotifier]   -> sent OK to " + job.Destinations.Count + " destination(s).");
                OnSent?.Invoke(job.Message);
            }
            else
            {
                LastStatus = "Failed";
                SimHub.Logging.Current.Info("[RaceNotifier]   -> send FAILED (see preceding error). Status set to Failed.");
                OnFailed?.Invoke(job.Message);
            }
        }

        private bool TrySendWithRetry(INotifier notifier, Destination dest, string message)
        {
            var outcome = TrySendOnce(notifier, dest, message);
            if (outcome == SendOutcome.Success)
                return true;
            if (outcome == SendOutcome.PermanentFailure)
                return false; // deterministic client/config error — a second identical request can't help
            Thread.Sleep(1000); // one quick retry, transient failures only
            return TrySendOnce(notifier, dest, message) == SendOutcome.Success;
        }

        private SendOutcome TrySendOnce(INotifier notifier, Destination dest, string message)
        {
            try
            {
                return notifier.SendAsync(dest, message).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                // Network/transport error — transient by nature, so allow the one retry.
                SimHub.Logging.Current.Info("[RaceNotifier] Send failed to '" + dest.Name + "': " + ex.Message);
                return SendOutcome.TransientFailure;
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
