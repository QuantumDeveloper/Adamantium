using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.EffectsFramework;
using Serilog;

namespace Adamantium.Graphics;

/// <summary>
/// Compiles every shader the application owns BEFORE it opens anything, in a THROWAWAY child process, starting another
/// one whenever that child dies. Creating a shader from SPIR-V runs the driver's NVVM compiler in-process and
/// intermittently access-violates; that is a corrupted-state exception, so nothing INSIDE the process can catch it -
/// only a parent can make another attempt. Every attempt persists what it did compile (see <see cref="ShaderBinaryCache"/>),
/// so the retries converge (measured on 596.97 / Quadro RTX 4000: ~7 attempts from an empty cache, sometimes stalling
/// for six in a row on one shader).
/// </summary>
/// <remarks>
/// Why the whole effect list rather than "restart until a window appears": effects are constructed lazily - the font
/// effect on the first text draw, others on first use - so a launch can get its window up and die a minute later on a
/// shader nobody had touched yet. Compiling the enumerated set is what makes "compiled" mean compiled.
/// <para>The stamp lives in the cache folder, which is keyed by GPU + DRIVER VERSION, so a driver update invalidates it
/// by itself and an already-compiled launch costs one File.Exists - no child process, no device work.</para>
/// <para>This is a workaround for a driver defect, not a fix, and it only covers the crash WHILE CREATING a shader. A
/// shader that is created successfully but compiled wrongly (a GPU fault, VK_ERROR_DEVICE_LOST) is a different failure
/// and precompiling cannot help it.</para>
/// </remarks>
public static class ShaderPrecompiler
{
    /// <summary>Master switch (off = the earlier behaviour: cold launches die until the cache happens to fill).</summary>
    public static bool Enabled = true;

    /// <summary>Marks the child process whose whole job is to compile and persist, then exit.</summary>
    public const string PassArgument = "--precompile-shaders";

    private const int MaxAttempts = 25;
    private const int MaxAttemptsWithoutProgress = 3;   // no longer flake - something is permanently wrong
    private static readonly TimeSpan AttemptTimeout = TimeSpan.FromMinutes(2);

    /// <summary>True in the child: this process must compile the shaders and exit, not run an application.</summary>
    public static bool IsCompilePass => Environment.GetCommandLineArgs().Contains(PassArgument);

    /// <summary>Called once the graphics device exists and BEFORE anything is shown. In the child this compiles and
    /// terminates the process; in a normal launch it returns as soon as the cache is known complete.</summary>
    public static void EnsureCompiled(GraphicsDevice device)
    {
        if (device == null || !Enabled || !ShaderBinaryCache.Enabled) return;

        if (IsCompilePass)
        {
            RunCompilePass(device);   // exits the process
            return;
        }

        var stamp = StampFile(device);
        if (stamp == null || File.Exists(stamp)) return;

        var folder = Path.GetDirectoryName(stamp)!;
        var compiled = CachedCount(folder);
        var withoutProgress = 0;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            Log.Logger.Information($"Shader cache is cold for this driver - precompile attempt {attempt} ({compiled} shaders cached)");
            if (RunChild() && File.Exists(stamp))
            {
                Log.Logger.Information($"Shaders precompiled in {attempt} attempt(s): {CachedCount(folder)} shaders");
                return;
            }

            // A dead child still made progress if it persisted something new; only a run that adds NOTHING counts
            // against us, because that is the shape of a permanent failure rather than the driver's flake.
            var now = CachedCount(folder);
            if (now > compiled)
            {
                compiled = now;
                withoutProgress = 0;
                continue;
            }

            if (++withoutProgress >= MaxAttemptsWithoutProgress) break;
        }

