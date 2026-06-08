using UnityEngine;

namespace GameDevTV.RTS.Utilities
{
    /// <summary>
    /// The Unity Behavior package's <c>SetAnimatorBoolAction</c> nodes log
    /// "SetAnimatorBoolAction: No Animator set." every time they execute on our drones.
    /// Those graph nodes are redundant — drone animation is driven entirely from C#
    /// (see <c>AbstractUnit.UpdateAnimation</c>) — and the package gives the embedded
    /// sub-graph nodes a sub-graph-local "Self" that never receives the live unit, so the
    /// node's Animator is always null and the warning cannot be cleared from our side via
    /// the supported API (the agent re-clones/restarts the graph on init, making it
    /// impossible to bind before the first tick).
    ///
    /// Since the message is known-harmless and was spamming the console, this installs a
    /// lightweight, self-contained log handler that drops ONLY this exact message and
    /// forwards everything else untouched. Installed once at startup; safe across domain
    /// reloads (it never double-wraps itself).
    /// </summary>
    public static class BehaviorLogFilter
    {
        private const string SuppressedMessage = "SetAnimatorBoolAction: No Animator set.";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            // Avoid wrapping more than once (domain reload re-runs this).
            if (Debug.unityLogger.logHandler is FilteringLogHandler) return;
            Debug.unityLogger.logHandler = new FilteringLogHandler(Debug.unityLogger.logHandler);
        }

        private class FilteringLogHandler : ILogHandler
        {
            private readonly ILogHandler inner;

            public FilteringLogHandler(ILogHandler inner)
            {
                this.inner = inner;
            }

            public void LogFormat(LogType logType, Object context, string format, params object[] args)
            {
                if (logType == LogType.Warning && ContainsSuppressed(format, args))
                {
                    return; // drop this specific, known-harmless warning
                }
                inner.LogFormat(logType, context, format, args);
            }

            public void LogException(System.Exception exception, Object context)
            {
                inner.LogException(exception, context);
            }

            private static bool ContainsSuppressed(string format, object[] args)
            {
                // Debug.LogWarning(object) routes through LogFormat as format "{0}" with the
                // message in args[0], so check both the format string and the first arg.
                if (!string.IsNullOrEmpty(format) && format.Contains(SuppressedMessage))
                {
                    return true;
                }
                if (args != null && args.Length > 0 && args[0] != null)
                {
                    string first = args[0].ToString();
                    if (!string.IsNullOrEmpty(first) && first.Contains(SuppressedMessage))
                    {
                        return true;
                    }
                }
                return false;
            }
        }
    }
}
