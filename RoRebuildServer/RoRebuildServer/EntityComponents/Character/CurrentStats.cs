namespace RoRebuildServer.EntityComponents.Character;

public class CurrentStats
{
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public int Sp, MaxSp;
    public short Vit { get; set; }
    public short Str, Agi, Dex, Int, Luk;
    public short Atk, Atk2;
    //public int Def, SoftDef;
    public int Def { get; set; }

    public float MoveSpeed { get; set; }
    public float AttackMotionTime, AttackDelayTime, HitDelayTime, SpriteAttackTiming;
    public int Range;
}