        Log.Logger.Warning(
            $"Shader precompile gave up after {compiled} shaders - starting anyway; the first launches may still fail while creating shaders");
    }

    /// <summary>The child: create every effect there is, so every shader object it holds is compiled and persisted.
    /// Exit code 0 means this process REACHED THE END of the list - which is the whole question, since the fault we are
    /// working around kills the process outright rather than throwing.</summary>
    private static void RunCompilePass(GraphicsDevice device)
    {
        var created = 0;
        var skipped = 0;

        // Effects belong to a RENDER device - it is the only kind that carries an effect pool (a resource-loading
        // device has none, and constructing an effect against one just throws). This is a throwaway process, so an
        // extra render device costs nothing.
        var target = device.MainDevice.CreateRenderDevice();

        foreach (var type in EffectTypes())
        {
            try
            {
                // Generated effects all take (IGraphicsDevice, EffectPool = null) and compile their shaders in the ctor.
                // A pool EACH: one pool refuses to hold two effects sharing a global shader name, and which effects
                // collide is none of this pass's business.
                (Activator.CreateInstance(type, target, EffectPool.New(target)) as IDisposable)?.Dispose();
                created++;
            }
            catch (Exception e)
            {
                // A managed exception is DETERMINISTIC: the driver fault we are here for kills the process, it never
                // throws. So this effect will fail identically on every retry - count it as uncompilable, not as a
                // failed attempt, or one such effect would keep the cache "cold" forever and re-run this on every launch.
                skipped++;
                // Reflection wraps whatever the ctor threw; the wrapper's message says nothing about the real fault.
                var cause = (e as TargetInvocationException)?.InnerException ?? e;
                Log.Logger.Warning($"Shader precompile skipped {type.Name}: {cause.GetType().Name}: {cause.Message}");
            }
        }

        // Reaching here at all means the process survived the whole list - that is what "precompiled" means.
        var stamped = false;
        if (created > 0)
        {
            try
            {
                var stamp = StampFile(device);
                Directory.CreateDirectory(Path.GetDirectoryName(stamp)!);
                File.WriteAllText(stamp, $"{created} effects precompiled ({skipped} skipped) at {DateTime.UtcNow:O}");
                stamped = true;
            }
            catch
            {
                // No stamp = not compiled, whatever went through: the parent must not be told otherwise.
            }
        }

        Environment.Exit(stamped ? 0 : 1);
    }

    private static bool RunChild()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) return false;

            var info = new ProcessStartInfo { FileName = exe, UseShellExecute = false };
            // Launched through the SDK host (dotnet run / dotnet app.dll), the process path is dotnet itself - it needs
            // the assembly back before any argument of ours.
            if (Path.GetFileNameWithoutExtension(exe).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            {
                var entry = Assembly.GetEntryAssembly()?.Location;
                if (string.IsNullOrEmpty(entry)) return false;
                info.ArgumentList.Add(entry);
            }
            info.ArgumentList.Add(PassArgument);

            using var child = Process.Start(info);
            if (child == null) return false;

            if (!child.WaitForExit((int)AttemptTimeout.TotalMilliseconds))
            {
                child.Kill(entireProcessTree: true);
                return false;
            }

            return child.ExitCode == 0;
        }
        catch (Exception e)
        {
            Log.Logger.Warning($"Shader precompile could not start a child process: {e.Message}");
            return false;
        }
    }

    // Every generated effect, from every Adamantium assembly the application pulls in. Enumerated rather than listed:
    // effects are code-generated per .fx file, so a written-down list would go stale the moment one is added.
    private static IEnumerable<Type> EffectTypes()
    {
        LoadAdamantiumAssemblies();

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.GetName().Name?.StartsWith("Adamantium", StringComparison.Ordinal) != true) continue;

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                types = e.Types.Where(t => t != null).ToArray()!;
            }

            foreach (var type in types)
            {
                if (type.IsAbstract || !typeof(Effect).IsAssignableFrom(type)) continue;
                if (type.Namespace?.EndsWith(".Effects.Generated", StringComparison.Ordinal) != true) continue;
                yield return type;
            }
        }
    }

    // An assembly is only loaded once something touches it, and the point of precompiling is that nothing has yet.
    private static void LoadAdamantiumAssemblies()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<Assembly>();

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            queue.Enqueue(assembly);
        }

        while (queue.Count > 0)
        {
            foreach (var reference in queue.Dequeue().GetReferencedAssemblies())
            {
                if (reference.Name?.StartsWith("Adamantium", StringComparison.Ordinal) != true) continue;
                if (!seen.Add(reference.FullName)) continue;

                try
                {
                    queue.Enqueue(Assembly.Load(reference));
                }
                catch
                {
                    // A reference that cannot be loaded holds no effects we could compile either.
                }
            }
        }
    }

    private static string StampFile(GraphicsDevice device)
    {
        try
        {
            return Path.Combine(ShaderBinaryCache.DirectoryFor(device), "precompiled.stamp");
        }
        catch
        {
            return null;
        }
    }

    private static int CachedCount(string folder)
    {
        try
        {
            return Directory.Exists(folder) ? Directory.GetFiles(folder, "*.shaderbin").Length : 0;
        }
        catch
        {
            return 0;
        }
    }
}
