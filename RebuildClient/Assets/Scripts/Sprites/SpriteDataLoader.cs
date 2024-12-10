using System.Collections.Generic;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using UnityEngine;

namespace Assets.Scripts.Sprites
{
	public struct PlayerSpawnParameters
    {
		public int ServerId;
		public int ClassId;
		public short HeadId;
		public short HairDyeId;
		public HeadFacing HeadFacing;
		public Direction Facing;
		public CharacterState State;
		public Vector2Int Position;
		public bool IsMale;
		public bool IsMainCharacter;
		public int Level;
        public string Name;
        public int Hp;
        public int MaxHp;
        public int Sp;
        public int MaxSp;
        public int WeaponClass;
        public int Headgear1;
        public int Headgear2;
        public int Headgear3;
        public int Weapon;
        public int Shield;
        public List<CharacterStatusEffect> CharacterStatusEffects;
    }

	public struct MonsterSpawnParameters
	{
		public int ServerId;
		public int ClassId;
        public string Name;
		public Direction Facing;
		public CharacterState State;
		public Vector2Int Position;
        public int Level;
        public int Hp;
        public int MaxHp;
        public bool Interactable;
        public List<CharacterStatusEffect> CharacterStatusEffects;
    }
}