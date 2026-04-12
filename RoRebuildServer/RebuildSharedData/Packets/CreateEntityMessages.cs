using MemoryPack;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using System;
using System.Collections.Generic;
using System.Text;

namespace RebuildSharedData.Packets;

[MemoryPackable]
public partial struct EntitySpawnParameters
{
    public int ServerId { get; set; }
    public int ClassId { get; set; }
    public int OverrideClassId { get; set; }
    public string Name { get; set; }
    public CharacterType Type { get; set; }
    public Direction Facing { get; set; }
    public CharacterState State { get; set; }
    public Position Position { get; set; }
    public byte Level { get; set; }
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public int Sp { get; set; }
    public int MaxSp { get; set; }
    public Dictionary<CharacterStatusEffect, float>? CharacterStatusEffects { get; set; }
    public bool IsMainCharacter { get; set; }
}

[MemoryPackable]
public partial struct NpcSpawnParameters
{
    public NpcDisplayType DisplayType { get; set; }
    public NpcEffectType EffectType { get; set; }
    public bool Interactable { get; set; }
    public int OwnerId { get; set; }
}

[MemoryPackable]
public partial struct PlayerSpawnParameters
{
    public bool IsMale { get; set; }
    public int WeaponClass { get; set; }
    public HeadFacing HeadFacing { get; set; }
    public byte HeadType { get; set; }
    public byte HairColor { get; set; }
    public int Headgear1 { get; set; }
    public int Headgear2 { get; set; }
    public int Headgear3 { get; set; }
    public int Weapon { get; set; }
    public int Shield { get; set; }
    public int PartyId { get; set; }
    public string? PartyName { get; set; }
    public CharacterFollowerState Follower { get; set; }
}