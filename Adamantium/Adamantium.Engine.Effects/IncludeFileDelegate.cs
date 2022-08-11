using System.IO;

namespace Adamantium.Engine.Effects;

public delegate Stream IncludeFileDelegate(bool isSystemInclude, string file);