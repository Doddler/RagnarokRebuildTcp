using System.Collections.Generic;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using UnityEngine;

namespace Assets.Scripts.Network.Messaging
{
    public class EntityMessageQueue
    {
        public ServerControllable Owner;
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
            if (msg.ActivationTime < Time.timeSinceLevelLoad)
            {
                Owner.ExecuteMessage(msg);
                return;
            }

            messages.Add(msg);
            isDirty = true;
        }

        public float TimeUntilMessageLogClears(EntityMessageType type)
        {
            var min = 0f;
            for (var i = 0; i < messages.Count; i++)
                if (messages[i].Type == type && messages[i].ActivationTime > Time.timeSinceLevelLoad)
                    min = messages[i].ActivationTime;

            if (min > 0)
                return min - Time.timeSinceLevelLoad;

            return 0f;
        }

        private int Compare(EntityMessage left, EntityMessage right) => left.ActivationTime.CompareTo(right.ActivationTime);

        public void SendAttackMotion(ServerControllable target, float motionTime, float time, CharacterSkill skill = CharacterSkill.None)
        {
            var msg = EntityMessagePool.Borrow();
            msg.Type = EntityMessageType.AttackMotion;
            msg.Entity = target;
            msg.ActivationTime = Time.timeSinceLevelLoad + time;
            msg.Value1 = (int)skill;
            msg.Float1 = motionTime;

            EnqueueMessage(msg);
        }

        public void SendFaceDirection(FacingDirection direction, float time)
        {
            var msg = EntityMessagePool.Borrow();
            msg.Type = EntityMessageType.FaceDirection;
            msg.ActivationTime = Time.timeSinceLevelLoad + time;
            msg.Value1 = (int)direction;

            EnqueueMessage(msg);
        }

        public void SendHitEffect(ServerControllable src, float time, int hitType, int hitCount = 1)
        {
            for (var i = 0; i < hitCount; i++)
            {
                var msg = EntityMessagePool.Borrow();
                msg.ActivationTime = Time.timeSinceLevelLoad + time + 0.2f * i;
                msg.Type = EntityMessageType.HitEffect;
                msg.Entity = src; //the hit will come from this entity's position
                msg.Value1 = hitType;
                if (src != null)
                    msg.Position = src.transform.position;
                else
                    msg.Position = Vector3.zero;

                EnqueueMessage(msg);
            }
        }

        public void SendElementalHitEffect(ServerControllable src, float time, AttackElement hitType, int hitCount = 1, bool hasSound = false)
        {
            for (var i = 0; i < hitCount; i++)
            {
                var msg = EntityMessagePool.Borrow();
                msg.ActivationTime = Time.timeSinceLevelLoad + time + 0.2f * i;
                msg.Type = EntityMessageType.ElementalEffect;
                msg.Entity = src; //the hit will come from this entity's position
                msg.Value1 = (int)hitType;
                msg.Value2 = hasSound ? 1 : 0;

                EnqueueMessage(msg);
            }
        }
        
        public void SendMessage(EntityMessageType type, float time, int val1 = 0, int val2 = 0)
        {
            var msg = EntityMessagePool.Borrow();
            msg.ActivationTime = Time.timeSinceLevelLoad + time;
            msg.Type = type;
            msg.Value1 = val1;
            msg.Value2 = val2;

            EnqueueMessage(msg);
        }

        public void SendMissEffect(float time)
        {
            var msg = EntityMessagePool.Borrow();
            msg.ActivationTime = Time.timeSinceLevelLoad + time;
            msg.Type = EntityMessageType.Miss;

            EnqueueMessage(msg);
        }

        public void SendDamageEvent(ServerControllable src, float time, int damage, int hitCount, bool isCrit = false, bool takeWeaponSound = true, bool playSound = true)
        {
// #if DEBUG
//             Debug.Log($"Enqueued damage event {damage}x{hitCount} damage, execute after {time}s");
// #endif
            for (var i = 0; i < hitCount; i++)
            {
                var msg = EntityMessagePool.Borrow();
                msg.ActivationTime = Time.timeSinceLevelLoad + time + 0.2f * i;
                msg.Type = EntityMessageType.ShowDamage;
                msg.Entity = src;
                msg.Value1 = damage;
                if (hitCount > 1)
                    msg.Value2 = (i + 1) * damage;
                msg.Value3 = isCrit ? 1 : 0;
                if (playSound)
                    msg.Value4 = takeWeaponSound ? 1 : 0;
                else
                    msg.Value4 = -1;

                EnqueueMessage(msg);
            }
        }
    }
}