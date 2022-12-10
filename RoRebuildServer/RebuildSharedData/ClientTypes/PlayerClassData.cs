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
    public int Weapon;
    public int AttackAnimation;
    public string SpriteMale;
    public string SpriteFemale;
}