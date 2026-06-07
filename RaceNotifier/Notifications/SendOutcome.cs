namespace RaceNotifier.Notifications
{
    /// <summary>
    /// Result of a single send attempt. The dispatcher retries ONLY <see cref="TransientFailure"/>;
    /// a <see cref="PermanentFailure"/> is a deterministic client/config error, so a second identical
    /// request cannot help and is skipped (no wasted retry, no duplicate error log).
    /// </summary>
    public enum SendOutcome
    {
        Success = 0,
        TransientFailure = 1, // network/transport error, timeout, 408, 429, or 5xx — a retry may succeed
        PermanentFailure = 2  // 4xx config/payload/auth error, or a missing URL — a retry cannot help
    }

    /// <summary>
    /// Maps an HTTP status code to a retry decision. Shared by every HTTP notifier so the
    /// retry policy lives in one place.
    /// </summary>
    public static class HttpSendClassifier
    {
        public static SendOutcome FromStatusCode(int statusCode)
        {
            if (statusCode >= 200 && statusCode < 300)
                return SendOutcome.Success;

            // Retry only server-side (5xx), rate-limit (429), and request-timeout (408) responses.
            if (statusCode == 408 || statusCode == 429 || (statusCode >= 500 && statusCode < 600))
                return SendOutcome.TransientFailure;

            // Everything else (400/401/403/404/415, other 3xx/4xx) is deterministic — don't retry.
            return SendOutcome.PermanentFailure;
        }
    }
}
