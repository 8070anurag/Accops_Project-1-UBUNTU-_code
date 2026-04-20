namespace App.Net
{
    public interface IPriorityService
    {
        void ReducePriority(int pid);
        void ReducePriorityByName(string processname);
        void RestorePriority(int pid);
    }
}
