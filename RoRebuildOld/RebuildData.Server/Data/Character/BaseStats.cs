using System;
using System.Collections.Generic;
using System.Text;

namespace RebuildData.Server.Data.Character
{
	//base stats that are modified 
	public class BaseStats
    {
        public int Level;

        public int Experience;

		public int MaxHp, MaxSp;
		public short Str, Agi, Dex, Vit, Int, Luk;
		public short Atk, Atk2;

        public float MoveSpeed;
        public float AttackMotionTime, AttackDelayTime, HitDelayTime, SpriteAttackTiming;
		public int Range;
    }
}
