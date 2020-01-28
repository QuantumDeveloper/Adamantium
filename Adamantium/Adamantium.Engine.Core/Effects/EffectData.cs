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
            ConfigureSerializationScheme();
        }

        private static void ConfigureSerializationScheme()
        {
            Scheme.AutoAddMissingTypes = true;

            var compilerArgs = Scheme.Add<CompilerArguments>();
            compilerArgs.AddField(0, "FilePath");
            compilerArgs.AddField(1, "DependencyFilePath");
            compilerArgs.AddField(2, "CompilerFlags");
            compilerArgs.AddField(3, "Macros");
            compilerArgs.AddField(4, "IncludeDirectoryList");


            var constantBuffer = Scheme.Add<ConstantBuffer>();
            constantBuffer.AddField(0, "Name");
            constantBuffer.AddField(1, "Size");
            constantBuffer.AddField(2, "Slot");
            constantBuffer.AddField(3, "Parameters");

            var parameter = Scheme.Add<Parameter>();
            parameter.AddField(0, "Name");
            parameter.AddField(1, "Class");
            parameter.AddField(2, "Type");

            var valueTypeParameter = Scheme.Add<ValueTypeParameter>();
            valueTypeParameter.AddField(3, "Offset");
            valueTypeParameter.AddField(4, "Count");
            valueTypeParameter.AddField(5, "Size");
            valueTypeParameter.AddField(6, "RowCount");
            valueTypeParameter.AddField(7, "ColumnCount");
            valueTypeParameter.AddField(8, "DefaultValue");
            //parameter.AddSubType(3, typeof(ValueTypeParameter));

            var resourceParameter = Scheme.Add<ResourceParameter>();
            resourceParameter.AddField(3, "Slot");
            resourceParameter.AddField(4, "Count");

            var technique = Scheme.Add<Technique>();
            technique.AddField(0, "Name");
            technique.AddField(1, "Passes");

            var shaderLink = Scheme.Add<ShaderLink>();
            shaderLink.AddField(0, "Index");
            shaderLink.AddField(1, "EntryPoint");
            shaderLink.AddField(2, "ImportName");
            shaderLink.AddField(3, "ShaderType");

            var pipeline = Scheme.Add<Pipeline>();
            pipeline.AddField(0, "Links");

            var pass = Scheme.Add<Pass>();
            pass.AddField(0, "Name");
            pass.AddField(1, "IsSubPass");
            pass.AddField(2, "Properties");
            pass.AddField(3, "Pipeline");

            var shaderMacro = Scheme.Add<ShaderMacro>();
            shaderMacro.AddField(0, "Name");
            shaderMacro.AddField(1, "Value");

            var effect = Scheme.Add<EffectData.Effect>();
            effect.AddField(0, "Name");
            effect.AddField(1, "ShareConstantBuffers");
            effect.AddField(2, "Techniques");
            effect.AddField(3, "Arguments");

            var shader = Scheme.Add<Shader>();
            shader.AddField(0, "Name");
            shader.AddField(1, "EntryPoint");
            shader.AddField(2, "Type");
            shader.AddField(3, "CompilerFlags");
            shader.AddField(4, "Level");
            shader.AddField(5, "Language");
            shader.AddField(6, "Bytecode");
            shader.AddField(7, "Hashcode");

            var effectData = Scheme.Add<EffectData>();
            effectData.AddField(0, "Shaders");
            effectData.AddField(1, "Description");
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
