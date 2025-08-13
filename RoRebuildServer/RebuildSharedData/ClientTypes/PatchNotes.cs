namespace RebuildSharedData.ClientTypes;

[Serializable]
public class PatchNotes
{
    public string? Date;
    public string? Desc;
}

[Serializable]
public class PatchNotesList
{
    public List<PatchNotes> Items = null!;
}