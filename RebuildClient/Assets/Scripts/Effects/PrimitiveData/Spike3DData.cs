using Assets.Scripts.Utility;
using UnityEngine;

namespace Assets.Scripts.Effects.PrimitiveData
{
    public enum Spike3DFlags
    {
        None,
        SpeedLimit,
        ReturnDown
    }
    
    public class Spike3DData : IResettable
    {
        public Spike3DFlags Flags;
        public float Size;
        public float Height;
        public float Speed;
        public float Acceleration;
        public Vector3 Up;
        public int StopStep;
        public int ChangeStep;
        public float ChangeSpeed;
        public float ChangeAccel;
        public int ReturnStep;
        public float ReturnSpeed;
        public float ReturnAccel;
        
        public float Alpha;
        public float AlphaMax;
        public float AlphaSpeed;
        public float FadeOutLength;
        
        public void Reset()
        {
            Flags = Spike3DFlags.None;
            Size = 0;
            Height = 0;
            Speed = 0;
            Acceleration = 0;
            Up = Vector3.zero;
            StopStep = 0;
            ChangeStep = 0;
            ChangeSpeed = 0;
            ChangeAccel = 0;
            ReturnStep = 0;
            ReturnSpeed = 0;
            ReturnAccel = 0;
            AlphaSpeed = 0;
            FadeOutLength = 0;
        }
    }
}