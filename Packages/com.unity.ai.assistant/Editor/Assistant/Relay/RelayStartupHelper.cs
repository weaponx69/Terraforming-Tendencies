using UnityEditor;
using UnityEngine;
using System;
using System.IO;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace Unity.Relay.Editor
{
    [InitializeOnLoad]
    public static class RelayStartupHelper
    {
        static RelayStartupHelper()
        {
            EditorApplication.delayCall += StartRelay;
        }

        private static void StartRelay()
        {
            KillZombieRelays();
            CleanOrphanedSockets();
            Debug.Log("[RelayStartupHelper] Forcing RelayService to start...");
            _ = RelayService.Instance.StartAsync();
        }

        private static void KillZombieRelays()
        {
            try
            {
                string projectPath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                var processes = Process.GetProcesses();
                foreach (var p in processes)
                {
                    try
                    {
                        if (p.ProcessName.Contains("relay_linux"))
                        {
                            string cmdlinePath = $"/proc/{p.Id}/cmdline";
                            if (File.Exists(cmdlinePath))
                            {
                                string cmdline = File.ReadAllText(cmdlinePath).Replace('\0', ' ');
                                if (cmdline.Contains(projectPath))
                                {
                                    Debug.Log($"[RelayStartupHelper] Killing lingering zombie relay process {p.Id} on port 9001/9002...");
                                    p.Kill();
                                    p.WaitForExit(1000);
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Ignore processes we don't have access to
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RelayStartupHelper] Failed to clean up zombie relays: {ex.Message}");
            }
        }

        private static void CleanOrphanedSockets()
        {
            try
            {
                string tmpDir = "/tmp";
                if (!Directory.Exists(tmpDir)) return;

                string[] files = Directory.GetFiles(tmpDir, "unity-mcp-*");
                foreach (string file in files)
                {
                    try
                    {
                        string fileName = Path.GetFileName(file);
                        string[] parts = fileName.Split('-');
                        if (parts.Length > 0)
                        {
                            string pidStr = parts[parts.Length - 1];
                            if (int.TryParse(pidStr, out int pid))
                            {
                                bool isRunning = false;
                                try
                                {
                                    var proc = Process.GetProcessById(pid);
                                    isRunning = !proc.HasExited;
                                }
                                catch
                                {
                                    // Process doesn't exist
                                    isRunning = false;
                                }

                                if (!isRunning && pid != Process.GetCurrentProcess().Id)
                                {
                                    Debug.Log($"[RelayStartupHelper] Deleting orphaned Unity MCP socket: {file}");
                                    File.Delete(file);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[RelayStartupHelper] Failed to inspect socket file {file}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RelayStartupHelper] Failed to clean orphaned sockets: {ex.Message}");
            }
        }
    }
}
