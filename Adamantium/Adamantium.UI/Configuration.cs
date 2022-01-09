using System.Runtime.InteropServices;

namespace Adamantium.UI;

public class Configuration
{
    public static Platform Platform { get; }
        
    static Configuration()
    {
        if (RuntimeInformation.IsOSPlatform((OSPlatform.Windows)))
        {
            Platform = Platform.Windows;
        }
        else if (RuntimeInformation.IsOSPlatform((OSPlatform.OSX)))
        {
            Platform = Platform.OSX;
        }
        else if (RuntimeInformation.IsOSPlatform((OSPlatform.Linux)))
        {
            Platform = Platform.Linux;
        }
        else if (RuntimeInformation.IsOSPlatform((OSPlatform.FreeBSD)))
        {
            Platform = Platform.FreeBSD;
        }
    }
}