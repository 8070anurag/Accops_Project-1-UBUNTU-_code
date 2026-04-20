namespace App.Net
{
    public interface IResourceRestrictionService
    {
        void RestrictProcessCPU(int pid, string processname, double cpuUsage);
        void RestrictProcessMemory(int pid, string processname, long memUsage);

        void RestrictProcessResources(int pid, string processname);

        void RestrictAllProcessesByName(string processname);
        void RestrictAllProcessesByName(string processname, string username);

        void RemoveRestriction(int pid);
    }
}
