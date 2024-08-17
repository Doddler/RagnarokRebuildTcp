using Assets.Scripts.Utility;
using UnityEngine;

namespace Assets.Scripts.Network.Messaging
{
    public class EntityMessage : IResettable
    {
        public float ActivationTime;
        public ServerControllable Entity;
        public EntityMessageType Type;
        public Vector3 Position;
        public int Value1;
        public int Value2;
        public int Value3;
        public float Float1;
        public float Float2;

        public void Reset()
        {
            ActivationTime = 0;
            Entity = null;
            Type = EntityMessageType.None;
            Value1 = Value2 = 0;
            Float1 = Float2 = 0;
        }
    }
}