using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntitySystem;

namespace RoRebuildServer.EntityComponents.Messaging;

public enum MessageType
{
    ApplyDamage,
}

public struct CharacterMessage
{
    public MessageType Type;
    public Entity Entity;
    public int Param1;
    public int Param2;
    public int Param3;
    public int Param4;
    public int Param5;
    public int Param6;

    public CharacterMessage(MessageType type, Entity entity, int param1 = 0, int param2 = 0, int param3 = 0,
        int param4 = 0, int param5 = 0, int param6 = 0)
    {
        Type = type;
        Entity = entity;
        Param1 = param1;
        Param2 = param2;
        Param3 = param3;
        Param4 = param4;
        Param5 = param5;
        Param6 = param6;
    }

    public CharacterMessage(DamageInfo di)
    {
        Type = MessageType.ApplyDamage;
        Entity = di.Target;
        Param1 = di.Damage;
        Param2 = (int)di.Flags;
        Param3 = di.HitCount;
        Param4 = di.KnockBack;
        Param5 = (int)di.AttackSkill;
        Param6 = di.AttackPosition.PackIntoInt;
    }
}