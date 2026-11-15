namespace RoBotClient.Bot.State;

/// <summary>Which reply the server is waiting for, inferred from the last NPC packet received.</summary>
public enum NpcPhase { None, Dialog, Option, ShopBuy, ShopSell, Refine, Ended }

/// <summary>
/// Live NPC-conversation state, updated from the server's NpcInteraction/OpenShop packets. The behavior
/// polls a snapshot of this to drive the dialog (advance / pick option / buy / sell / close).
/// </summary>
public sealed class NpcDialogState
{
    public NpcPhase Phase;
    public int Seq;                 // bumps on every NPC packet so the driver can tell new state arrived
    public string DialogText = "";
    public readonly List<string> Options = new();
    public readonly List<(int itemId, int price)> ShopItems = new();

    public NpcDialogState Clone()
    {
        var c = new NpcDialogState { Phase = Phase, Seq = Seq, DialogText = DialogText };
        c.Options.AddRange(Options);
        c.ShopItems.AddRange(ShopItems);
        return c;
    }
}
