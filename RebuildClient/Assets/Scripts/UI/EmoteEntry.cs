using Assets.Scripts;
using Assets.Scripts.Sprites;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class EmoteEntry : MonoBehaviour, IPointerClickHandler
{
    public RoSpriteRendererUI SpriteRenderer;
    public TextMeshProUGUI Text;

    private int emoteId;

    public void SetEmote(int id, int sprite, int frame, Vector2 pos, float scale, string text)
    {
        emoteId = id;

        SpriteRenderer.ActionId = sprite;
        SpriteRenderer.CurrentFrame = frame;
        SpriteRenderer.OffsetPosition = pos;
        Text.text = text;

        SpriteRenderer.gameObject.transform.localScale = Vector3.one * scale;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        CameraFollower.Instance.Emote(emoteId);
    }

    public void Awake()
    {
        //SpriteRenderer.Rebuild();
    }
}
