using System.Text;
using Adamantium.Engine.Core;
using Adamantium.EntityFramework;
using Adamantium.Game;
using Adamantium.Game.GameInput;

namespace Adamantium.Engine.Processors
{
    public class FontRenderingProcessor : EntityProcessor
    {
        private InputService inputService;

        public FontRenderingProcessor(EntityWorld world) : base(world)
        {
            inputService = Services.Resolve<InputService>();
        }

        public override void Update(IGameTime gameTime)
        {
            var kbInputs = inputService.GetKeyboardInputs();

            var strBuilder = new StringBuilder();

            foreach (var input in kbInputs)
            {
                strBuilder.Append(input.Key.ToString());
            }
            
            base.Update(gameTime);
        }
    }
}