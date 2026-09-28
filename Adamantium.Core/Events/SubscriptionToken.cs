using System;

namespace Adamantium.Core.Events
{
    public class SubscriptionToken : IEquatable<SubscriptionToken>
    {
        public Guid Token { get; }

        public SubscriptionToken()
        {
            Token = Guid.NewGuid();
        }

        public bool Equals(SubscriptionToken other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Token.Equals(other.Token);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((SubscriptionToken) obj);
        }

        public override int GetHashCode()
        {
            return Token.GetHashCode();
        }

        public static bool operator ==(SubscriptionToken token1, SubscriptionToken token2)
        {
            if (token1 == null) return false;
            return token1.Equals(token2);
        }

        public static bool operator !=(SubscriptionToken token1, SubscriptionToken token2)
        {
            return !(token1 == token2);
        }
    }
}