namespace RebuildSharedData.ClientTypes;

[Serializable]
public class CardPrefixData
{
    public int Id;
    public string Prefix;
    public string Postfix;
}

[Serializable]
public class CardPrefixDataList
{
    public List<CardPrefixData> Items = null!;
}