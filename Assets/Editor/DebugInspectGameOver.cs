using UnityEditor;
using UnityEngine;
using System.Reflection;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.UI;

public static class DebugInspectGameOver
{
    [MenuItem("Debug/Inspect Game Over State")]
    public static void Inspect()
    {
        Debug.Log("=== Debug Game Over State ===");
        var manager = Object.FindAnyObjectByType<GameOverManager>(FindObjectsInactive.Include);
        var ui = Object.FindAnyObjectByType<GameOverUI>(FindObjectsInactive.Include);

        Debug.Log("GameOverManager exists: " + (manager != null));
        if (manager != null)
        {
            Debug.Log("  isActiveAndEnabled: " + manager.isActiveAndEnabled);
            
            // Check OnGameOver delegate subscribers via Reflection
            var eventField = typeof(GameOverManager).GetField("OnGameOver", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            if (eventField != null)
            {
                var del = eventField.GetValue(null) as System.MulticastDelegate;
                if (del != null)
                {
                    var list = del.GetInvocationList();
                    Debug.Log("  OnGameOver subscriber count: " + list.Length);
                    foreach (var sub in list)
                    {
                        Debug.Log("    Subscriber: " + sub.Method.DeclaringType.FullName + "." + sub.Method.Name);
                    }
                }
                else
                {
                    Debug.Log("  OnGameOver event field is null (no subscribers).");
                }
            }
            else
            {
                Debug.LogWarning("  Could not find 'OnGameOver' field via Reflection!");
            }
        }

        Debug.Log("GameOverUI exists: " + (ui != null));
        if (ui != null)
        {
            Debug.Log("  isActiveAndEnabled: " + ui.isActiveAndEnabled);
            Debug.Log("  gameObject: " + ui.gameObject.name);
        }
    }
}
