namespace App.Net
{
    public interface IMemoryReleaseService
    {
        void ReleaseMemory(int pid);
        void ReleaseMemoryByName(string processname);
    }
}
