using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Adamantium.GameInput.Interop;
using Adamantium.Multiverse.Input;
using EngineGamepad = Adamantium.Multiverse.Input.Gamepad;

namespace Adamantium.GameInput;

/// <summary>
/// Gamepads through Microsoft GameInput: Xbox gamepads with the Elite paddles and the Share button, PlayStation gamepads
/// and the others GameInput maps to a gamepad. Arrivals and removals come on GameInput's own thread and are taken in on
/// <see cref="Update"/>.
/// </summary>
public sealed unsafe class GameInputGamepadBackend : IGamepadBackend, IDisposable
{
    private readonly object sync = new();
    private readonly ConcurrentQueue<(nint Device, bool Connected)> changes = new();
    private readonly List<GameInputGamepad> gamepads = [];
    private readonly GameInputContext context;
    private GCHandle self;
    private EngineGamepad[] connected = [];
    private bool disposed;

    private GameInputGamepadBackend(GameInputContext context)
    {
        this.context = context;
    }

    /// <summary>Starts GameInput. False when it is not installed or gameinput-c-shared is missing - then another backend
    /// has to do.</summary>
    public static bool TryCreate(out GameInputGamepadBackend backend)
    {
        backend = null;
        try
        {
            if (GameInputContext.Create(out var context) < 0)
            {
                return false;
            }

            var created = new GameInputGamepadBackend(context);
            if (!created.Watch())
            {
                created.Dispose();
                return false;
            }

            backend = created;
            return true;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    public IReadOnlyList<EngineGamepad> Gamepads => Volatile.Read(ref connected);

    public void Update()
    {
        lock (sync)
        {
            var changed = false;
            while (changes.TryDequeue(out var change))
            {
                changed |= change.Connected ? Arrive(change.Device) : Leave(change.Device);
            }

            if (changed)
            {
                Volatile.Write(ref connected, gamepads.ToArray<EngineGamepad>());
            }
        }
    }

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            context.Release();
            foreach (var gamepad in gamepads)
            {
                gamepad.Release();
            }

            gamepads.Clear();
            while (changes.TryDequeue(out var change))
            {
                if (change.Connected)
                {
                    GameInputInterop.gameinputc_device_release(new GameInputDeviceT { pointer = (void*)change.Device });
                }
            }

            Volatile.Write(ref connected, []);
            if (self.IsAllocated)
            {
                self.Free();
            }
        }
    }

    private bool Watch()
    {
        self = GCHandle.Alloc(this);
        delegate* unmanaged[Stdcall]<void*, void*, int, void> callback = &OnDevice;
        return context.WatchGamepads((nuint)callback, (nuint)(nint)GCHandle.ToIntPtr(self)) >= 0;
    }

    private bool Arrive(nint device)
    {
        gamepads.Add(new GameInputGamepad(context, new GameInputDeviceT { pointer = (void*)device }));
        return true;
    }

    private bool Leave(nint device)
    {
        var index = gamepads.FindIndex(gamepad => gamepad.Handle == device);
        if (index < 0)
        {
            return false;
        }

        gamepads[index].Release();
        gamepads.RemoveAt(index);
        return true;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static void OnDevice(void* userData, void* device, int connected)
    {
        if (GCHandle.FromIntPtr((nint)userData).Target is GameInputGamepadBackend backend)
        {
            backend.changes.Enqueue(((nint)device, connected != 0));
        }
    }
}
