using System;
using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Effects.EffectHandlers.Environment;
using Assets.Scripts.Effects.EffectHandlers.General;
using Assets.Scripts.Effects.EffectHandlers.Skills;
using Assets.Scripts.Effects.EffectHandlers.StatusEffects;

namespace Assets.Scripts.Effects
{
	public enum EffectType
	{
		ForestLightEffect,
		MapWarpEffect,
		CastEffect,
		CastLockOn,
		CastTargetCircle,
		DefaultSkillCastEffect,
		HitEffect,
		FireArrow,
		HammerFall,
		HealEffect,
		IceArrow,
		Hide,
		Stun,
		Blessing,
		CartRevolution,
		Provoke,
		SafetyWall,
		SoulStrike,
		StealEffect,
		WarpPortal,
		WarpPortalOpening,
		ArcherArrow,
		CastHolyEffect,
		ColdHit,
		Entry,
		Exit,
		Teleport,
		BlueWaterfallEffect,
	}

	public enum PrimitiveType
	{
		Aura,
		Casting3D,
		Circle2D,
		Circle,
		Cylinder3D,
		DirectionalBillboard,
		Flash2D,
		ForestLight,
		Heal,
		Particle3DSpline,
		ProjectorPrimitive,
		Teleport,
		Texture2D,
		Texture3D,
		WarpPortal,
		Wind,
	}

