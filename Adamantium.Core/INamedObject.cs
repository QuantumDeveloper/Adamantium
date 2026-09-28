namespace Adamantium.Core
{
    /// <summary>
    /// Base interface for a component base.
    /// </summary>
    public interface INamedObject
    {
        /// <summary>
        /// Gets the name of this component.
        /// </summary>
        /// <value>The name.</value>
        public string Name { get; set; }

        public bool HasName { get; }
        
        /// <summary>
        /// Gets or sets the tag associated to this object.
        /// </summary>
        /// <value>The tag.</value>
        object Tag { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether the name of this instance is immutable.
        /// </summary>
        /// <value><c>true</c> if this instance is name immutable; otherwise, <c>false</c>.</value>
        bool IsNameImmutable { get; }
    }
}