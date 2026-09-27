namespace Adamantium.Multiverse
{
    public enum UniverseMode
    {
        /// <summary>
        /// The universe runs as a standalone instance and starts its own platform
        /// </summary>
        Standalone,

        /// <summary>
        /// The universe runs under another platform and does not start its own
        /// </summary>
        Slave
    }
}