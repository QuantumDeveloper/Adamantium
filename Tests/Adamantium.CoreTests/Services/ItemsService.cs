namespace Adamantium.CoreTests.Services
{
    public class ItemsService : IItemsService
    {
        public ItemsService(IItemsApi itemsApi)
        {
            ItemsApi = itemsApi;
        }
        
        public IItemsApi ItemsApi { get; }
    }
}