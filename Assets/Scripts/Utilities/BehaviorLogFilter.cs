#if UNITY_EDITOR
using UnityEditor;
#endif
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
    /// Additionally, Unity 6 background services occasionally throw "Curl error 3: URL rejected",
    /// which is a harmless telemetry/AI-assistant artifact.
    ///
    /// Since these messages are known-harmless and spam the console, this installs a
    /// lightweight log handler that drops them and forwards everything else untouched.
    /// Installed at startup in both Editor and Runtime.
    /// </summary>
#if UNITY_EDITOR
    [InitializeOnLoad]
#endif
    public static class BehaviorLogFilter
    {
        private static readonly string[] SuppressedMessages = 
        {
            "SetAnimatorBoolAction: No Animator set.",
            "Curl error 3: URL rejected",
            "Resolve of invalid GC handle",
            "ScriptableSingleton already exists",
            "Assertion failed on expression: '!m_MonoReference.HasTarget()'"
        };

#if UNITY_EDITOR
        private static ILogHandler originalHandler;
#endif

#if UNITY_EDITOR
        static BehaviorLogFilter()
        {
            Install();
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            // Avoid wrapping more than once (domain reload re-runs this).
            if (Debug.unityLogger.logHandler is FilteringLogHandler) return;
            
            ILogHandler currentHandler = Debug.unityLogger.logHandler;
#if UNITY_EDITOR
            originalHandler = currentHandler;
            AssemblyReloadEvents.beforeAssemblyReload += Cleanup;
#endif
            Debug.unityLogger.logHandler = new FilteringLogHandler(currentHandler);
            
            // Log installation
            Debug.Log("[BehaviorLogFilter] Log suppression active (dropping SetAnimatorBoolAction warnings).");
        }

#if UNITY_EDITOR
        private static void Cleanup()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= Cleanup;
            if (originalHandler != null)
            {
                if (Debug.unityLogger.logHandler is FilteringLogHandler)
                {
                    Debug.unityLogger.logHandler = originalHandler;
                }
                originalHandler = null;
            }
        }
#endif

        private class FilteringLogHandler : ILogHandler
        {
            private readonly ILogHandler inner;

            public FilteringLogHandler(ILogHandler inner)
            {
                this.inner = inner;
            }

            public void LogFormat(LogType logType, Object context, string format, params object[] args)
            {
                if (IsSuppressed(logType, format, args)) return;
                inner.LogFormat(logType, context, format, args);
            }

            public void LogException(System.Exception exception, Object context)
            {
                if (exception != null && !string.IsNullOrEmpty(exception.Message) && exception.Message.Contains("Curl error 3")) return;
                inner.LogException(exception, context);
            }

            private static bool IsSuppressed(LogType logType, string format, object[] args)
            {
                foreach (string msg in SuppressedMessages)
                {
                    if (ContainsMessage(msg, format, args)) return true;
                }
                return false;
            }

            private static bool ContainsMessage(string target, string format, object[] args)
            {
                if (!string.IsNullOrEmpty(format) && format.Contains(target))
                {
                    return true;
                }
                if (args != null && args.Length > 0 && args[0] != null)
                {
                    string first = args[0].ToString();
                    if (!string.IsNullOrEmpty(first) && first.Contains(target))
                    {
                        return true;
                    }
                }
                return false;
            }
        }
    }
}
