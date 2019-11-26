using System.Collections.Generic;
using System.IO;

namespace Adamantium.Engine.Core.Effects
{
    public sealed partial class EffectData
    {
        public static readonly string CompiledExtension = "fx.compiled";

        //private static readonly RuntimeTypeModel Scheme;

        static EffectData()
        {
            //Scheme = TypeModel.Create();
            //Scheme.AutoAddMissingTypes = true;
        }

        public EffectData() { }

        /// <summary>
        /// List of compiled shaders.
        /// </summary>
        public List<Shader> Shaders;

        /// <summary>
        /// Complete Effect description
        /// </summary>
        public Effect Description;

        /// <summary>
        /// Saves this <see cref="EffectData"/> instance to the specified stream.
        /// </summary>
        /// <param name="stream">The stream.</param>
        public void Save(Stream stream)
        {
            //Scheme.Serialize(stream, this);
        }

        /// <summary>
        /// Saves this <see cref="EffectData"/> instance to the specified file.
        /// </summary>
        /// <param name="fileName">The output filename.</param>
        public void Save(string fileName)
        {
            using (var stream = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.Write))
                Save(stream);
        }

        /// <summary>
        /// Loads an <see cref="EffectData"/> from the specified stream.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <returns>An <see cref="EffectData"/>. Null if the stream is not a serialized <see cref="EffectData"/>.</returns>
        /// <remarks>
        /// </remarks>
        public static EffectData Load(Stream stream)
        {
            //EffectData effect = (EffectData)Scheme.Deserialize(stream, null, typeof(EffectData));
            //return effect;
            return null;
        }

        /// <summary>
        /// Loads an <see cref="EffectData"/> from the specified buffer.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <returns>An <see cref="EffectData"/> </returns>
        public static EffectData Load(byte[] buffer)
        {
            return Load(new MemoryStream(buffer));
        }

        /// <summary>
        /// Loads an <see cref="EffectData"/> from the specified file.
        /// </summary>
        /// <param name="fileName">The filename.</param>
        /// <returns>An <see cref="EffectData"/> </returns>
        public static EffectData Load(string fileName)
        {
            //var path = fileName.Replace('/', '\\');
            using (var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read))
                return Load(stream);
        }

    }
}
