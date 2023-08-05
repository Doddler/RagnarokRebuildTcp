using System;
using Assets.Scripts.Effects.EffectHandlers;

namespace Assets.Scripts.Effects
{
	public enum EffectType
	{
		CastEffect,
		MapWarpEffect,
	}

	public enum PrimitiveType
	{
		Cylender,
		Circle,
	}

	public partial class RagnarokEffectData
	{
		static RagnarokEffectData()
		{
			effectHandlers.Add(EffectType.CastEffect, new Assets.Scripts.Effects.EffectHandlers.CastEffect());
			effectHandlers.Add(EffectType.MapWarpEffect, new Assets.Scripts.Effects.EffectHandlers.MapWarpEffect());
			primitiveHandlers.Add(PrimitiveType.Circle, new Assets.Scripts.Effects.PrimitiveHandlers.CirclePrimitive());
			primitiveDataFactory.Add(PrimitiveType.Circle, () => new Assets.Scripts.Effects.PrimitiveData.CircleData());
		}
	}
}
