using System;

namespace Adamantium.ECS.Components
{
    public abstract class ActivatableComponent : Component
    {
        private Boolean isEnabled;

        protected ActivatableComponent()
        {
            isEnabled = true;
        }

        public bool IsEnabled
        {
            get => isEnabled;
            set => SetProperty(ref isEnabled, value);
        }
    }
}