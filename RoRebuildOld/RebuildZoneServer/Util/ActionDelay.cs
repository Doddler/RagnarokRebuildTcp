using System;
using System.Collections.Generic;
using System.Text;
using RebuildData.Server.Logging;

namespace RebuildZoneServer.Util
{
	public enum CooldownActionType
	{
		Click,
		FaceDirection,
		SitStand,
		StopAction
	}

	public static class ActionDelay
	{
		public const float ClickCooldownTime = 0.30f;
		public const float FaceDirectionCooldown = 0.10f;
		public const float SitStandCooldown = 0.25f;
		private const float StopActionCooldown = 0.20f;

		public static float CooldownTime(CooldownActionType type)
		{
			switch (type)
			{
				case CooldownActionType.Click: return ClickCooldownTime;
				case CooldownActionType.FaceDirection: return FaceDirectionCooldown;
				case CooldownActionType.SitStand: return SitStandCooldown;
				case CooldownActionType.StopAction: return StopActionCooldown;
				default:
					ServerLogger.LogWarning($"Could not get ActionDelay Cooldown for type {type} (value {(int)type}.");
					return 0.3f;
			}
		}
	}
}
