using System.Collections;
using System.Collections.Generic;
using System.Text;
using Assets.Scripts;
using Assets.Scripts.Effects;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using Assets.Scripts.Utility;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using TMPro;
using UnityEngine;
using Utility;

public enum TextIndicatorType
{
	Damage,
	Heal,
	ComboDamage
}

public class DamageIndicator : MonoBehaviour
{
	public TextMeshPro TextObject;
	// public AnimationCurve Trajectory;
	// public AnimationCurve Size;
	// public AnimationCurve Alpha;
 //    public bool FliesAwayFromTarget = true;
 //    public int HeightMultiplier = 6;
 //    public float TweenTime = 1f;

    public DamageIndicatorPathData PathData;
    public ServerControllable Controllable;

	private static StringBuilder sb = new StringBuilder(128);

	private Vector3 start;
	private Vector3 end;

	public void AttachComboIndicatorToControllable(ServerControllable controllable)
	{
		RemoveComboIndicatorIfExists(controllable);
		Controllable = controllable;
		Controllable.ComboIndicator = gameObject;
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
	
	public void DoDamage(TextIndicatorType type, string value, Vector3 startPosition, float height, Direction direction, bool isRed, bool isCrit)
	{
		PathData = type switch
		{
			TextIndicatorType.Heal => ClientConstants.Instance.HealPath,
			TextIndicatorType.ComboDamage => ClientConstants.Instance.ComboPath,
			_ => ClientConstants.Instance.DamagePath
		};
	    
        direction = direction.GetIntercardinalDirection();
		var text = value.ToString();

		if (isRed)
			sb.Append("<color=#FF0000>");

		var useTrueType = CameraFollower.Instance.UseTTFDamage;
        if (!int.TryParse(value, out var _))
            useTrueType = true;

		if (useTrueType)
			sb.Append("<cspace=0.4>");

		foreach (var c in text)
		{
			if(!useTrueType)
				sb.Append("<sprite=");
			sb.Append(c);
			if (!useTrueType)
			{
				if (isRed)
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
            end = start + dirVector * 4;
        else
            end = start;

        transform.parent = null;
		transform.localPosition = start;
		
		gameObject.SetActive(true);

		var lt = LeanTween.value(gameObject, OnUpdate, 0, 1, PathData.TweenTime);
		lt.setOnComplete(onComplete: OnComplete);
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
		var height = PathData.Trajectory.Evaluate(f);
		var size = PathData.Size.Evaluate(f);
		var pos = Vector3.Lerp(start, end, f);
		var alpha = PathData.Alpha.Evaluate(f);

		transform.localPosition = new Vector3(pos.x, pos.y + height * PathData.HeightMultiplier, pos.z);
		transform.localScale = new Vector3(size, size, size);
		TextObject.color = new Color(1, 1, 1, alpha);
	}

}
