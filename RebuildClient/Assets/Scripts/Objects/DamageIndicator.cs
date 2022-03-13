using System.Collections;
using System.Collections.Generic;
using System.Text;
using Assets.Scripts;
using Assets.Scripts.Utility;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using TMPro;
using UnityEngine;

public class DamageIndicator : MonoBehaviour
{
	public TextMeshPro TextObject;
	public AnimationCurve Trajectory;
	public AnimationCurve Size;
	public AnimationCurve Alpha;
    public bool FliesAwayFromTarget = true;
    public int HeightMultiplier = 6;
    public float TweenTime = 1f;

	private static StringBuilder sb = new StringBuilder(128);

	private Vector3 start;
	private Vector3 end;
	
	public void DoDamage(string value, Vector3 startPosition, float height, Direction direction, bool isRed, bool isCrit)
    {
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
        if (FliesAwayFromTarget)
            end = start + dirVector * 4;
        else
            end = start;

		transform.localPosition = start;

		var lt = LeanTween.value(gameObject, OnUpdate, 0, 1, TweenTime);
		lt.setOnComplete(onComplete: () => GameObject.Destroy(gameObject));
	}

	void OnUpdate(float f)
	{
		var height = Trajectory.Evaluate(f);
		var size = Size.Evaluate(f);
		var pos = Vector3.Lerp(start, end, f);
		var alpha = Alpha.Evaluate(f);

		transform.localPosition = new Vector3(pos.x, pos.y + height * HeightMultiplier, pos.z);
		transform.localScale = new Vector3(size, size, size);
		TextObject.color = new Color(1, 1, 1, alpha);
	}

}
