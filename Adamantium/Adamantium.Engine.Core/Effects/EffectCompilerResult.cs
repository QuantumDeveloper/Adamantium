namespace Adamantium.Engine.Core.Effects
{
   public sealed class EffectCompilerResult
   {
      /// <summary>
      /// Initializes a new instance of the <see cref="EffectCompilerResult" /> class.
      /// </summary>
      /// <param name="dependencyFilePath">The path to dependency file (may be null).</param>
      /// <param name="effectData">The EffectData.</param>
      /// <param name="logger">The logger.</param>
      public EffectCompilerResult(string dependencyFilePath, EffectData effectData, Logger logger)
      {
         DependencyFilePath = dependencyFilePath;
         EffectData = effectData;
         Logger = logger;
      }

      /// <summary>
      /// The effect dependency list (a list of files and includes that this effect is timestamp dependent).
      /// </summary>
      public string DependencyFilePath;

      /// <summary>
      /// Gets the EffectData.
      /// </summary>
      /// <value>The EffectData.</value>
      public EffectData EffectData { get; private set; }

      /// <summary>
      /// Gets a value indicating whether this instance has errors.
      /// </summary>
      /// <value><c>true</c> if this instance has errors; otherwise, <c>false</c>.</value>
      public bool HasErrors => Logger.HasErrors;

      /// <summary>
      /// Gets the logger containing compilation messages..
      /// </summary>
      /// <value>The logger.</value>
      public Logger Logger { get; private set; }
   }
}
