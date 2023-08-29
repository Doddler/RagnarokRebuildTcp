using System;
using Assets.Scripts.Effects.EffectHandlers;

namespace Assets.Scripts.Effects
{
	public enum EffectType
	{
		CastEffect,
		CastLockOn,
		FireArrow,
		ForestLightEffect,
		MapWarpEffect,
	}

	public enum PrimitiveType
	{
		Cylender,
		Circle,
		DirectionalBillboard,
		ForestLight,
		Texture3D,
	}

	public partial class RagnarokEffectData
	{
		static RagnarokEffectData()
		{
			effectHandlers.Add(EffectType.CastEffect, new Assets.Scripts.Effects.EffectHandlers.CastEffect());
			effectHandlers.Add(EffectType.CastLockOn, new Assets.Scripts.Effects.EffectHandlers.CastLockOnEffect());
			effectHandlers.Add(EffectType.FireArrow, new Assets.Scripts.Effects.EffectHandlers.FireArrow());
			effectHandlers.Add(EffectType.ForestLightEffect, new Assets.Scripts.Effects.EffectHandlers.ForestLightEffect());
			effectHandlers.Add(EffectType.MapWarpEffect, new Assets.Scripts.Effects.EffectHandlers.MapWarpEffect());
			primitiveHandlers.Add(PrimitiveType.Cylender, new Assets.Scripts.Effects.PrimitiveHandlers.CastingCylinderPrimitive());
			primitiveHandlers.Add(PrimitiveType.Circle, new Assets.Scripts.Effects.PrimitiveHandlers.CirclePrimitive());
			primitiveDataFactory.Add(PrimitiveType.Circle, () => new Assets.Scripts.Effects.PrimitiveData.CircleData());
			primitiveHandlers.Add(PrimitiveType.DirectionalBillboard, new Assets.Scripts.Effects.PrimitiveHandlers.DirectionalBillboardPrimitive());
			primitiveDataFactory.Add(PrimitiveType.DirectionalBillboard, () => new Assets.Scripts.Effects.PrimitiveData.EffectSpriteData());
			primitiveHandlers.Add(PrimitiveType.ForestLight, new Assets.Scripts.Effects.PrimitiveHandlers.ForestLightPrimitive());
			primitiveHandlers.Add(PrimitiveType.Texture3D, new Assets.Scripts.Effects.PrimitiveHandlers.Texture3DPrimitive());
			primitiveDataFactory.Add(PrimitiveType.Texture3D, () => new Assets.Scripts.Effects.PrimitiveData.Texture3DData());
		}
	}
}
