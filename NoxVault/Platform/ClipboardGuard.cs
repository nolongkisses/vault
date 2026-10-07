using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace NoxVault;

// Clears copied secrets after a delay, but only while the clipboard still holds what vault put there.
internal sealed class ClipboardGuard : IDisposable
{
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(30) };
    readonly Action<string> write;
    readonly Func<uint> getSequence;
    readonly Action clear;
    uint sequence;
    bool owned;

    internal ClipboardGuard() : this(Write, Native.GetClipboardSequenceNumber, Clipboard.Clear) { }

    internal ClipboardGuard(Action<string> write, Func<uint> getSequence, Action clear)
    {
        this.write = write;
        this.getSequence = getSequence;
        this.clear = clear;
        timer.Tick += (_, _) => Clear();
    }

    internal int ClearAfterSeconds { get; set; } = 30;

    static void Write(string value)
    {
        var data = new DataObject();
        data.SetData(DataFormats.UnicodeText, value);
        // Windows clipboard history / cloud clipboard opt-out formats (DWORD zero).
        data.SetData("CanIncludeInClipboardHistory", new MemoryStream(new byte[4]));
        data.SetData("CanUploadToCloudClipboard", new MemoryStream(new byte[4]));
        Clipboard.SetDataObject(data, true);
    }

    internal void Copy(string value)
    {
        write(value);
        sequence = getSequence();
        owned = true;
        timer.Stop();
        timer.Interval = TimeSpan.FromSeconds(ClearAfterSeconds is 15 or 30 or 60 ? ClearAfterSeconds : 30);
        timer.Start();
    }

    internal void Clear()
    {
        timer.Stop();
        if (!owned) return;
        try
        {
            if (sequence == getSequence()) clear();
            owned = false;
        }
        catch (COMException) { timer.Interval = TimeSpan.FromSeconds(2); timer.Start(); }
    }

    public void Dispose()
    {
        Clear();
        timer.Stop();
    }
}
