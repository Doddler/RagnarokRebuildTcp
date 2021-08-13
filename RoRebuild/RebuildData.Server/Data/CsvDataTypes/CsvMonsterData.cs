using System;
using System.Collections.Generic;
using System.Text;
using RebuildData.Shared.Enum.EntityStats;

namespace RebuildData.Server.Data.CsvDataTypes
{
	public class CsvMonsterData
	{
		public int Id { get; set; }
		public string Code { get; set; }
		public string Name { get; set; }
		public int Level { get; set; }
		public int HP { get; set; }
		public int Str { get; set; }
		public int Int { get; set; }
		public int Vit { get; set; }
		public int Dex { get; set; }
		public int Agi { get; set; }
		public int Luk { get; set; }
		public int AtkMin { get; set; }
		public int AtkMax { get; set; }
		public int Range { get; set; }
		public int Def { get; set; }
		public int MDef { get; set; }
		public int Exp { get; set; }
		public int JExp { get; set; }
		public int ScanDist { get; set; }
		public int ChaseDist { get; set; }
		public CharacterSize Size { get; set; }
		public CharacterRace Race { get; set; }
		public CharacterElement Element { get; set; }
		public int RechargeTime { get; set; }
		public int AttackTime { get; set; }
		public int HitTime { get; set; }
		public int MoveSpeed { get; set; }
		public CharacterSpecialType Special { get; set; }
		public string MonsterAiType { get; set; }
		public string ClientSprite { get; set; }
		public int SpriteAttackTiming { get; set; }
		public float ClientOffset { get; set; }
		public float ClientShadow { get; set; }
		public float ClientSize { get; set; }
	}
}
