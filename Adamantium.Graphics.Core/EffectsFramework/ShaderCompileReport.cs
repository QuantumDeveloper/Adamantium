namespace Adamantium.Graphics.Core.EffectsFramework;

/// <summary>
/// How one effect source fared in <see cref="ShaderHotReload.ApplyAsync"/>. Compiled means the new version waits for its
/// device's next frame; <see cref="ShaderHotReload.Reloaded"/> tells when it is in place. Otherwise Messages holds the
/// compiler's errors.
/// </summary>
public sealed record ShaderCompileReport(string Source, bool Compiled, string Messages);