	public partial class RagnarokEffectData
	{
		static RagnarokEffectData()
		{
			effectHandlers.Add(EffectType.ForestLightEffect, new Assets.Scripts.Effects.EffectHandlers.ForestLightEffect());
			effectHandlers.Add(EffectType.MapWarpEffect, new Assets.Scripts.Effects.EffectHandlers.MapWarpEffect());
			effectHandlers.Add(EffectType.CastEffect, new Assets.Scripts.Effects.EffectHandlers.CastEffect());
			effectHandlers.Add(EffectType.CastLockOn, new Assets.Scripts.Effects.EffectHandlers.CastLockOnEffect());
			effectHandlers.Add(EffectType.CastTargetCircle, new Assets.Scripts.Effects.EffectHandlers.CastTargetCircle());
			effectHandlers.Add(EffectType.DefaultSkillCastEffect, new Assets.Scripts.Effects.EffectHandlers.DefaultSkillCastEffect());
			effectHandlers.Add(EffectType.HitEffect, new Assets.Scripts.Effects.EffectHandlers.HitEffect());
			effectHandlers.Add(EffectType.FireArrow, new Assets.Scripts.Effects.EffectHandlers.FireArrow());
			effectHandlers.Add(EffectType.HammerFall, new Assets.Scripts.Effects.EffectHandlers.HammerFallEffect());
			effectHandlers.Add(EffectType.HealEffect, new Assets.Scripts.Effects.EffectHandlers.HealEffect());
			effectHandlers.Add(EffectType.IceArrow, new Assets.Scripts.Effects.EffectHandlers.IceArrow());
			effectHandlers.Add(EffectType.Hide, new Assets.Scripts.Effects.EffectHandlers.StatusEffects.HideEffect());
			effectHandlers.Add(EffectType.Stun, new Assets.Scripts.Effects.EffectHandlers.StatusEffects.StunEffect());
			effectHandlers.Add(EffectType.Blessing, new Assets.Scripts.Effects.EffectHandlers.Skills.BlessingEffect());
			effectHandlers.Add(EffectType.CartRevolution, new Assets.Scripts.Effects.EffectHandlers.Skills.CartRevolutionEffect());
			effectHandlers.Add(EffectType.Provoke, new Assets.Scripts.Effects.EffectHandlers.Skills.ProvokeEffect());
			effectHandlers.Add(EffectType.SafetyWall, new Assets.Scripts.Effects.EffectHandlers.Skills.SafetyWallEffect());
			effectHandlers.Add(EffectType.SoulStrike, new Assets.Scripts.Effects.EffectHandlers.Skills.SoulStrikeEffect());
			effectHandlers.Add(EffectType.StealEffect, new Assets.Scripts.Effects.EffectHandlers.Skills.StealEffect());
			effectHandlers.Add(EffectType.WarpPortal, new Assets.Scripts.Effects.EffectHandlers.Skills.WarpPortalEffect());
			effectHandlers.Add(EffectType.WarpPortalOpening, new Assets.Scripts.Effects.EffectHandlers.Skills.WarpPortalOpeningEffect());
			effectHandlers.Add(EffectType.ArcherArrow, new Assets.Scripts.Effects.EffectHandlers.General.ArcherArrow());
			effectHandlers.Add(EffectType.CastHolyEffect, new Assets.Scripts.Effects.EffectHandlers.General.CastHolyEffect());
			effectHandlers.Add(EffectType.ColdHit, new Assets.Scripts.Effects.EffectHandlers.General.ColdHitEffect());
			effectHandlers.Add(EffectType.Entry, new Assets.Scripts.Effects.EffectHandlers.General.EntryEffect());
			effectHandlers.Add(EffectType.Exit, new Assets.Scripts.Effects.EffectHandlers.General.ExitEffect());
			effectHandlers.Add(EffectType.Teleport, new Assets.Scripts.Effects.EffectHandlers.General.TeleportEffect());
			effectHandlers.Add(EffectType.BlueWaterfallEffect, new Assets.Scripts.Effects.EffectHandlers.Environment.BlueWaterfallEffect());
			primitiveHandlers.Add(PrimitiveType.Aura, new Assets.Scripts.Effects.PrimitiveHandlers.AuraPrimitive());
			primitiveHandlers.Add(PrimitiveType.Casting3D, new Assets.Scripts.Effects.PrimitiveHandlers.CastingCylinderPrimitive());
			primitiveHandlers.Add(PrimitiveType.Circle2D, new Assets.Scripts.Effects.PrimitiveHandlers.Circle2DPrimitive());
			primitiveDataFactory.Add(PrimitiveType.Circle2D, () => new Assets.Scripts.Effects.PrimitiveData.CircleData());
			primitiveHandlers.Add(PrimitiveType.Circle, new Assets.Scripts.Effects.PrimitiveHandlers.CirclePrimitive());
			primitiveDataFactory.Add(PrimitiveType.Circle, () => new Assets.Scripts.Effects.PrimitiveData.CircleData());
			primitiveHandlers.Add(PrimitiveType.Cylinder3D, new Assets.Scripts.Effects.PrimitiveHandlers.Cylinder3DPrimitive());
			primitiveDataFactory.Add(PrimitiveType.Cylinder3D, () => new Assets.Scripts.Effects.PrimitiveData.CylinderData());
			primitiveHandlers.Add(PrimitiveType.DirectionalBillboard, new Assets.Scripts.Effects.PrimitiveHandlers.DirectionalBillboardPrimitive());
			primitiveDataFactory.Add(PrimitiveType.DirectionalBillboard, () => new Assets.Scripts.Effects.PrimitiveData.EffectSpriteData());
			primitiveHandlers.Add(PrimitiveType.Flash2D, new Assets.Scripts.Effects.PrimitiveHandlers.Flash2DPrimitive());
			primitiveDataFactory.Add(PrimitiveType.Flash2D, () => new Assets.Scripts.Effects.PrimitiveData.FlashData());
			primitiveHandlers.Add(PrimitiveType.ForestLight, new Assets.Scripts.Effects.PrimitiveHandlers.ForestLightPrimitive());
			primitiveHandlers.Add(PrimitiveType.Heal, new Assets.Scripts.Effects.PrimitiveHandlers.HealPrimitive());
			primitiveHandlers.Add(PrimitiveType.Particle3DSpline, new Assets.Scripts.Effects.PrimitiveHandlers.Particle3DSplinePrimitive());
			primitiveDataFactory.Add(PrimitiveType.Particle3DSpline, () => new Assets.Scripts.Effects.PrimitiveData.Particle3DSplineData());
			primitiveHandlers.Add(PrimitiveType.ProjectorPrimitive, new Assets.Scripts.Effects.PrimitiveHandlers.ProjectorPrimitive());
			primitiveHandlers.Add(PrimitiveType.Teleport, new Assets.Scripts.Effects.PrimitiveHandlers.TeleportPrimitive());
			primitiveHandlers.Add(PrimitiveType.Texture2D, new Assets.Scripts.Effects.PrimitiveHandlers.Texture2DPrimitive());
			primitiveDataFactory.Add(PrimitiveType.Texture2D, () => new Assets.Scripts.Effects.PrimitiveData.Texture2DData());
			primitiveHandlers.Add(PrimitiveType.Texture3D, new Assets.Scripts.Effects.PrimitiveHandlers.Texture3DPrimitive());
			primitiveDataFactory.Add(PrimitiveType.Texture3D, () => new Assets.Scripts.Effects.PrimitiveData.Texture3DData());
			primitiveHandlers.Add(PrimitiveType.WarpPortal, new Assets.Scripts.Effects.PrimitiveHandlers.WarpPortalPrimitive());
			primitiveHandlers.Add(PrimitiveType.Wind, new Assets.Scripts.Effects.PrimitiveHandlers.WindPrimitive());
		}
	}
}
