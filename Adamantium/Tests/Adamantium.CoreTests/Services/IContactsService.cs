using System.Collections.Generic;

namespace Adamantium.CoreTests.Services
{
    public interface IContactsService
    {
        public List<string> Contacts { get; set; }
    }
}