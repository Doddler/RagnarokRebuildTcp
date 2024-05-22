using System;

namespace Assets.Scripts.Effects
{
    [Flags]
    public enum RoPrimitiveHandlerFlags : int
    {
        None,
        CycleColors,
        NoAnimation
    }
    
    public class RoEffectAttribute : Attribute
    {
        public string TypeName;
        
        public RoEffectAttribute(string name)
        {
            TypeName = name;
        }
    }

    public class RoPrimitiveAttribute : Attribute
    {
        public string TypeName;
        public Type DataType;

        public RoPrimitiveAttribute(string name, Type type = null)
        {
            TypeName = name;
            DataType = type;
        }
    }
}