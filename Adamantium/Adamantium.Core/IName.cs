namespace Adamantium.Core
{
    /// <summary>
    /// Base interface for a component base.
    /// </summary>
    public interface IName
    {
        /// <summary>
        /// Gets the name of this component.
        /// </summary>
        /// <value>The name.</value>
        string Name { get; set; }

        public bool HasName { get; }
    }
}