using System.Collections.Generic;
using Utils;

namespace Assets.Scripts.Network
{
    public static class EntityMessagePool
    {
        private static Stack<EntityMessageQueue> MessageQueues = new();
        private static Stack<EntityMessage> Messages = new();
        

        public static EntityMessageQueue BorrowQueue()
        {
            if (MessageQueues.TryPop(out var q))
                return q;
            
            return new EntityMessageQueue();
        }

        public static void ReturnQueue(EntityMessageQueue queue)
        {
            queue.Reset();
            MessageQueues.Push(queue);
        }

        public static EntityMessage NewMessage()
        {
            if (Messages.TryPop(out var m))
                return m;
            return new EntityMessage();
        }

        public static void ReturnMessage(EntityMessage m)
        {
            m.Reset();
            Messages.Push(m);
        }
    }
    
    public class EntityMessageQueue
    {
        private PriorityQueue<EntityMessage, float> messageQueue;

        public void Reset()
        {
            messageQueue.Clear();
        }
    }

    public enum EntityMessageType
    {
        
    }
    
    public class EntityMessage
    {
        public void Reset()
        {
            
        }
    }
}