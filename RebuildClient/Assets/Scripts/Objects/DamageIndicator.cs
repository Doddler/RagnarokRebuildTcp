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
	Effect
}

public class DamageIndicator : MonoBehaviour
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

	public void AttachDamageIndicator(ServerControllable controllable)
	{
		Controllable = controllable;
		basePosition = controllable.transform.localPosition;
	}
	
	public void AttachComboIndicatorToControllable(ServerControllable controllable)
	{
		RemoveComboIndicatorIfExists(controllable);
		Controllable = controllable;
		Controllable.ComboIndicator = gameObject;
		basePosition = controllable.transform.localPosition;
	}
	
	private void RemoveComboIndicatorIfExists(ServerControllable controllable)
	{
		if (controllable.ComboIndicator == null)
			return;

		var di = controllable.ComboIndicator.GetComponent<DamageIndicator>();
		if (di == null)
		{
			Destroy(controllable.ComboIndicator);
			controllable.ComboIndicator = null;
		}
		
		di.EndDamageIndicator();
	}
	
	public void DoDamage(TextIndicatorType type, string value, Vector3 startPosition, float height, Direction direction, string colorCode, bool isCrit)
	{
		PathData = type switch
		{
			TextIndicatorType.Heal => ClientConstants.Instance.HealPath,
			TextIndicatorType.ComboDamage => ClientConstants.Instance.ComboPath,
			TextIndicatorType.Experience => ClientConstants.Instance.ExpPath,
			TextIndicatorType.Miss => ClientConstants.Instance.MissPath,
			TextIndicatorType.Critical => ClientConstants.Instance.CriticalPath,
			TextIndicatorType.Effect => ClientConstants.Instance.EffectPath,
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

		TextObject.text = sb.ToString();
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
        transform.parent = null;
		transform.localPosition = start;
		
		gameObject.SetActive(true);

		var lt = LeanTween.value(gameObject, OnUpdate, 0, 1, PathData.TweenTime);
		lt.setOnComplete(onComplete: OnComplete);
		OnUpdate(0); //do one early for our first frame

		this.isCrit = isCrit;
		CritSprite.gameObject.SetActive(isCrit);
		if (isCrit)
			CritSprite.Reset();
	}

	private void OnComplete()
	{
		EndDamageIndicator(true);
	}

	public void EndDamageIndicator( bool skipCancelTween = false)
	{
		if(!skipCancelTween)
			LeanTween.cancel(gameObject);
	
		if (Controllable && Controllable.ComboIndicator == gameObject)
			Controllable.ComboIndicator = null;
			
		RagnarokEffectPool.ReturnDamageIndicator(this);
	}

	void OnUpdate(float f)
	{
		if (Controllable)
			basePosition = Controllable.transform.localPosition;
		
		var height = PathData.Trajectory.Evaluate(f);
		var size = PathData.Size.Evaluate(f);
		var pos = Vector3.Lerp(start, end, f) + basePosition;
		var alpha = PathData.Alpha.Evaluate(f);

		transform.localPosition = new Vector3(pos.x, pos.y + height * PathData.HeightMultiplier, pos.z);
		transform.localScale = new Vector3(size, size, size) * GameConfig.Data.DamageNumberSize;

		TextObject.color = new Color(1, 1, 1, alpha);
		
		if(isCrit)
			CritSprite.SpriteRenderer.color = new Color(0.8f, 0.8f, 0.8f, alpha);

	}

}
