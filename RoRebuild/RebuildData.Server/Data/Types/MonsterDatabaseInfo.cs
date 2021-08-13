using RebuildData.Server.Data.Monster;

namespace RebuildData.Server.Data.Types
{
	public class MonsterDatabaseInfo
	{
		public int Id { get; set; }
		public int Level { get; set; }
		public string Name { get; set; }
		public string Code { get; set; }
		public int ScanDist { get; set; }
		public int ChaseDist { get; set; }
		public int HP { get; set; }
		public int Exp { get; set; }
		public int AtkMin { get; set; }
		public int AtkMax { get; set; }
		public int Vit { get; set; }
		public int Def { get; set; }
        public float RechargeTime { get; set; }
		public float AttackTime { get; set; }
		public float HitTime { get; set; }
		public float SpriteAttackTiming { get; set; }
		public int Range { get; set; }
		public MonsterAiType AiType { get; set; }
		public float MoveSpeed { get; set; }
	}
}
