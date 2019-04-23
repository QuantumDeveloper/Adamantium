using System;

namespace Adamantium.Engine.Core.Content
{
   public struct ContentReaderParameters
   {
      /// <summary>
      /// Name of the asset currently loaded when using <see cref="IContentManager.Load{T}"/>.
      /// </summary>
      public String AssetName;

      /// <summary>
      /// Path to currently loaded asset when using <see cref="IContentManager.Load{T}"/>.
      /// </summary>
      public String AssetPath;

      /// <summary>
      /// Type of the asset currently loaded when using <see cref="IContentManager.Load{T}"/>.
      /// </summary>
      public Type AssetType;

      /// <summary>
      /// Path to copy additional asset data and serialized asset itself
      /// </summary>
      public String OutputPath;

      /// <summary>
      /// Custom options provided when using <see cref="IContentManager.Load{T}"/>.
      /// </summary>
      public object Options;
   }
}
