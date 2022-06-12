namespace Adamantium.Game.Core.Input
{
    /// <summary>
    /// Describes current state of one Key
    /// </summary>
    public struct ButtonState : IEquatable<ButtonState>
    {
        private ButtonStateFlags stateFlags;

        /// <summary>
        /// Constructs <see cref="ButtonState"/> using <see cref="ButtonStateFlags"/>
        /// </summary>
        /// <param name="flags"></param>
        public ButtonState(ButtonStateFlags flags)
        {
            stateFlags = flags;
        }

        /// <summary>
        /// Button state flags
        /// </summary>
        public ButtonStateFlags StateFlags
        {
            get => stateFlags;
            set => stateFlags = value;
        }

        /// <summary>
        /// Indicates is button down
        /// </summary>
        public bool IsDown
        {
            get => (stateFlags & ButtonStateFlags.Down) != 0;
            set
            {
                if (value)
                {
                    stateFlags |= ButtonStateFlags.Down;
                }
                else
                {
                    stateFlags &= ~ButtonStateFlags.Down;
                }
            }
        }

        /// <summary>
        /// Indicates is button pressed in first time
        /// </summary>
        public bool IsPressed
        {
            get => (stateFlags & ButtonStateFlags.Pressed) != 0;
            set
            {
                if (value)
                {
                    stateFlags |= ButtonStateFlags.Pressed;
                }
                else
                {
                    stateFlags &= ~ButtonStateFlags.Pressed;
                }
            }
        }

        /// <summary>
        /// Indicates is button released
        /// </summary>
        public bool IsReleased
        {
            get => (stateFlags & ButtonStateFlags.Released) != 0;
            set
            {
                if (value)
                {
                    stateFlags |= ButtonStateFlags.Released;
                }
                else
                {
                    stateFlags &= ~ButtonStateFlags.Released;
                }
            }
        }

        /// <summary>
        /// Set IsPressed and IsReleased to false
        /// </summary>
        public void Reset()
        {
            IsPressed = false;
            IsReleased = false;
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <returns>
        /// true if the current object is equal to the <paramref name="other"/> parameter; otherwise, false.
        /// </returns>
        /// <param name="other">An object to compare with this object.</param>
        public bool Equals(ButtonState other)
        {
            return stateFlags == other.stateFlags;
        }

        /// <summary>
        /// Indicates whether this instance and a specified object are equal.
        /// </summary>
        /// <returns>
        /// true if <paramref name="obj"/> and this instance are the same type and represent the same value; otherwise, false. 
        /// </returns>
        /// <param name="obj">The object to compare with the current instance. </param><filterpriority>2</filterpriority>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is ButtonState && Equals((ButtonState)obj);
        }

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns>
        /// A 32-bit signed integer that is the hash code for this instance.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public override int GetHashCode()
        {
            unchecked
            {
                return stateFlags.GetHashCode();
            }
        }

        /// <summary>
        /// Compares two <see cref="ButtonState"/>s
        /// </summary>
        /// <param name="left">first <see cref="ButtonState"/></param>
        /// <param name="right">second <see cref="ButtonState"/></param>
        /// <returns>true if Button states are equals, otherwise - false</returns>
        public static bool operator ==(ButtonState left, ButtonState right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two <see cref="ButtonState"/>s
        /// </summary>
        /// <param name="left">first <see cref="ButtonState"/></param>
        /// <param name="right">second <see cref="ButtonState"/></param>
        /// <returns>true if Button states are NOT equals, otherwise - false</returns>
        public static bool operator !=(ButtonState left, ButtonState right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// Creates <see cref="ButtonStateFlags"/> from <see cref="ButtonState"/>
        /// </summary>
        /// <param name="keyState">button state</param>
        /// <returns>new <see cref="ButtonStateFlags"/></returns>
        public static implicit operator ButtonStateFlags(ButtonState keyState)
        {
            return keyState.stateFlags;
        }

        /// <summary>
        /// Creates <see cref="ButtonState"/> from <see cref="ButtonStateFlags"/>
        /// </summary>
        /// <param name="keyStateflags">key state flags</param>
        /// <returns>new <see cref="ButtonState"/> instance</returns>
        public static implicit operator ButtonState(ButtonStateFlags keyStateflags)
        {
            return new ButtonState(keyStateflags);
        }

        /// <summary>
        /// Returns the fully qualified type name of this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.String"/> containing a fully qualified type name.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public override string ToString()
        {
            return $"{stateFlags}";
        }
    }
}
