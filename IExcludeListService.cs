using System;
using System.Collections.Generic;

namespace App.Net
{
    // Event args for exclude list events
    public class ExcludeEventArgs : EventArgs
    {
        public string ProcessName { get; set; }
        public string Action { get; set; } // "ADDED", "REMOVED", "BLOCKED"
        public int PID { get; set; }

        public ExcludeEventArgs(string name, string action, int pid = 0)
        {
            ProcessName = name;
            Action = action;
            PID = pid;
        }
    }

    public interface IExcludeListService
    {
        event EventHandler<ExcludeEventArgs> OnExcludeEvent;

        void AddToExcludeList(string processName);
        void RemoveFromExcludeList(string processName);
        List<string> GetExcludeList();
        bool IsExcluded(string processName);
    }
}
