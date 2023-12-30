using System;
using System.Threading;

namespace Adamantium.Engine.Graphics;

public class SyncObject : IDisposable
{
    private Mutex _mutex;
    
    private string SyncObjectName { get; }

    public SyncObject(string name)
    {
        SyncObjectName = name;
        if (!Mutex.TryOpenExisting(name, out _mutex))
        {
            _mutex = new Mutex(false, name);
        }
    }

    public void Wait()
    {
        _mutex.WaitOne();
    }

    public void Release()
    {
        _mutex.ReleaseMutex();
    }

    public void Dispose()
    {
        _mutex?.Dispose();
    }
}