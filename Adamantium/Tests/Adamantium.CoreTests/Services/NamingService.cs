namespace Adamantium.CoreTests.Services
{
    public class NamingService : INamingService
    {
        public NamingService(string name)
        {
            Name = name;
        }
        
        public string Name { get; }
    }
}