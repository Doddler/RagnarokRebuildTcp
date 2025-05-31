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
		Curse,
		Freeze,
		Hide,
		Petrified,
		Petrifying,
		Silence,
		Sleep,
		Stun,
		AgiDown,
		AgiUp,
		Blessing,
		BloodDrain,
		CartRevolution,
		Detox,
		Endure,
		Fireball,
		FrostDiverHit,
		FrostDiverTrail,
		MagnumBreak,
		Provoke,
		Ruwach,
		SafetyWall,
		Sight,
		SoulStrike,
		StealEffect,
		WarpPortal,
		WarpPortalOpening,
		EarthSpike,
		HeavensDrive,
		JupitelBall,
		JupitelHit,
		LordOfVermilion,
		WaterBallAttack,
		WaterBallRise,
		ArcherArrow,
		CastHolyEffect,
		ColdHit,
		DummyGroundEffect,
		Entry,
		Exit,
		ExplosiveAura,
		RecoveryParticles,
		RoSprite,
		RoProjectileSprite,
		Teleport,
		BlueWaterfallEffect,
		DiscoLights,
	}

	public enum PrimitiveType
	{
		AnimatedTexture2D,
		Aura,
		Casting3D,
		Circle2D,
		Circle,
		Cylinder3D,
		DirectionalBillboard,
		ExplosiveAura,
		Flash2D,
		ForestLight,
		Heal,
		Particle3DSpline,
		ParticleAnimatedSprite,
		ParticleUp,
		ProjectorPrimitive,
		RoSprite,
		Sphere3D,
		Spike3D,
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
			effectHandlers.Add(EffectType.Curse, new Assets.Scripts.Effects.EffectHandlers.StatusEffects.CurseEffect());
			effectHandlers.Add(EffectType.Freeze, new Assets.Scripts.Effects.EffectHandlers.StatusEffects.FreezeEffect());
			effectHandlers.Add(EffectType.Hide, new Assets.Scripts.Effects.EffectHandlers.StatusEffects.HideEffect());
			effectHandlers.Add(EffectType.Petrified, new Assets.Scripts.Effects.EffectHandlers.StatusEffects.PetrifiedEffect());
			effectHandlers.Add(EffectType.Petrifying, new Assets.Scripts.Effects.EffectHandlers.StatusEffects.PetrifyingEffect());
			effectHandlers.Add(EffectType.Silence, new Assets.Scripts.Effects.EffectHandlers.StatusEffects.SilenceEffect());
			effectHandlers.Add(EffectType.Sleep, new Assets.Scripts.Effects.EffectHandlers.StatusEffects.SleepEffect());
			effectHandlers.Add(EffectType.Stun, new Assets.Scripts.Effects.EffectHandlers.StatusEffects.StunEffect());
			effectHandlers.Add(EffectType.AgiDown, new Assets.Scripts.Effects.EffectHandlers.Skills.AgiDownEffect());
			effectHandlers.Add(EffectType.AgiUp, new Assets.Scripts.Effects.EffectHandlers.Skills.AgiUpEffect());
			effectHandlers.Add(EffectType.Blessing, new Assets.Scripts.Effects.EffectHandlers.Skills.BlessingEffect());
			effectHandlers.Add(EffectType.BloodDrain, new Assets.Scripts.Effects.EffectHandlers.Skills.BloodDrainEffect());
			effectHandlers.Add(EffectType.CartRevolution, new Assets.Scripts.Effects.EffectHandlers.Skills.CartRevolutionEffect());
			effectHandlers.Add(EffectType.Detox, new Assets.Scripts.Effects.EffectHandlers.Skills.DetoxEffect());
			effectHandlers.Add(EffectType.Endure, new Assets.Scripts.Effects.EffectHandlers.Skills.EndureEffect());
			effectHandlers.Add(EffectType.Fireball, new Assets.Scripts.Effects.EffectHandlers.Skills.FireballEffect());
			effectHandlers.Add(EffectType.FrostDiverHit, new Assets.Scripts.Effects.EffectHandlers.Skills.FrostDiverHitEffect());
			effectHandlers.Add(EffectType.FrostDiverTrail, new Assets.Scripts.Effects.EffectHandlers.Skills.FrostDiverTrailEffect());
			effectHandlers.Add(EffectType.MagnumBreak, new Assets.Scripts.Effects.EffectHandlers.Skills.MagnumBreakEffect());
			effectHandlers.Add(EffectType.Provoke, new Assets.Scripts.Effects.EffectHandlers.Skills.ProvokeEffect());
			effectHandlers.Add(EffectType.Ruwach, new Assets.Scripts.Effects.EffectHandlers.Skills.RuwachEffect());
			effectHandlers.Add(EffectType.SafetyWall, new Assets.Scripts.Effects.EffectHandlers.Skills.SafetyWallEffect());
			effectHandlers.Add(EffectType.Sight, new Assets.Scripts.Effects.EffectHandlers.Skills.SightEffect());
			effectHandlers.Add(EffectType.SoulStrike, new Assets.Scripts.Effects.EffectHandlers.Skills.SoulStrikeEffect());
			effectHandlers.Add(EffectType.StealEffect, new Assets.Scripts.Effects.EffectHandlers.Skills.StealEffect());
			effectHandlers.Add(EffectType.WarpPortal, new Assets.Scripts.Effects.EffectHandlers.Skills.WarpPortalEffect());
			effectHandlers.Add(EffectType.WarpPortalOpening, new Assets.Scripts.Effects.EffectHandlers.Skills.WarpPortalOpeningEffect());
			effectHandlers.Add(EffectType.EarthSpike, new Assets.Scripts.Effects.EffectHandlers.Skills.EarthSpikeEffect());
			effectHandlers.Add(EffectType.HeavensDrive, new Assets.Scripts.Effects.EffectHandlers.Skills.HeavensDriveEffect());
			effectHandlers.Add(EffectType.JupitelBall, new Assets.Scripts.Effects.EffectHandlers.Skills.JupitelBallEffect());
			effectHandlers.Add(EffectType.JupitelHit, new Assets.Scripts.Effects.EffectHandlers.Skills.JupitelHitEffect());
			effectHandlers.Add(EffectType.LordOfVermilion, new Assets.Scripts.Effects.EffectHandlers.Skills.LordOfVermilionEffect());
			effectHandlers.Add(EffectType.WaterBallAttack, new Assets.Scripts.Effects.EffectHandlers.Skills.WaterBallAttackEffect());
			effectHandlers.Add(EffectType.WaterBallRise, new Assets.Scripts.Effects.EffectHandlers.Skills.WaterBallRiseEffect());
			effectHandlers.Add(EffectType.ArcherArrow, new Assets.Scripts.Effects.EffectHandlers.General.ArcherArrow());
			effectHandlers.Add(EffectType.CastHolyEffect, new Assets.Scripts.Effects.EffectHandlers.General.CastHolyEffect());
			effectHandlers.Add(EffectType.ColdHit, new Assets.Scripts.Effects.EffectHandlers.General.ColdHitEffect());
			effectHandlers.Add(EffectType.DummyGroundEffect, new Assets.Scripts.Effects.EffectHandlers.General.DummyGroundEffect());
			effectHandlers.Add(EffectType.Entry, new Assets.Scripts.Effects.EffectHandlers.General.EntryEffect());
			effectHandlers.Add(EffectType.Exit, new Assets.Scripts.Effects.EffectHandlers.General.ExitEffect());
			effectHandlers.Add(EffectType.ExplosiveAura, new Assets.Scripts.Effects.EffectHandlers.General.ExplosiveAuraEffect());
			effectHandlers.Add(EffectType.RecoveryParticles, new Assets.Scripts.Effects.EffectHandlers.General.RecoveryParticlesEffect());
			effectHandlers.Add(EffectType.RoSprite, new Assets.Scripts.Effects.EffectHandlers.General.RoSpriteEffect());
			effectHandlers.Add(EffectType.RoProjectileSprite, new Assets.Scripts.Effects.EffectHandlers.General.RoSpriteProjectileEffect());
			effectHandlers.Add(EffectType.Teleport, new Assets.Scripts.Effects.EffectHandlers.General.TeleportEffect());
			effectHandlers.Add(EffectType.BlueWaterfallEffect, new Assets.Scripts.Effects.EffectHandlers.Environment.BlueWaterfallEffect());
			effectHandlers.Add(EffectType.DiscoLights, new Assets.Scripts.Effects.EffectHandlers.Environment.DiscoLightsEffect());
			primitiveHandlers.Add(PrimitiveType.AnimatedTexture2D, new Assets.Scripts.Effects.PrimitiveHandlers.AnimatedTexture2DPrimitive());
			primitiveDataFactory.Add(PrimitiveType.AnimatedTexture2D, () => new Assets.Scripts.Effects.PrimitiveData.EffectSpriteData());
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
			primitiveHandlers.Add(PrimitiveType.ExplosiveAura, new Assets.Scripts.Effects.PrimitiveHandlers.ExplosiveAuraPrimitive());
			primitiveDataFactory.Add(PrimitiveType.ExplosiveAura, () => new Assets.Scripts.Effects.PrimitiveData.SimpleSpriteData());
			primitiveHandlers.Add(PrimitiveType.Flash2D, new Assets.Scripts.Effects.PrimitiveHandlers.Flash2DPrimitive());
			primitiveDataFactory.Add(PrimitiveType.Flash2D, () => new Assets.Scripts.Effects.PrimitiveData.FlashData());
			primitiveHandlers.Add(PrimitiveType.ForestLight, new Assets.Scripts.Effects.PrimitiveHandlers.ForestLightPrimitive());
			primitiveHandlers.Add(PrimitiveType.Heal, new Assets.Scripts.Effects.PrimitiveHandlers.HealPrimitive());
			primitiveHandlers.Add(PrimitiveType.Particle3DSpline, new Assets.Scripts.Effects.PrimitiveHandlers.Particle3DSplinePrimitive());
			primitiveDataFactory.Add(PrimitiveType.Particle3DSpline, () => new Assets.Scripts.Effects.PrimitiveData.Particle3DSplineData());
			primitiveHandlers.Add(PrimitiveType.ParticleAnimatedSprite, new Assets.Scripts.Effects.PrimitiveHandlers.ParticleAnimatedSpritePrimitive());
			primitiveDataFactory.Add(PrimitiveType.ParticleAnimatedSprite, () => new Assets.Scripts.Effects.PrimitiveData.SpriteParticleData());
			primitiveHandlers.Add(PrimitiveType.ParticleUp, new Assets.Scripts.Effects.PrimitiveHandlers.ParticleUpPrimitive());
			primitiveDataFactory.Add(PrimitiveType.ParticleUp, () => new Assets.Scripts.Effects.PrimitiveData.ParticleUpData());
			primitiveHandlers.Add(PrimitiveType.ProjectorPrimitive, new Assets.Scripts.Effects.PrimitiveHandlers.ProjectorPrimitive());
			primitiveHandlers.Add(PrimitiveType.RoSprite, new Assets.Scripts.Effects.PrimitiveHandlers.RoSpritePrimitive());
			primitiveHandlers.Add(PrimitiveType.Sphere3D, new Assets.Scripts.Effects.PrimitiveHandlers.Sphere3DPrimitive());
			primitiveDataFactory.Add(PrimitiveType.Sphere3D, () => new Assets.Scripts.Effects.PrimitiveData.CircleData());
			primitiveHandlers.Add(PrimitiveType.Spike3D, new Assets.Scripts.Effects.PrimitiveHandlers.Spike3DPrimitive());
			primitiveDataFactory.Add(PrimitiveType.Spike3D, () => new Assets.Scripts.Effects.PrimitiveData.Spike3DData());
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
