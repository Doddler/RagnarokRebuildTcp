using System;
using System.Collections.Generic;
using Assets.Scripts.Utility;

namespace Assets.Scripts.Effects
{
   
    public static partial class RagnarokEffectData
    {
        public delegate void PrimitiveUpdateDelegate(RagnarokPrimitive primitive);
        public delegate void PrimitiveRenderDelegate(RagnarokPrimitive primitive, MeshBuilder mb);
        
        private static Dictionary<EffectType, IEffectHandler> effectHandlers = new();
        private static Dictionary<PrimitiveType, IPrimitiveHandler> primitiveHandlers = new();
        private static Dictionary<PrimitiveType, Func<Object>> primitiveDataFactory = new(); 
        
        
        public static IEffectHandler GetEffectHandler(EffectType type) => effectHandlers[type];
        public static IPrimitiveHandler GetPrimitiveHandler(PrimitiveType type) => primitiveHandlers.GetValueOrDefault(type, null);

        public static Object NewPrimitiveData(PrimitiveType type)
        {
            if (primitiveDataFactory.TryGetValue(type, out var f))
                return f();
            return null;
        }

        public static void SceneChangeCleanup()
        {
            foreach(var handlers in effectHandlers)
                handlers.Value.SceneChangeResourceCleanup();
        }
    }
}