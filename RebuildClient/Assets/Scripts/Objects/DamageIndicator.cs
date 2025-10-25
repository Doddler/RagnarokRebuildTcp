using System.Globalization;
using System.Text;
using Assets.Scripts;
using Assets.Scripts.Effects;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using Assets.Scripts.UI.ConfigWindow;
using Assets.Scripts.Utility;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using TMPro;
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

public class DamageIndicator// : MonoBehaviour
{
	public TextMeshPro TextObject;

    public DamageIndicatorPathData PathData;
    public ServerControllable Controllable;
    public CriticalDamageSprite CritSprite;

	private static StringBuilder sb = new StringBuilder(128);

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
		int.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out this.value);
		if (type == TextIndicatorType.Experience) this.value = int.Parse(value.Split(' ')[0]);
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
		// Debug.Log($"{Time.timeSinceLevelLoad} DoDamage {direction} {direction.GetIntercardinalDirection()}");
		
        direction = direction.GetIntercardinalDirection();
		var text = value.ToString();
		var hasColor = !string.IsNullOrEmpty(colorCode);

		if (hasColor)
			sb.Append($"<color={colorCode}>");

		var useTrueType = CameraFollower.Instance.UseTTFDamage;
        if (!int.TryParse(value, out var _))
            useTrueType = true;
		//
		// if (useTrueType)
		// 	sb.Append("<cspace=0.4>");

		foreach (var c in text)
		{
			if(!useTrueType)
				sb.Append("<sprite=");
			sb.Append(c);
			if (!useTrueType)
			{
				if (hasColor)
					sb.Append(" tint");
				sb.Append(">");
			}
		}

		//TextObject.text = sb.ToString();
		//Debug.Log(sb.ToString());
		sb.Clear();
		
		var vec = -direction.GetVectorValue();
		var dirVector = new Vector3(vec.x, 0, vec.y);

		start = new Vector3(startPosition.x, startPosition.y + height * 1.25f, startPosition.z);
		//end = start;
        if (PathData.FliesAwayFromTarget)
            end = start + dirVector * PathData.DistanceMultiplier;
        else
            end = start;

        Controllable = null;
        basePosition = Vector3.zero;
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
		
		size = PathData.Size.Evaluate(lifeTime) * GameConfig.Data.DamageNumberSize;
		pos = Vector3.Lerp(start, end, lifeTime) + basePosition;
		pos.y += PathData.Trajectory.Evaluate(lifeTime) * PathData.HeightMultiplier;
		alpha = PathData.Alpha.Evaluate(lifeTime);

		var index = Mathf.Min(Mathf.FloorToInt((lifeTime * PathData.TweenTime) / 0.1f), jitterSequence.Length - 1);
		critJitter = jitterSequence[index] / 70f;
		
		lifeTime += Time.deltaTime / PathData.TweenTime;
	}
}
