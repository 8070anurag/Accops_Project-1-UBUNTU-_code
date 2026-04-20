using System;
using System.Linq;
using System.Threading;
using App.Net;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("==========================================");
        Console.WriteLine("       === Linux Process Manager ===      ");
        Console.WriteLine("==========================================");
        Console.WriteLine();

        while (true)
        {
            Console.WriteLine("Choose an option:");
            Console.WriteLine("  1. Suspend and Resume Process Manually");
            Console.WriteLine("  2. System Monitoring (Auto-restrict)");
            Console.WriteLine("  3. Clipboard Hook (Real-Time)");
            Console.WriteLine("  4. Exit");
            Console.WriteLine();
            Console.Write("Enter choice (1/2/3/4): ");

            string choice = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(choice))
                continue;

            switch (choice.Trim())
            {
                case "1":
                    ManualMode();
                    break;

                case "2":
                    MonitoringMode();
                    break;

                case "3":
                    ClipboardHookMode();
                    break;

                case "4":
                    Console.WriteLine("Exiting...");
                    return;

                default:
                    Console.WriteLine("Invalid choice. Please enter 1, 2, 3, or 4.\n");
                    break;
            }
        }
    }

    // Event handler for exclude list events
    static void OnExcludeEvent(object sender, ExcludeEventArgs e)
    {
        switch (e.Action)
        {
            case "ADDED":
                Console.WriteLine($"[EVENT] Process '{e.ProcessName}' added to exclude list.");
                break;

            case "REMOVED":
                Console.WriteLine($"[EVENT] Process '{e.ProcessName}' removed from exclude list.");
                break;

            case "BLOCKED":
                Console.WriteLine(
                    $"[EVENT] BLOCKED — '{e.ProcessName}' (PID {e.PID}) is in the exclude list. Cannot suspend/resume.");
                break;
        }
    }

    // ========== OPTION 1: Manual Suspend / Resume ==========
    static void ManualMode()
    {
        IProcessService service = new ProcessService();
        service.OnExcludeEvent += OnExcludeEvent;

        Console.WriteLine("\n--- Manual Suspend / Resume Mode ---");
        Console.WriteLine("Type 'help' for commands, 'back' to return to menu.\n");

        while (true)
        {
            Console.Write("\nadmin@manual> ");

            string input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
                continue;

            var parts = input.Split(' ',
                StringSplitOptions.RemoveEmptyEntries);

            string command = parts[0].ToLower();

            try
            {
                switch (command)
                {
                    case "help":
                        Console.WriteLine("\nManual Mode Commands:\n");
                        Console.WriteLine("  suspend <pid>            - Suspend a process by PID");
                        Console.WriteLine("  suspend-user <username>  - Suspend all processes of a user");
                        Console.WriteLine("  resume <pid>             - Resume a process by PID");
                        Console.WriteLine("  resume-user <username>   - Resume all processes of a user");
                        Console.WriteLine("  exclude-add <name>       - Add process to exclude list");
                        Console.WriteLine("  exclude-remove <name>    - Remove process from exclude list");
                        Console.WriteLine("  exclude-list             - Show all excluded processes");
                        Console.WriteLine("  clip-replace <f> <r>     - Find and replace text in clipboard");
                        Console.WriteLine("  clip-get                 - Output current clipboard contents");
                        Console.WriteLine("  clip-clear               - Empty the clipboard");
                        Console.WriteLine("  clip-set <text>          - Overwrite the clipboard");
                        Console.WriteLine("  help                     - Show this help");
                        Console.WriteLine("  back                     - Return to main menu\n");
                        break;

                    case "suspend":
                        if (parts.Length < 2)
                        {
                            Console.WriteLine("Usage: suspend <pid>");
                            break;
                        }
                        service.SuspendProcess(int.Parse(parts[1]));
                        break;

                    case "suspend-user":
                        if (parts.Length < 2)
                        {
                            Console.WriteLine("Usage: suspend-user <username>");
                            break;
                        }
                        service.SuspendProcess(parts[1]);
                        break;

                    case "resume":
                        if (parts.Length < 2)
                        {
                            Console.WriteLine("Usage: resume <pid>");
                            break;
                        }
                        service.ResumeProcess(int.Parse(parts[1]));
                        break;

                    case "resume-user":
                        if (parts.Length < 2)
                        {
                            Console.WriteLine("Usage: resume-user <username>");
                            break;
                        }
                        service.ResumeProcess(parts[1]);
                        break;

                    // Clipboard commands
                    case "clip-get":
                        string clipText = service.GetClipboard();
                        if (string.IsNullOrEmpty(clipText))
                            Console.WriteLine("[CLIPBOARD] Empty");
                        else
                            Console.WriteLine($"[CLIPBOARD] {clipText}");
                        break;

                    case "clip-set":
                        if (parts.Length < 2)
                        {
                            Console.WriteLine("Usage: clip-set <text>");
                            break;
                        }
                        service.SetClipboard(string.Join(" ", parts.Skip(1)));
                        break;

                    case "clip-clear":
                        service.ClearClipboard();
                        break;

                    case "clip-replace":
                        if (parts.Length < 3)
                        {
                            Console.WriteLine("Usage: clip-replace <find> <replace>");
                            break;
                        }
                        service.ReplaceClipboardText(parts[1], parts[2]);
                        break;

                    // Exclude list commands
                    case "exclude-add":
                        if (parts.Length < 2)
                        {
                            Console.WriteLine("Usage: exclude-add <processname>");
                            break;
                        }
                        service.AddToExcludeList(parts[1]);
                        break;

                    case "exclude-remove":
                        if (parts.Length < 2)
                        {
                            Console.WriteLine("Usage: exclude-remove <processname>");
                            break;
                        }
                        service.RemoveFromExcludeList(parts[1]);
                        break;

                    case "exclude-list":
                        var list = service.GetExcludeList();
                        Console.WriteLine("\nExcluded Processes:");
                        foreach (var name in list)
                            Console.WriteLine($"  - {name}");
                        Console.WriteLine();
                        break;

                    case "back":
                        Console.WriteLine("Returning to main menu...\n");
                        return;

                    default:
                        Console.WriteLine("Unknown command. Type 'help' for available commands.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
        }
    }

    // ========== OPTION 2: System Monitoring (Auto-restrict) ==========
    static void MonitoringMode()
    {
        IProcessService service = new ProcessService();
        service.OnExcludeEvent += OnExcludeEvent;

        Console.WriteLine("\n--- System Monitoring Mode ---");
        Console.WriteLine("Auto-monitoring started in background.");
        Console.WriteLine("Type 'help' for commands, 'back' to return to menu.\n");

        // Start monitoring thread
        SystemMonitorService monitor =
            new SystemMonitorService(service);

        Thread monitorThread =
            new Thread(new ThreadStart(monitor.StartMonitoring));

        monitorThread.IsBackground = true;
        monitorThread.Start();

        // CLI LOOP
        while (true)
        {
            Console.Write("\nadmin@monitor> ");

            string input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
                continue;

            var parts = input.Split(' ',
                StringSplitOptions.RemoveEmptyEntries);

            string command = parts[0].ToLower();

            try
            {
                switch (command)
                {
                    case "help":
                        PrintMonitorHelp();
                        break;

                    // Suspend
                    case "suspend":
                        if (parts.Length < 2)
                        {
                            Console.WriteLine("Usage: suspend <pid>");
                            break;
                        }
                        service.SuspendProcess(int.Parse(parts[1]));
                        break;

                    case "suspend-user":
                        if (parts.Length < 2)
                        {
                            Console.WriteLine("Usage: suspend-user <username>");
                            break;
                        }
                        service.SuspendProcess(parts[1]);
                        break;

                    // Resume
                    case "resume":
                        if (parts.Length < 2)
                        {
                            Console.WriteLine("Usage: resume <pid>");
                            break;
                        }
                        service.ResumeProcess(int.Parse(parts[1]));
                        break;

                    case "resume-user":
                        if (parts.Length < 2)
                        {
                            Console.WriteLine("Usage: resume-user <username>");
                            break;
                        }
                        service.ResumeProcess(parts[1]);
                        break;

                    // Restrict CPU
                    case "restrict-cpu":
                        if (parts.Length < 3)
                        {
                            Console.WriteLine("Usage: restrict-cpu <pid> <processname>");
                            break;
                        }
                        {
                            int cpuPid = int.Parse(parts[1]);
                            // Get actual CPU usage and pass it
                            service.RestrictProcessCPU(
                                cpuPid,
                                parts[2],
                                0); // usage will be fetched inside RestrictProcessResources; for direct call pass 0 to use min limit
                        }
                        break;

                    // Restrict Memory
                    case "restrict-mem":
                        if (parts.Length < 3)
                        {
                            Console.WriteLine("Usage: restrict-mem <pid> <processname>");
                            break;
                        }
                        {
                            int memPid = int.Parse(parts[1]);
                            // Get actual memory usage and pass it
                            service.RestrictProcessMemory(
                                memPid,
                                parts[2],
                                0); // usage will be fetched inside RestrictProcessResources; for direct call pass 0 to use min limit
                        }
                        break;

                    // Restrict both (all instances system-wide)
                    case "restrict":
                        if (parts.Length < 2)
                        {
                            Console.WriteLine("Usage: restrict <processname>");
                            break;
                        }
                        service.RestrictAllProcessesByName(parts[1]);
                        break;

                    // Restrict per user
                    case "restrict-user":
                        if (parts.Length < 3)
                        {
                            Console.WriteLine("Usage: restrict-user <processname> <username>");
                            break;
                        }
                        service.RestrictAllProcessesByName(parts[1], parts[2]);
                        break;

                    // Remove restriction
                    case "remove-restrict":
                        if (parts.Length < 2)
                        {
                            Console.WriteLine("Usage: remove-restrict <pid>");
                            break;
                        }
                        service.RemoveRestriction(
                            int.Parse(parts[1]));
                        break;

                    // Block process
                    case "block-process":
                        if (parts.Length < 2)
                        {
                            Console.WriteLine("Usage: block-process <processname>");
                            break;
                        }
                        service.BlockProcess(parts[1]);
                        break;

                    // Block process for user
                    case "block-process-user":
                        if (parts.Length < 3)
                        {
                            Console.WriteLine("Usage: block-process-user <processname> <username>");
                            break;
                        }
                        service.BlockProcessForUser(
                            parts[1],
                            parts[2]);
                        break;

                    // Unblock process
                    case "unblock-process":
                        if (parts.Length < 2)
                        {
                            Console.WriteLine("Usage: unblock-process <processname>");
                            break;
                        }
                        service.UnblockProcess(parts[1]);
                        break;

                    // Unblock process for user
                    case "unblock-process-user":
                        if (parts.Length < 3)
                        {
                            Console.WriteLine("Usage: unblock-process-user <processname> <username>");
                            break;
                        }
                        service.UnblockProcessForUser(
                            parts[1],
                            parts[2]);
                        break;

                    // Reduce priority
                    case "reduce-priority":
                        if (parts.Length < 2)
                        {
                            Console.WriteLine("Usage: reduce-priority <pid>");
                            break;
                        }
                        service.ReducePriority(int.Parse(parts[1]));
                        break;

                    // Reduce priority by name
                    case "reduce-priority-name":
                        if (parts.Length < 2)
                        {
                            Console.WriteLine("Usage: reduce-priority-name <processname>");
                            break;
                        }
                        service.ReducePriorityByName(parts[1]);
                        break;

                    // Restore priority
                    case "restore-priority":
                        if (parts.Length < 2)
                        {
                            Console.WriteLine("Usage: restore-priority <pid>");
                            break;
                        }
                        service.RestorePriority(int.Parse(parts[1]));
                        break;

                    // Release Process Memory
                    case "release-memory":
                        if (parts.Length < 2)
                        {
                            Console.WriteLine("Usage: release-memory <pid>");
                            break;
                        }
                        service.ReleaseMemory(int.Parse(parts[1]));
                        break;

                    // Release Process Memory by Name
                    case "release-memory-name":
                        if (parts.Length < 2)
                        {
                            Console.WriteLine("Usage: release-memory-name <processname>");
                            break;
                        }
                        service.ReleaseMemoryByName(parts[1]);
                        break;

                    // Clipboard commands
                    case "clip-get":
                        string clipText = service.GetClipboard();
                        if (string.IsNullOrEmpty(clipText))
                            Console.WriteLine("[CLIPBOARD] Empty");
                        else
                            Console.WriteLine($"[CLIPBOARD] {clipText}");
                        break;

                    case "clip-set":
                        if (parts.Length < 2)
                        {
                            Console.WriteLine("Usage: clip-set <text>");
                            break;
                        }
                        service.SetClipboard(string.Join(" ", parts.Skip(1)));
                        break;

                    case "clip-clear":
                        service.ClearClipboard();
                        break;

                    case "clip-replace":
                        if (parts.Length < 3)
                        {
                            Console.WriteLine("Usage: clip-replace <find> <replace>");
                            break;
                        }
                        service.ReplaceClipboardText(parts[1], parts[2]);
                        break;

                    // Exclude list commands
                    case "exclude-add":
                        if (parts.Length < 2)
                        {
                            Console.WriteLine("Usage: exclude-add <processname>");
                            break;
                        }
                        service.AddToExcludeList(parts[1]);
                        break;

                    case "exclude-remove":
                        if (parts.Length < 2)
                        {
                            Console.WriteLine("Usage: exclude-remove <processname>");
                            break;
                        }
                        service.RemoveFromExcludeList(parts[1]);
                        break;

                    case "exclude-list":
                        var list = service.GetExcludeList();
                        Console.WriteLine("\nExcluded Processes:");
                        foreach (var name in list)
                            Console.WriteLine($"  - {name}");
                        Console.WriteLine();
                        break;

                    // Back to menu
                    case "back":
                        Console.WriteLine("Stopping monitor and returning to menu...\n");
                        return;

                    default:
                        Console.WriteLine("Unknown command. Type 'help' for commands.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
        }
    }

    static void PrintMonitorHelp()
    {
        Console.WriteLine("\nMonitoring Mode Commands:\n");

        Console.WriteLine("  suspend <pid>");
        Console.WriteLine("  suspend-user <username>");

        Console.WriteLine("  resume <pid>");
        Console.WriteLine("  resume-user <username>");

        Console.WriteLine("  restrict-cpu <pid> <processname>");
        Console.WriteLine("  restrict-mem <pid> <processname>");

        Console.WriteLine("  restrict <processname>");
        Console.WriteLine("  restrict-user <processname> <username>");

        Console.WriteLine("  remove-restrict <pid>");

        Console.WriteLine("  block-process <processname>");
        Console.WriteLine("  block-process-user <processname> <username>");
        Console.WriteLine("  unblock-process <processname>");
        Console.WriteLine("  unblock-process-user <processname> <username>");

        Console.WriteLine("  reduce-priority <pid>");
        Console.WriteLine("  reduce-priority-name <processname>");
        Console.WriteLine("  restore-priority <pid>");

        Console.WriteLine("  release-memory <pid>");
        Console.WriteLine("  release-memory-name <processname>");

        Console.WriteLine("  clip-get");
        Console.WriteLine("  clip-set <text>");
        Console.WriteLine("  clip-clear");
        Console.WriteLine("  clip-replace <find> <replace>");

        Console.WriteLine("  exclude-add <processname>");
        Console.WriteLine("  exclude-remove <processname>");
        Console.WriteLine("  exclude-list");

        Console.WriteLine("  help");
        Console.WriteLine("  back  - Return to main menu\n");
    }

    // ========== OPTION 3: Clipboard Hook (Real-Time) ==========
    static void ClipboardHookMode()
    {
        IProcessService service = new ProcessService();

        Console.WriteLine("\n--- Clipboard Hook Mode (Real-Time, Event-Driven) ---");
        Console.WriteLine("Using XFixes via clipnotify for real-time clipboard events.");
        Console.WriteLine("(Inspired by xrdp clipboard.c — XFixesSelectSelectionInput)\n");

        // Subscribe to the real-time clipboard change event
        // This is the CALLBACK — prints what was copied in real-time
        service.OnClipboardChanged += (sender, content) =>
        {
            Console.WriteLine();
            Console.WriteLine("╔══════════════════════════════════════════╗");
            Console.WriteLine("║     [CLIPBOARD COPIED] — Real-Time      ║");
            Console.WriteLine("╠══════════════════════════════════════════╣");
            if (string.IsNullOrEmpty(content))
                Console.WriteLine("║  (empty clipboard)                       ║");
            else
                Console.WriteLine($"║  Content: {content}");
            Console.WriteLine("╚══════════════════════════════════════════╝");
            Console.Write("\nadmin@clipboard> ");
        };

        // Start the real-time clipboard monitor
        service.StartClipboardMonitor();

        Console.WriteLine("Type 'help' for commands, 'back' to return to menu.\n");

        while (true)
        {
            Console.Write("\nadmin@clipboard> ");

            string input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
                continue;

            var parts = input.Split(' ',
                StringSplitOptions.RemoveEmptyEntries);

            string command = parts[0].ToLower();

            try
            {
                switch (command)
                {
                    case "help":
                        Console.WriteLine("\nClipboard Hook Mode Commands:\n");
                        Console.WriteLine("  clip-get                 - Show current clipboard content");
                        Console.WriteLine("  clip-set <text>          - Replace full clipboard with text");
                        Console.WriteLine("  clip-clear               - Clear the clipboard");
                        Console.WriteLine("  clip-replace <find> <r>  - Find and replace in clipboard");
                        Console.WriteLine("  help                     - Show this help");
                        Console.WriteLine("  back                     - Stop monitor & return to menu\n");
                        Console.WriteLine("Note: Copy anything in any app to see real-time notifications above.");
                        Console.WriteLine("When you paste (Ctrl+V), the FULL clipboard content is pasted.\n");
                        break;

                    case "clip-get":
                        string clipText = service.GetClipboard();
                        if (string.IsNullOrEmpty(clipText))
                            Console.WriteLine("[CLIPBOARD] Empty");
                        else
                            Console.WriteLine($"[CLIPBOARD] {clipText}");
                        break;

                    case "clip-set":
                        if (parts.Length < 2)
                        {
                            Console.WriteLine("Usage: clip-set <text>");
                            break;
                        }
                        // Set FULL clipboard — replaces everything
                        service.SetClipboard(string.Join(" ", parts.Skip(1)));
                        break;

                    case "clip-clear":
                        service.ClearClipboard();
                        break;

                    case "clip-replace":
                        if (parts.Length < 3)
                        {
                            Console.WriteLine("Usage: clip-replace <find> <replace>");
                            break;
                        }
                        service.ReplaceClipboardText(parts[1], parts[2]);
                        break;

                    case "back":
                        service.StopClipboardMonitor();
                        Console.WriteLine("Returning to main menu...\n");
                        return;

                    default:
                        Console.WriteLine("Unknown command. Type 'help' for available commands.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
        }
    }
}
