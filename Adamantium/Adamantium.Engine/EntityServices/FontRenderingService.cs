using System.Text;
using Adamantium.Core;
using Adamantium.EntityFramework;
using Adamantium.Game.Core.Input;

namespace Adamantium.Engine.EntityServices
{
    public class FontRenderingService : EntityService
    {
        private GameInputManager inputManager;

        public FontRenderingService(EntityWorld world) : base(world)
        {
            inputManager = DependencyResolver.Resolve<GameInputManager>();
        }

        public override void Update(AppTime gameTime)
        {
            var kbInputs = inputManager.GetKeyboardInputs();

            var strBuilder = new StringBuilder();

            foreach (var input in kbInputs)
            {
                strBuilder.Append(input.Key.ToString());
            }
            
            base.Update(gameTime);
        }
    }
}