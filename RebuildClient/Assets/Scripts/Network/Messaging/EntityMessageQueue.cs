using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Network.Messaging
{
    public class EntityMessageQueue
    {
        private readonly List<EntityMessage> messages = new();
        private bool isDirty;

        public bool HasMessages => messages.Count > 0;

        public bool TryGetMessage(out EntityMessage msg)
        {
            msg = null;
            if (messages.Count == 0)
                return false;
            if (isDirty)
            {
                messages.Sort(Compare);
                isDirty = false;
            }

            if (messages[0].ActivationTime < Time.timeSinceLevelLoad)
            {
                msg = messages[0];
                messages.RemoveAt(0);
                return true;
            }

            return false;   
        }

        public void EnqueueMessage(EntityMessage msg)
        {
            messages.Add(msg);
            isDirty = true;
        }

        private int Compare(EntityMessage left, EntityMessage right) => left.ActivationTime.CompareTo(right.ActivationTime);
        
        public void SendHitEffect(ServerControllable src, float time)
        {
            var msg = EntityMessagePool.Borrow();
            msg.ActivationTime = Time.timeSinceLevelLoad + time;
            msg.Type = EntityMessageType.HitEffect;
            msg.Entity = src; //the hit will come from this entity's position
            msg.Value1 = 0;

            EnqueueMessage(msg);
        }

        public void SendShowDamage(float time, int damage)
        {
            var msg = EntityMessagePool.Borrow();
            msg.ActivationTime = Time.timeSinceLevelLoad + time;
            msg.Type = EntityMessageType.ShowDamage;
            msg.Value1 = damage;

            EnqueueMessage(msg);
        }

        public void SendComboDamage(float time, int comboDamage)
        {
            var msg = EntityMessagePool.Borrow();
            msg.ActivationTime = Time.timeSinceLevelLoad + time;
            msg.Type = EntityMessageType.ComboDamage;
            msg.Value1 = comboDamage;

            EnqueueMessage(msg);
        }
    }
}