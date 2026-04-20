using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace App.Net
{
    public class ProcessService : IProcessService
    {
        // Default system processes that can never be removed
        private readonly List<string> defaultExcluded = new List<string>
        {
            "systemd","bash","sh","sudo","sshd",
            "init","login","MyApp","dotnet",
            "ps","top","awk","grep","head","xclip"
        };

        // User-added excluded processes
        private readonly List<string> userExcluded = new List<string>();

        // C# Event for exclude list actions
        public event EventHandler<ExcludeEventArgs> OnExcludeEvent;

        private void RaiseExcludeEvent(string name, string action, int pid = 0)
        {
            OnExcludeEvent?.Invoke(this, new ExcludeEventArgs(name, action, pid));
        }

        // Check if process is in either list
        public bool IsExcluded(string name)
        {
            return defaultExcluded.Contains(name) || userExcluded.Contains(name);
        }

        // Add process to exclude list
        public void AddToExcludeList(string processName)
        {
            if (!userExcluded.Contains(processName))
            {
                userExcluded.Add(processName);
                RaiseExcludeEvent(processName, "ADDED");
            }
            else
            {
                Console.WriteLine($"'{processName}' is already in the exclude list.");
            }
        }

        // Remove process from exclude list (only user-added)
        public void RemoveFromExcludeList(string processName)
        {
            if (defaultExcluded.Contains(processName))
            {
                Console.WriteLine($"Cannot remove '{processName}' — it is a system-protected process.");
                return;
            }

            if (userExcluded.Remove(processName))
            {
                RaiseExcludeEvent(processName, "REMOVED");
            }
            else
            {
                Console.WriteLine($"'{processName}' is not in the exclude list.");
            }
        }

        // Get full exclude list
        public List<string> GetExcludeList()
        {
            var all = new List<string>(defaultExcluded);
            all.AddRange(userExcluded);
            return all;
        }

        private string RunCommand(string command)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = "-c \"" + command.Replace("\"","\\\"") + "\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            var process = Process.Start(psi);

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();

            process.WaitForExit();

            if(!string.IsNullOrEmpty(error))
                Console.WriteLine("[ERROR] " + error);

            return output.Trim();
        }

        private string RunSudo(string cmd)
        {
            return RunCommand("sudo " + cmd);
        }

        // FIXED controller activation
        private void EnableControllers()
        {
            RunSudo("bash -c \"echo '+cpu +memory' > /sys/fs/cgroup/cgroup.subtree_control 2>/dev/null\"");
        }

        private List<int> GetUserPids(string username)
        {
            string output = RunSudo($"pgrep -u {username}");

            if(string.IsNullOrEmpty(output))
                return new List<int>();

            return output.Split('\n').Select(int.Parse).ToList();
        }

        private List<int> GetPidsByName(string name, string username = null)
        {
            // If username is provided, filter by user using pgrep -u
            string command = string.IsNullOrEmpty(username)
                ? $"pgrep {name}"
                : $"pgrep -u {username} {name}";

            string output = RunSudo(command);

            if(string.IsNullOrEmpty(output))
                return new List<int>();

            return output.Split('\n').Select(int.Parse).ToList();
        }

        private string GetProcessName(int pid)
        {
            return RunSudo($"ps -p {pid} -o comm=");
        }

        // Get current CPU usage percentage of a process
        private double GetProcessCpuUsage(int pid)
        {
            string output = RunSudo($"ps -p {pid} -o %cpu=");
            if (double.TryParse(output.Trim(), out double cpu))
                return cpu;
            return 0.0;
        }

        // Get current memory usage in bytes of a process
        private long GetProcessMemoryUsage(int pid)
        {
            // RSS is in KB from ps
            string output = RunSudo($"ps -p {pid} -o rss=");
            if (long.TryParse(output.Trim(), out long rssKb))
                return rssKb * 1024; // convert KB to bytes
            return 0;
        }

        public void RestrictProcessCPU(int pid, string name, double cpuUsage)
        {
            if(IsExcluded(name)) return;

            EnableControllers();

            string path=$"/sys/fs/cgroup/limit_{pid}";

            RunSudo($"mkdir -p {path}");

            // Dynamically set CPU limit to 50% of current usage (min 5%, max 80%)
            double limitPercent = Math.Max(5, Math.Min(80, cpuUsage * 0.5));
            int quota = (int)(limitPercent * 1000); // quota in microseconds per 100ms period
            RunSudo($"bash -c \"echo '{quota} 100000' > {path}/cpu.max\"");

            RunSudo($"bash -c \"echo {pid} > {path}/cgroup.procs\"");

            Console.WriteLine($"[CPU RESTRICTED] PID {pid} — Usage: {cpuUsage}% → Limit: {limitPercent}%");
        }

        public void RestrictProcessMemory(int pid, string name, long memUsage)
        {
            if(IsExcluded(name)) return;

            EnableControllers();

            string path=$"/sys/fs/cgroup/limit_{pid}";

            RunSudo($"mkdir -p {path}");

            // Dynamically set memory limit to 50% of current usage (min 50MB, max 1GB)
            long minLimit = 50L * 1024 * 1024;       // 50MB
            long maxLimit = 1024L * 1024 * 1024;      // 1GB
            long memLimit = Math.Max(minLimit, Math.Min(maxLimit, memUsage / 2));
            RunSudo($"bash -c \"echo {memLimit} > {path}/memory.max\"");

            RunSudo($"bash -c \"echo {pid} > {path}/cgroup.procs\"");

            Console.WriteLine($"[MEMORY RESTRICTED] PID {pid} — Usage: {memUsage / (1024*1024)}MB → Limit: {memLimit / (1024*1024)}MB");
        }

        public void RestrictProcessResources(int pid,string name)
        {
            if (IsExcluded(name)) return;
            
            // Get actual CPU and memory usage of the process
            double cpuUsage = GetProcessCpuUsage(pid);
            long memUsage = GetProcessMemoryUsage(pid);

            // Pass CPU and memory usage to restriction methods
            RestrictProcessCPU(pid, name, cpuUsage);
            RestrictProcessMemory(pid, name, memUsage);

            Console.WriteLine($"[CPU + MEMORY RESTRICTED] PID {pid}");
        }

        // Restrict all instances of a process (system-wide)
        public void RestrictAllProcessesByName(string name)
        {
            var pids=GetPidsByName(name);

            foreach(var pid in pids)
                RestrictProcessResources(pid,name);
        }

        // Restrict all instances of a process for a specific user
        public void RestrictAllProcessesByName(string name, string username)
        {
            var pids=GetPidsByName(name, username);

            foreach(var pid in pids)
                RestrictProcessResources(pid,name);

            Console.WriteLine($"[RESTRICTED] All '{name}' processes for user '{username}'");
        }

        public void RemoveRestriction(int pid)
        {
            RunSudo($"bash -c \"echo {pid} > /sys/fs/cgroup/cgroup.procs\"");
            RunSudo($"rm -rf /sys/fs/cgroup/limit_{pid}");

            Console.WriteLine($"[RESTRICTION REMOVED] PID {pid}");
        }

        public void SuspendProcess(int pid)
        {
            string name = GetProcessName(pid);

            if(IsExcluded(name)) // if part of exclude do not perform any operation
            {
                // Fire BLOCKED event
                RaiseExcludeEvent(name, "BLOCKED", pid);
                return;
            }

            RunSudo($"kill -STOP {pid}");
            Console.WriteLine($"[SUSPENDED] {pid}");
        }

        public void SuspendProcess(string username)
        {
            foreach(var pid in GetUserPids(username))
                SuspendProcess(pid);
        }

        public void ResumeProcess(int pid)
        {
            string name = GetProcessName(pid);

            if(IsExcluded(name)) // For exclude do not perform any operation
            {
                // Fire BLOCKED event
                RaiseExcludeEvent(name, "BLOCKED", pid);
                return;
            }

            RunSudo($"kill -CONT {pid}");
            Console.WriteLine($"[RESUMED] {pid}");
        }

        public void ResumeProcess(string username)
        {
            foreach(var pid in GetUserPids(username))
                ResumeProcess(pid);
        }

        public void BlockProcess(string name)
        {
            if(IsExcluded(name))
            {
                RaiseExcludeEvent(name, "BLOCKED");
                return;
            }

            RunSudo($"pkill -STOP {name}");
            Console.WriteLine($"[BLOCKED] {name}");
        }

        public void BlockProcessForUser(string name,string user)
        {
            if(IsExcluded(name))
            {
                RaiseExcludeEvent(name, "BLOCKED");
                return;
            }

            RunSudo($"pkill -STOP -u {user} {name}");
            Console.WriteLine($"[BLOCKED] {name} for user {user}");
        }

        public void UnblockProcess(string name)
        {
            RunSudo($"pkill -CONT {name}");
            Console.WriteLine($"[UNBLOCKED] {name}");
        }

        public void UnblockProcessForUser(string name, string user)
        {
            RunSudo($"pkill -CONT -u {user} {name}");
            Console.WriteLine($"[UNBLOCKED] {name} for user {user}");
        }

        // Reduce priority of a process (nice value 10 = lower priority)
        public void ReducePriority(int pid)
        {
            string name = GetProcessName(pid);

            if(IsExcluded(name))
            {
                RaiseExcludeEvent(name, "BLOCKED", pid);
                return;
            }

            RunSudo($"renice 10 -p {pid}");
            Console.WriteLine($"[PRIORITY REDUCED] PID {pid} ({name}) — Nice set to 10");
        }

        // Reduce priority of all processes by name
        public void ReducePriorityByName(string name)
        {
            if(IsExcluded(name))
            {
                RaiseExcludeEvent(name, "BLOCKED");
                return;
            }

            var pids = GetPidsByName(name);
            foreach(var pid in pids)
            {
                RunSudo($"renice 10 -p {pid}");
                Console.WriteLine($"[PRIORITY REDUCED] PID {pid} ({name}) — Nice set to 10");
            }
        }

        // Restore priority back to normal (nice value 0)
        public void RestorePriority(int pid)
        {
            RunSudo($"renice 0 -p {pid}");
            Console.WriteLine($"[PRIORITY RESTORED] PID {pid} — Nice set to 0");
        }

        // Release free memory back to the OS without crashing the process
        // Uses gdb to call malloc_trim(0) safely in the target process
        public void ReleaseMemory(int pid)
        {
            string name = GetProcessName(pid);

            if (IsExcluded(name))
            {
                RaiseExcludeEvent(name, "BLOCKED", pid);
                return;
            }

            // Record memory before
            long memBefore = GetProcessMemoryUsage(pid) / (1024 * 1024);

            // Use gdb to attach, call malloc_trim(0) to release free heap memory to OS, and detach
            string gdbCmd = $"gdb -p {pid} -batch -ex \"call malloc_trim(0)\" 2>/dev/null";
            RunSudo(gdbCmd);

            // Record memory after
            long memAfter = GetProcessMemoryUsage(pid) / (1024 * 1024);

            Console.WriteLine($"[MEMORY RELEASED] PID {pid} ({name}) — Freed {memBefore - memAfter} MB (Now: {memAfter} MB)");
        }

        public void ReleaseMemoryByName(string name)
        {
            if (IsExcluded(name))
            {
                RaiseExcludeEvent(name, "BLOCKED");
                return;
            }

            var pids = GetPidsByName(name);
            foreach (var pid in pids)
            {
                ReleaseMemory(pid);
            }
        }

        // =============== CLIPBOARD HOOK (Real-Time, Event-Driven) ===============

        // Background process running clipnotify for real-time events
        private Process _clipnotifyProcess;
        private Thread _clipboardMonitorThread;
        private volatile bool _monitorRunning;

        // C# event — fired in real-time when clipboard content changes (not a loop, event-driven)
        public event EventHandler<string> OnClipboardChanged;

        // Read current clipboard text (full clipboard, not selection)
        public string GetClipboard()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "xclip",
                    Arguments = "-selection clipboard -o",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                };

                using (var process = Process.Start(psi))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    return output.TrimEnd('\r', '\n');
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CLIPBOARD ERROR] {ex.Message} - Is xclip installed and GUI running?");
                return "";
            }
        }

        // Write text to clipboard — replaces FULL clipboard content (no wait time)
        public void SetClipboard(string text)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "xclip",
                    Arguments = "-selection clipboard -i",
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                };

                using (var process = Process.Start(psi))
                {
                    // Write the FULL text to clipboard — replaces everything
                    process.StandardInput.Write(text);
                    process.StandardInput.Close();

                    // No wait time — xclip forks to background to serve clipboard to X server
                    // Previous 200ms wait removed as per manager instruction
                    process.WaitForExit();

                    Console.WriteLine($"[CLIPBOARD SET] Full clipboard replaced: \"{text}\"");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CLIPBOARD ERROR] {ex.Message}");
            }
        }

        // Clear clipboard
        public void ClearClipboard()
        {
            SetClipboard("");
            Console.WriteLine("[CLIPBOARD CLEARED]");
        }

        // Find and replace text in clipboard — the "hook" that modifies clipboard content
        // Reads full clipboard, replaces text, writes back full clipboard
        public void ReplaceClipboardText(string find, string replace)
        {
            string current = GetClipboard();

            if (string.IsNullOrEmpty(current))
            {
                Console.WriteLine("[CLIPBOARD] Clipboard is empty, nothing to replace.");
                return;
            }

            if (!current.Contains(find))
            {
                Console.WriteLine($"[CLIPBOARD] Text \"{find}\" not found in clipboard.");
                return;
            }

            // Replace and set FULL clipboard (not partial, entire content is replaced)
            string modified = current.Replace(find, replace);
            SetClipboard(modified);
            Console.WriteLine($"[CLIPBOARD HOOKED] Replaced \"{find}\" with \"{replace}\"");
        }

        // =============== REAL-TIME CLIPBOARD MONITOR ===============
        // Uses clipnotify which internally uses X11 XFixes extension
        // XFixesSelectSelectionInput with XFixesSetSelectionOwnerNotifyMask
        // Blocks on XNextEvent — fires ONLY when clipboard changes (NOT a polling loop)

        // Start real-time clipboard monitoring (background thread, event-driven)
        public void StartClipboardMonitor()
        {
            if (_monitorRunning)
            {
                Console.WriteLine("[CLIPBOARD MONITOR] Already running.");
                return;
            }

            _monitorRunning = true;

            _clipboardMonitorThread = new Thread(() =>
            {
                Console.WriteLine("[CLIPBOARD MONITOR] Started — listening for clipboard events (XFixes)...");
                Console.WriteLine("[CLIPBOARD MONITOR] Copy anything to see real-time notifications.\n");

                while (_monitorRunning)
                {
                    try
                    {
                        // clipnotify blocks until clipboard ownership changes (XNextEvent)
                        // Using -s clipboard to monitor CLIPBOARD selection only
                        var psi = new ProcessStartInfo
                        {
                            FileName = "clipnotify",
                            Arguments = "-s clipboard",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false
                        };

                        _clipnotifyProcess = Process.Start(psi);

                        // This BLOCKS until clipboard changes — not a loop, it's event-driven
                        // clipnotify uses XFixesSelectSelectionInput + XNextEvent internally
                        _clipnotifyProcess.WaitForExit();

                        if (!_monitorRunning) break;

                        // Clipboard changed! Read the new content
                        string newContent = GetClipboard();

                        // Fire the C# event (real-time callback)
                        OnClipboardChanged?.Invoke(this, newContent);
                    }
                    catch (Exception ex)
                    {
                        if (_monitorRunning)
                        {
                            Console.WriteLine($"[CLIPBOARD MONITOR ERROR] {ex.Message}");
                            Console.WriteLine("[CLIPBOARD MONITOR] Is clipnotify installed? Run setup.sh to install it.");
                            break;
                        }
                    }
                }

                Console.WriteLine("[CLIPBOARD MONITOR] Stopped.");
            });

            _clipboardMonitorThread.IsBackground = true;
            _clipboardMonitorThread.Start();
        }

        // Stop the real-time clipboard monitor
        public void StopClipboardMonitor()
        {
            if (!_monitorRunning)
            {
                Console.WriteLine("[CLIPBOARD MONITOR] Not running.");
                return;
            }

            _monitorRunning = false;

            // Kill the clipnotify process to unblock the waiting thread
            try
            {
                if (_clipnotifyProcess != null && !_clipnotifyProcess.HasExited)
                {
                    _clipnotifyProcess.Kill();
                    _clipnotifyProcess.Dispose();
                    _clipnotifyProcess = null;
                }
            }
            catch { }

            Console.WriteLine("[CLIPBOARD MONITOR] Stopping...");
        }
    }
}
