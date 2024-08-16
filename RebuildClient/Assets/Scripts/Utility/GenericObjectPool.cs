using System.Collections.Generic;

namespace Assets.Scripts.Utility
{
    public class GenericObjectPool<T> where T : IResettable, new()
    {
        private static Stack<T> pool = new Stack<T>(20);
        public static T Borrow()
        {
            if (pool.Count > 0)
                return pool.Pop();
            return new T();
        }

        public static void Return(T obj)
        {
            obj.Reset();
            pool.Push(obj);
        }
    }
}