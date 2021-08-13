using System;
using System.Collections.Generic;
using System.Text;
using Leopotam.Ecs;

namespace RebuildData.Server.Data.Character
{
	public class DamageInfo
	{
		public EcsEntity Source;
		public EcsEntity Target;
		public float Time;
		public short Damage;
		public byte HitCount;
		public byte KnockBack;
	}
}
