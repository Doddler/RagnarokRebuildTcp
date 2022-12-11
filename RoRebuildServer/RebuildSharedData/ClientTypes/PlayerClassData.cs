using System;
using System.Collections.Generic;
using System.Text;

namespace RebuildSharedData.ClientTypes;

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
    public int AttackAnimation;
    public string SpriteMale;
    public string SpriteFemale;

    public PlayerWeaponData Clone() => new PlayerWeaponData
    {
        Job = Job,
        Class = Class,
        Weapon = Weapon,
        AttackAnimation = AttackAnimation,
        SpriteMale = SpriteMale,
        SpriteFemale = SpriteFemale
    };
}

public class PlayerWeaponClass
{
    public int Id;
    public string Name;
}