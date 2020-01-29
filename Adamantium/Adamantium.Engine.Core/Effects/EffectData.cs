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

            //var compilerArgs = Scheme.Add<CompilerArguments>(false);
            var compilerArgs = Scheme.Add(typeof(CompilerArguments),false);
            compilerArgs.AddField(1, "FilePath");
            compilerArgs.AddField(2, "DependencyFilePath");
            compilerArgs.AddField(3, "CompilerFlags");
            compilerArgs.AddField(4, "Macros");
            compilerArgs.AddField(5, "IncludeDirectoryList");


            //var constantBuffer = Scheme.Add<ConstantBuffer>(false);
            var constantBuffer = Scheme.Add(typeof(ConstantBuffer), false);
            constantBuffer.AddField(1, "Name");
            constantBuffer.AddField(2, "Size");
            constantBuffer.AddField(3, "Slot");
            constantBuffer.AddField(4, "Parameters");

            //var parameter = Scheme.Add<Parameter>();
            var parameter = Scheme.Add(typeof(Parameter), false);
            parameter.AddField(1, "Name");
            parameter.AddField(2, "Class");
            parameter.AddField(3, "Type");

            //var valueTypeParameter = Scheme.Add<ValueTypeParameter>(false);
            var valueTypeParameter = Scheme.Add(typeof(ValueTypeParameter), false);
            valueTypeParameter.AddField(1, "Offset");
            valueTypeParameter.AddField(2, "Count");
            valueTypeParameter.AddField(3, "Size");
            valueTypeParameter.AddField(4, "RowCount");
            valueTypeParameter.AddField(5, "ColumnCount");
            valueTypeParameter.AddField(6, "DefaultValue");

            //var resourceParameter = Scheme.Add<ResourceParameter>(false);
            var resourceParameter = Scheme.Add(typeof(ResourceParameter), false);
            resourceParameter.AddField(4, "Slot");
            resourceParameter.AddField(5, "Count");

            parameter.AddSubType(10, typeof(ValueTypeParameter));
            parameter.AddSubType(11, typeof(ResourceParameter));

            //var technique = Scheme.Add<Technique>();
            var technique = Scheme.Add(typeof(Technique), false);
            technique.AddField(1, "Name");
            technique.AddField(2, "Passes");

            //var pipeline = Scheme.Add<Pipeline>(false);
            var pipeline = Scheme.Add(typeof(Pipeline), false);
            //pipeline.AddField(1, "Links");
            pipeline.AddField(1, "Links2");
            pipeline.IgnoreListHandling = true;

            //var shaderLink = Scheme.Add<ShaderLink>(false);
            var shaderLink = Scheme.Add(typeof(ShaderLink), false);
            shaderLink.AddField(1, "Index");
            shaderLink.AddField(2, "EntryPoint");
            shaderLink.AddField(3, "ImportName");
            shaderLink.AddField(4, "ShaderType");

            //var pass = Scheme.Add<Pass>(false);
            var pass = Scheme.Add(typeof(Pass), false);
            pass.AddField(1, "Name");
            pass.AddField(2, "IsSubPass");
            //pass.AddField(3, "Properties");
            pass.AddField(4, "Pipeline");

            //var shaderMacro = Scheme.Add<ShaderMacro>(false);
            var shaderMacro = Scheme.Add(typeof(ShaderMacro), false);
            shaderMacro.AddField(1, "Name");
            shaderMacro.AddField(2, "Value");

            //var effect = Scheme.Add<EffectData.Effect>(false);
            var effect = Scheme.Add(typeof(EffectData.Effect), false);
            effect.AddField(1, "Name");
            effect.AddField(2, "ShareConstantBuffers");
            effect.AddField(3, "Techniques");
            effect.AddField(4, "Arguments");

            //var shader = Scheme.Add<Shader>(false);
            var shader = Scheme.Add(typeof(Shader), false);
            shader.AddField(1, "Name");
            shader.AddField(2, "EntryPoint");
            shader.AddField(3, "Type");
            shader.AddField(4, "CompilerFlags");
            shader.AddField(5, "Level");
            shader.AddField(6, "Language");
            shader.AddField(7, "Bytecode");
            shader.AddField(8, "Hashcode");

            //var effectData = Scheme.Add<EffectData>(false);
            var effectData = Scheme.Add(typeof(EffectData), false);
            effectData.AddField(1, "Shaders");
            effectData.AddField(2, "Description");

            Scheme.CompileInPlace();
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
