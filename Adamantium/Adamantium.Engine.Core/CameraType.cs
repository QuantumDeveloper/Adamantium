using System;

namespace Adamantium.Engine.Core
{
    public struct CameraType : IEquatable<CameraType>
    {
        enum CameraClass
        {
            Free,
            FirstPerson,
            ThirdPersonFree,
            ThirdPersonFreeAlt,
            ThirdPersonLocked,
            Special
        }

        private readonly CameraClass Value;

        private CameraType(CameraClass cameraClass)
        {
            Value = cameraClass;
        }

        public static CameraType Free = new CameraType(CameraClass.Free);

        public static CameraType FirstPerson = new CameraType(CameraClass.FirstPerson);

        public static CameraType ThirdPersonFree = new CameraType(CameraClass.ThirdPersonFree);

        public static CameraType ThirdPersonFreeAlt = new CameraType(CameraClass.ThirdPersonFreeAlt);

        public static CameraType ThirdPersonLocked = new CameraType(CameraClass.ThirdPersonLocked);

        public static CameraType Special = new CameraType(CameraClass.Special);

        public Boolean IsThirdPerson()
        {
            if (Value == CameraClass.ThirdPersonFree ||
                Value == CameraClass.ThirdPersonFreeAlt ||
                Value == CameraClass.ThirdPersonLocked)
            {
                return true;
            }
            return false;
        }

        public bool Equals(CameraType other)
        {
            return Value.Equals(other.Value);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is CameraType && Equals((CameraType)obj);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public static bool operator ==(CameraType left, CameraType right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CameraType left, CameraType right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }


}
