namespace App.Net
{
    public interface ISuspendResumeService
    {
        void SuspendProcess(int pid);
        void SuspendProcess(string username);

        void ResumeProcess(int pid);
        void ResumeProcess(string username);
    }
}
