using System;
using System.Collections.Generic;
using System.Text;

namespace RebuildSharedData.ClientTypes;

#pragma warning disable CS8618 //disable warning for nullable fields

[Serializable]
public class PlayerClassData
{
    public int Id;
    public string Name;
    public string SpriteMale;
    public string SpriteFemale;
}

[Serializable]
public class PlayerWeaponData
{
    public int Job;
    public int Class;
    public int Weapon;
    public int AttackMale;
    public int AttackFemale;
    public string SpriteMale;
    public string EffectMale;
    public string SpriteFemale;
    public string EffectFemale;

    public PlayerWeaponData Clone() => new PlayerWeaponData
    {
        Job = Job,
        Class = Class,
        Weapon = Weapon,
        AttackMale = AttackMale,
        AttackFemale = AttackFemale,
        SpriteMale = SpriteMale,
        EffectMale = EffectMale,
        SpriteFemale = SpriteFemale,
        EffectFemale = EffectFemale,

    };
}

public class PlayerWeaponClass
{
    public int Id;
    public string Name;
    public string WeaponClass;
    public string HitSound;
}