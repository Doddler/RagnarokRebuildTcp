using System;
using Assets.Scripts.Effects.EffectHandlers;

namespace Assets.Scripts.Effects
{
	public enum EffectType
	{
		CastEffect,
		CastLockOn,
		DefaultSkillCastEffect,
		FireArrow,
		ForestLightEffect,
		MapWarpEffect,
	}

	public enum PrimitiveType
	{
		Cylender,
		Circle2D,
		Circle,
		DirectionalBillboard,
		Flash2D,
		ForestLight,
		Texture3D,
	}

	public partial class RagnarokEffectData
	{
		static RagnarokEffectData()
		{
			effectHandlers.Add(EffectType.CastEffect, new Assets.Scripts.Effects.EffectHandlers.CastEffect());
			effectHandlers.Add(EffectType.CastLockOn, new Assets.Scripts.Effects.EffectHandlers.CastLockOnEffect());
			effectHandlers.Add(EffectType.DefaultSkillCastEffect, new Assets.Scripts.Effects.EffectHandlers.DefaultSkillCastEffect());
			effectHandlers.Add(EffectType.FireArrow, new Assets.Scripts.Effects.EffectHandlers.FireArrow());
			effectHandlers.Add(EffectType.ForestLightEffect, new Assets.Scripts.Effects.EffectHandlers.ForestLightEffect());
			effectHandlers.Add(EffectType.MapWarpEffect, new Assets.Scripts.Effects.EffectHandlers.MapWarpEffect());
			primitiveHandlers.Add(PrimitiveType.Cylender, new Assets.Scripts.Effects.PrimitiveHandlers.CastingCylinderPrimitive());
			primitiveHandlers.Add(PrimitiveType.Circle2D, new Assets.Scripts.Effects.PrimitiveHandlers.Circle2DPrimitive());
			primitiveDataFactory.Add(PrimitiveType.Circle2D, () => new Assets.Scripts.Effects.PrimitiveData.CircleData());
			primitiveHandlers.Add(PrimitiveType.Circle, new Assets.Scripts.Effects.PrimitiveHandlers.CirclePrimitive());
			primitiveDataFactory.Add(PrimitiveType.Circle, () => new Assets.Scripts.Effects.PrimitiveData.CircleData());
			primitiveHandlers.Add(PrimitiveType.DirectionalBillboard, new Assets.Scripts.Effects.PrimitiveHandlers.DirectionalBillboardPrimitive());
			primitiveDataFactory.Add(PrimitiveType.DirectionalBillboard, () => new Assets.Scripts.Effects.PrimitiveData.EffectSpriteData());
			primitiveHandlers.Add(PrimitiveType.Flash2D, new Assets.Scripts.Effects.PrimitiveHandlers.Flash2DPrimitive());
			primitiveDataFactory.Add(PrimitiveType.Flash2D, () => new Assets.Scripts.Effects.PrimitiveData.FlashData());
			primitiveHandlers.Add(PrimitiveType.ForestLight, new Assets.Scripts.Effects.PrimitiveHandlers.ForestLightPrimitive());
			primitiveHandlers.Add(PrimitiveType.Texture3D, new Assets.Scripts.Effects.PrimitiveHandlers.Texture3DPrimitive());
			primitiveDataFactory.Add(PrimitiveType.Texture3D, () => new Assets.Scripts.Effects.PrimitiveData.Texture3DData());
		}
	}
}
