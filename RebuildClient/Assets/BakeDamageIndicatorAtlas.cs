using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

public class BakeDamageIndicatorAtlas : MonoBehaviour
{
	public TMP_FontAsset numberFont;
	public TMP_FontAsset textFont;
	public RenderTexture damageIndicatorTexture;
	
	[SerializeField] private GameObject numberContainer;
	[SerializeField] private GameObject textContainer;
	[SerializeField] private GameObject critSprite;
	[SerializeField] private bool useLinearFilteringForCritSprite = false;

	private TMP_Text[] _numbers;
	private TMP_Text[] _texts;
	
	[ContextMenu("Bake Damage Indicator")]
	public void BakeAtlas()
	{
		_numbers = numberContainer.GetComponentsInChildren<TMP_Text>();
		_texts = textContainer.GetComponentsInChildren<TMP_Text>();

		foreach (var number in _numbers)
		{
			number.font = numberFont;
		}

		foreach (var text in _texts)
		{
			text.font = textFont;
		}

		var critRenderer = critSprite.GetComponent<SpriteRenderer>();
		critRenderer.sprite.texture.filterMode = useLinearFilteringForCritSprite ? FilterMode.Bilinear : FilterMode.Point;
		
		
		if (damageIndicatorTexture != null)
		{
			damageIndicatorTexture.Release();
			damageIndicatorTexture = null;
		}
		
		damageIndicatorTexture = new RenderTexture(2048, 1024, 24, DefaultFormat.LDR);
		damageIndicatorTexture.Create();

		var camera = GetComponent<Camera>();
		camera.targetTexture = damageIndicatorTexture;
		camera.clearFlags = CameraClearFlags.Color;
		camera.backgroundColor = Color.clear;
		camera.Render();
	}
}
