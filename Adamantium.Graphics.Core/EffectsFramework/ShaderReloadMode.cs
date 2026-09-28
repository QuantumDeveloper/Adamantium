namespace Adamantium.Graphics.Core.EffectsFramework;

/// <summary>When <see cref="ShaderHotReload"/> compiles a change saved to disk.</summary>
public enum ShaderReloadMode
{
    /// <summary>As soon as it is saved.</summary>
    OnSave,

    /// <summary>Only on <see cref="ShaderHotReload.ApplyAsync"/>; until then changes are only gathered.</summary>
    Manual
}
