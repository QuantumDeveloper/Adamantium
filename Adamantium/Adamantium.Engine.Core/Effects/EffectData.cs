using ProtoBuf.Meta;
using System.Collections.Generic;
using System.IO;

namespace Adamantium.Engine.Core.Effects
{
    public sealed partial class EffectData
    {
        public static readonly string CompiledExtension = "fx.compiled";

        private static readonly RuntimeTypeModel Scheme;

        static EffectData()
        {
            Scheme = RuntimeTypeModel.Create();
            Scheme.AutoAddMissingTypes = true;
        }

        private static void Configure()
        {
            var compilerArgs = Scheme.Add<CompilerArguments>();
            compilerArgs.AddField(0, "FilePath");
            compilerArgs.AddField(1, "DependencyFilePath");
            compilerArgs.AddField(2, "CompilerFlags");
            compilerArgs.AddField(3, "Macros");
            compilerArgs.AddField(4, "IncludeDirectoryList");

            var technique = Scheme.Add<Technique>();
            technique.AddField(0, "Name");
            technique.AddField(1, "Passes");
            technique.AddField(2, "IsSubPass");
            technique.AddField(3, "Properties");
            technique.AddField(4, "Pipeline");

            var pipeline = Scheme.Add<Pipeline>();
            pipeline.AddField(0, "Links");

            var pass = Scheme.Add<Pass>();
            pass.AddField(0, "Name");
            pass.AddField(1, "IsSubPass");


            var effect = Scheme.Add<EffectData.Effect>();
            effect.AddField(0, "Name");
            effect.AddField(1, "ShareConstantBuffers");
            effect.AddField(2, "Techniques");
            effect.AddField(3, "Arguments");
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
            Scheme.Serialize(stream, this);
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
            var effect = (EffectData)Scheme.Deserialize(stream, null, typeof(EffectData));
            return effect;
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
