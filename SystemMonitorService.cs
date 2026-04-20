using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace App.Net
{
    public class SystemMonitorService
    {
        private IProcessService service;

        // Correct thresholds
        private const double CPU_THRESHOLD = 80;
        private const double MEMORY_THRESHOLD = 80;

        // Track CPU-restricted and MEM-restricted separately
        private HashSet<string> cpuRestricted = new HashSet<string>();
        private HashSet<string> memRestricted = new HashSet<string>();

        public SystemMonitorService(IProcessService s)
        {
            service = s;
        }

        public void StartMonitoring()
        {
            Console.WriteLine("[MONITOR STARTED]");

            while (true)
            {
                try
                {
                    double cpu = GetCPUUsage();
                    double mem = GetMemoryUsage();

                    Console.WriteLine($"CPU: {cpu:F1}% | MEMORY: {mem:F1}%");

                    var processes = GetProcesses();

                    // Handle CPU independently
                    if (cpu > CPU_THRESHOLD)
                    {
                        Console.WriteLine("[ALERT] High CPU usage detected!");

                        foreach (var p in processes)
                        {
                            if (p.CPU > 20 && !cpuRestricted.Contains(p.Name) && !service.IsExcluded(p.Name))
                            {
                                Console.WriteLine(
                                    $"[CPU RESTRICT] {p.Name} (PID:{p.PID}) CPU:{p.CPU}%");

                                service.RestrictAllProcessesByName(p.Name);
                                cpuRestricted.Add(p.Name);
                            }
                        }
                    }
                    else
                    {
                        // CPU dropped below threshold, clear so it can re-trigger
                        if (cpuRestricted.Count > 0)
                        {
                            Console.WriteLine("[INFO] CPU usage normal. Clearing CPU restriction tracking.");
                            cpuRestricted.Clear();
                        }
                    }

                    // Handle Memory independently
                    if (mem > MEMORY_THRESHOLD)
                    {
                        Console.WriteLine("[ALERT] High Memory usage detected!");

                        foreach (var p in processes)
                        {
                            if (p.Memory > 20 && !memRestricted.Contains(p.Name) && !service.IsExcluded(p.Name))
                            {
                                Console.WriteLine(
                                    $"[MEM RESTRICT] {p.Name} (PID:{p.PID}) MEM:{p.Memory}%");

                                service.RestrictAllProcessesByName(p.Name);
                                memRestricted.Add(p.Name);
                            }
                        }
                    }
                    else
                    {
                        // Memory dropped below threshold, clear so it can re-trigger
                        if (memRestricted.Count > 0)
                        {
                            Console.WriteLine("[INFO] Memory usage normal. Clearing Memory restriction tracking.");
                            memRestricted.Clear();
                        }
                    }

                    Thread.Sleep(5000);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Monitor error: " + ex.Message);
                }
            }
        }

        // FIXED CPU detection (correct calculation)
        private double GetCPUUsage()
        {
            string output =
                Run("top -bn1 | grep Cpu | awk '{print 100 - $8}'");

            double.TryParse(output, out double cpu);

            return cpu;
        }

        private double GetMemoryUsage()
        {
            // free output: Mem: total used free shared buff/cache available
            // Index:         0    1    2    3      4        5         6
            // Use 'available' (index 6) for accurate real usage
            var parts = Run("free | grep Mem")
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);

            double total = double.Parse(parts[1]);
            double available = double.Parse(parts[6]);

            return (total - available) / total * 100;
        }

        // Detect both CPU and memory heavy processes
        private List<ProcessInfo> GetProcesses()
        {
            var list = new List<ProcessInfo>();

            var lines =
                Run("ps -eo pid,comm,%cpu,%mem --sort=-%cpu | head -n 15")
                .Split('\n')
                .Skip(1);

            foreach (var line in lines)
            {
                var p =
                    line.Split(' ',
                    StringSplitOptions.RemoveEmptyEntries);

                if (p.Length >= 4)
                {
                    list.Add(new ProcessInfo
                    {
                        PID = int.Parse(p[0]),
                        Name = p[1],
                        CPU = double.Parse(p[2]),
                        Memory = double.Parse(p[3])
                    });
                }
            }

            return list;
        }

        private string Run(string cmd)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = "-c \"" + cmd + "\"",
                RedirectStandardOutput = true,
                UseShellExecute = false
            };

            var process = Process.Start(psi);

            string result =
                process.StandardOutput.ReadToEnd();

            process.WaitForExit();

            return result.Trim();
        }
    }

    public class ProcessInfo
    {
        public int PID;
        public string Name;
        public double CPU;
        public double Memory;
    }
}
