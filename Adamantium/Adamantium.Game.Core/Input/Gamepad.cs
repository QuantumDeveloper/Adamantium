namespace Adamantium.Game.Core.Input
{
    internal abstract class Gamepad
    {
        public abstract GamepadState GetState();

        public virtual void SetReportingState(bool enable)
        {
            
        }

        public virtual void SetVibration(float leftMotorSpeed, float rightMotorSpeed)
        {
            
        }
    }
}