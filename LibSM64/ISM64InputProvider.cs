using UnityEngine;

using Vector2NET = System.Numerics.Vector2;
using Vector3NET = System.Numerics.Vector3;

namespace LibSM64
{
    public interface ISM64InputProvider
    {
        public enum Button
        {
            Jump,
            Kick,
            Stomp
        };

        public abstract Vector3NET GetCameraLookDirection();
        public abstract Vector2NET GetJoystickAxes();
        public abstract bool GetButtonHeld( Button button );
    }
}