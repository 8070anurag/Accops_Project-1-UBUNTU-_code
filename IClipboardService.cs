using System;

namespace App.Net
{
    public interface IClipboardService
    {
        string GetClipboard();
        void SetClipboard(string text);
        void ClearClipboard();
        void ReplaceClipboardText(string find, string replace);

        // Real-time clipboard monitoring (event-driven using XFixes via clipnotify)
        void StartClipboardMonitor();
        void StopClipboardMonitor();

        // C# event fired in real-time whenever clipboard content changes
        event EventHandler<string> OnClipboardChanged;
    }
}
