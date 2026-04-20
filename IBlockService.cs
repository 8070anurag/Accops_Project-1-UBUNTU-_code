namespace App.Net
{
    public interface IBlockService
    {
        void BlockProcess(string processname);
        void BlockProcessForUser(string processname, string username);

        void UnblockProcess(string processname);
        void UnblockProcessForUser(string processname, string username);
    }
}
