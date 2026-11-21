namespace RebuildSharedData.Enum;

//the cart, peco and falcon are independent; only the server gives them
public enum FollowerChange : byte
{
    CartStyle, //followed by the style, 0-4
    RemoveCart,
    RemoveFalcon,
    RemovePeco
}
