using System.Collections.Generic;
using System.IO;
using MessagePack;

namespace Adamantium.Engine.Effects
{
    [MessagePackObject]
    public sealed partial class EffectData
    {
        public static readonly string CompiledExtension = "fx.compiled";

        //private static readonly RuntimeTypeModel Scheme;

        static EffectData()
        {
            //Scheme = RuntimeTypeModel.Create();
            //ConfigureSerializationScheme();
        }

        private static void ConfigureSerializationScheme()
        {
            MessagePackSerializer.DefaultOptions = MessagePack.Resolvers.ContractlessStandardResolver.Options;
            
            /*Scheme.AutoAddMissingTypes = true;
            
            var compilerArgs = Scheme.Add(typeof(CompilerArguments),false);
            compilerArgs.AddField(1, "FilePath");
            compilerArgs.AddField(2, "DependencyFilePath");
            compilerArgs.AddField(3, "CompilerFlags");
            compilerArgs.AddField(4, "Macros");
            compilerArgs.AddField(5, "IncludeDirectoryList");

            var constantBuffer = Scheme.Add(typeof(ConstantBuffer), false);
            constantBuffer.AddField(1, "Name");
            constantBuffer.AddField(2, "Size");
            constantBuffer.AddField(3, "Slot");
            constantBuffer.AddField(4, "Parameters");
            
            var parameter = Scheme.Add(typeof(Parameter), false);
            parameter.AddField(1, "Name");
            parameter.AddField(2, "Class");
            parameter.AddField(3, "Type");
            
            var valueTypeParameter = Scheme.Add(typeof(ValueTypeParameter), false);
            valueTypeParameter.AddField(1, "Offset");
            valueTypeParameter.AddField(2, "Count");
            valueTypeParameter.AddField(3, "Size");
            valueTypeParameter.AddField(4, "RowCount");
            valueTypeParameter.AddField(5, "ColumnCount");
            valueTypeParameter.AddField(6, "DefaultValue");
            
            var resourceParameter = Scheme.Add(typeof(ResourceParameter), false);
            resourceParameter.AddField(4, "Slot");
            resourceParameter.AddField(5, "Count");

            parameter.AddSubType(10, typeof(ValueTypeParameter));
            parameter.AddSubType(11, typeof(ResourceParameter));
            
            var technique = Scheme.Add(typeof(Technique), false);
            technique.AddField(1, "Name");
            technique.AddField(2, "Passes");

            var pipeline = Scheme.Add<Pipeline>();
            pipeline.AddField(1, "Links");
            pipeline.IgnoreListHandling = true;

            var shaderLink = Scheme.Add(typeof(ShaderLink), false);
            shaderLink.AddField(1, "Index");
            shaderLink.AddField(2, "EntryPoint");
            shaderLink.AddField(3, "ImportName");
            shaderLink.AddField(4, "ShaderType");

            var pass = Scheme.Add(typeof(Pass), false);
            pass.AddField(1, "Name");
            pass.AddField(2, "IsSubPass");
            //pass.AddField(3, "Properties");
            pass.AddField(4, "Pipeline");

            var shaderMacro = Scheme.Add(typeof(ShaderMacro), false);
            shaderMacro.AddField(1, "Name");
            shaderMacro.AddField(2, "Value");

            var effect = Scheme.Add(typeof(EffectData.Effect), false);
            effect.AddField(1, "Name");
            effect.AddField(2, "ShareConstantBuffers");
            effect.AddField(3, "Techniques");
            effect.AddField(4, "Arguments");

            var shader = Scheme.Add(typeof(Shader), false);
            shader.AddField(1, "Name");
            shader.AddField(2, "EntryPoint");
            shader.AddField(3, "Type");
            shader.AddField(4, "CompilerFlags");
            shader.AddField(5, "Level");
            shader.AddField(6, "Language");
            shader.AddField(7, "Bytecode");
            shader.AddField(8, "Hashcode");
            shader.AddField(9, "ConstantBuffers");
            shader.AddField(10, "ResourceParameters");

            var effectData = Scheme.Add(typeof(EffectData), false);
            effectData.AddField(1, "Shaders");
            effectData.AddField(2, "Description");

            Scheme.CompileInPlace();
            */
        }

        public EffectData() { }

        
        /// <summary>
        /// List of compiled shaders.
        /// </summary>
        [Key(0)]
        public List<Shader> Shaders;

        /// <summary>
        /// Complete Effect description
        /// </summary>
        [Key(1)]
        public Effect Description;

        /// <summary>
        /// Saves this <see cref="EffectData"/> instance to the specified stream.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="effectData">instance of <see cref="EffectData"/></param>
        public void Save(Stream stream)
        {
            MessagePackSerializer.Serialize(stream, this);
            //Scheme.Serialize(stream, this);
        }

        /// <summary>
        /// Saves this <see cref="EffectData"/> instance to the specified file.
        /// </summary>
        /// <param name="fileName">The output filename.</param>
        public void Save(string fileName)
        {
            using var stream = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.Write);
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
            //var effect = (EffectData)Scheme.Deserialize(stream, null, typeof(EffectData));
            var effect = MessagePackSerializer.Deserialize<EffectData>(stream);
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
