namespace Adamantium.Engine.Generators
{
    internal class FontGeneratorResult
    {
        public string SourceText { get; set; }

        public string ClassName { get; set; }

        public string FinalNamespace { get; set; }

        public bool HasErrors { get; set; }
    }
}
