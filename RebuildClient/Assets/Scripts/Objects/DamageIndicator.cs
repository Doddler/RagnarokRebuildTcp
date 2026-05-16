using System.Globalization;
using Assets.Scripts;
using Assets.Scripts.Effects;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using Assets.Scripts.UI.ConfigWindow;
using Assets.Scripts.Utility;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using UnityEngine;

public enum TextIndicatorType
{
	Damage,
	Heal,
	ComboDamage,
	Experience,
	Critical,
	Miss,
	Effect,
	Debuff
}

public class DamageIndicator
{
    public DamageIndicatorPathData PathData;
    public ServerControllable Controllable;

	private Vector3 start;
	private Vector3 end;
	private Vector3 basePosition;
	private bool isCrit;

	public Vector3 pos;
	public float size;
	public float alpha;
	public Color color;
	public float critJitter;

	public int value;

	public TextIndicatorType type;

	public float lifeTime = 0;

	public bool UseTmpFallback;
	public DamageIndicatorTmpVisual TmpVisual;

	private readonly int[] jitterSequence = { 0, -10, 10, -8, 8, -6, 6, -4, 4, -2, 2, -1, 1, 0 };

	public void AttachDamageIndicator(ServerControllable controllable)
	{
		Controllable = controllable;
		basePosition = controllable.transform.localPosition;
	}

	public void AttachComboIndicatorToControllable(ServerControllable controllable)
	{
		Controllable = controllable;
		basePosition = controllable.transform.localPosition;
	}

	public void DoDamage(TextIndicatorType type, string value, Vector3 startPosition, float height, Direction direction, string colorCode, bool isCrit)
	{
		this.type = type;
		this.isCrit = isCrit;

		color = colorCode switch
		{
			null or "" => Color.white,
			"green" => Color.green,
			"red" => Color.red,
			"blue" => new Color(0.251f, 0.486f, 1f, 1f),
			_ when ColorUtility.TryParseHtmlString(colorCode, out var parsed) => parsed,
			_ => Color.white
		};

		PathData = type switch
		{
			TextIndicatorType.Heal => ClientConstants.Instance.HealPath,
			TextIndicatorType.ComboDamage => ClientConstants.Instance.ComboPath,
			TextIndicatorType.Experience => ClientConstants.Instance.ExpPath,
			TextIndicatorType.Miss => ClientConstants.Instance.MissPath,
			TextIndicatorType.Critical => ClientConstants.Instance.CriticalPath,
			TextIndicatorType.Effect => ClientConstants.Instance.EffectPath,
			TextIndicatorType.Debuff => ClientConstants.Instance.DebuffPath,
			_ => ClientConstants.Instance.DamagePath
		};

		direction = direction.GetIntercardinalDirection();

		var isBakedMiss = type == TextIndicatorType.Miss && value == "Miss";
		var isNumeric = int.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out this.value);
		UseTmpFallback = !isBakedMiss && !isNumeric;

		if (UseTmpFallback)
		{
			TmpVisual = RagnarokEffectPool.GetTmpDamageVisual();
			if (TmpVisual.TextObject != null)
			{
				TmpVisual.TextObject.text = value ?? string.Empty;
				var c = color;
				c.a = 1f;
				TmpVisual.TextObject.color = c;
			}
			if (TmpVisual.CritSprite != null)
			{
				TmpVisual.CritSprite.gameObject.SetActive(isCrit);
				if (isCrit)
					TmpVisual.CritSprite.Reset();
			}
		}

		var vec = -direction.GetVectorValue();
		var dirVector = new Vector3(vec.x, 0, vec.y);

		start = new Vector3(startPosition.x, startPosition.y + height * 1.25f, startPosition.z);
		if (PathData.FliesAwayFromTarget)
			end = start + dirVector * PathData.DistanceMultiplier;
		else
			end = start;

		Controllable = null;
		basePosition = Vector3.zero;
		lifeTime = 0;
	}

	public void EndDamageIndicator()
	{
		lifeTime = 99;
	}

	public bool ShouldRender()
	{
		return lifeTime <= 1;
	}

	public void OnUpdate()
	{
		if (Controllable)
		{
			basePosition = Controllable.transform.localPosition;
		}

		if (lifeTime > 1)
		{
			pos = new Vector3(0, -5000, 0);
			size = 0;
			return;
		}

		size = PathData.Size.Evaluate(lifeTime) * GameConfig.Data.DamageNumberSize * PathData.SizeScale;
		pos = Vector3.Lerp(start, end, lifeTime) + basePosition;
		pos.y += PathData.Trajectory.Evaluate(lifeTime) * PathData.HeightMultiplier;
		alpha = PathData.Alpha.Evaluate(lifeTime);
        
		var index = Mathf.Min(Mathf.FloorToInt((lifeTime * PathData.TweenTime) / 0.1f), jitterSequence.Length - 1);
		critJitter = jitterSequence[index] / 70f;

		lifeTime += Time.deltaTime / PathData.TweenTime;

        if (UseTmpFallback && TmpVisual != null)
        {
            ApplyToTmpVisual();
        }
	}

	private void ApplyToTmpVisual()
	{
		var t = TmpVisual.transform;
		t.localPosition = pos;
		t.localScale = new Vector3(size, size, size);

		if (TmpVisual.TextObject != null)
		{
			var c = color;
			c.a = alpha;
			TmpVisual.TextObject.color = c;
		}

		if (isCrit && TmpVisual.CritSprite != null && TmpVisual.CritSprite.SpriteRenderer != null)
			TmpVisual.CritSprite.SpriteRenderer.color = new Color(0.8f, 0.8f, 0.8f, alpha);
	}

	public void OnRemoved()
	{
		if (UseTmpFallback && TmpVisual != null)
		{
			RagnarokEffectPool.ReturnTmpDamageVisual(TmpVisual);
			TmpVisual = null;
			UseTmpFallback = false;
		}
	}
}
