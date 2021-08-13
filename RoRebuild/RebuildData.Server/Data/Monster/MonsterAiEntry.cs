using System;
using System.Collections.Generic;
using System.Text;

namespace RebuildData.Server.Data.Monster
{
	public class MonsterAiEntry
	{
		public MonsterAiState InputState;
		public MonsterInputCheck InputCheck;
		public MonsterOutputCheck OutputCheck;
		public MonsterAiState OutputState;
	}
}
