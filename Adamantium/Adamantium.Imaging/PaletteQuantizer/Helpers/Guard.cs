using System;

namespace Adamantium.Imaging.PaletteQuantizer.Helpers
{
    public static class Guard
    {
        /// <summary>
        /// Checks if an argument is null
        /// </summary>
        /// <param name="argument">argument</param>
        /// <param name="argumentName">argument name</param>
        public static void CheckNull(Object argument, String argumentName)
        {
            if (argument == null)
            {
                String message = $"Cannot use '{argumentName}' when it is null!";
                throw new ArgumentNullException(message);
            }
        }
    }
}
