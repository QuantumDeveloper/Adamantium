using Adamantium.Core.DependencyInjection;

namespace Adamantium.CoreTests.Services
{
    public interface ICommonService
    {
        IContactsService ContactsService { get; }
        
        IItemsService ItemsService { get; }
        
        IMediaService MediaService { get; }
    }

    public class CommonService : ICommonService
    {
        public CommonService()
        {
            
        }
        
        [DependencyConstructor]
        public CommonService(IContactsService contactsService,
            IItemsService itemsService,
            IMediaService mediaService)
        {
            ContactsService = contactsService;
            ItemsService = itemsService;
            MediaService = mediaService;
        }

        public IContactsService ContactsService { get; }
        public IItemsService ItemsService { get; }
        public IMediaService MediaService { get; }
    }
    
}