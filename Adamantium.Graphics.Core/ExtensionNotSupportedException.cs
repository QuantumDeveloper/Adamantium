using System;

namespace Adamantium.Graphics.Core;

public class ExtensionNotSupportedException(string adapterName, string[] extensions)
    : Exception($"{adapterName} is not supporting following extensions: {string.Join(", ", extensions)}. Choose another adapter or update drivers");