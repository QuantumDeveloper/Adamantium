using System.Collections.Generic;

namespace Adamantium.CoreTests.Services
{
    public class ContactsService : IContactsService
    {
        public ContactsService()
        {
            Contacts = new List<string>();
        }
        
        public List<string> Contacts { get; set; }
    }
}