using System;
using System.Collections.Generic;
using System.Text;

namespace RebuildSharedData.ClientTypes;

[Serializable]
public class EffectTypeEntry
{
    public int Id;
    public string Name = null!;
    public bool ImportEffect;
    public bool Billboard;
    public bool IsLooping;
    public string? StrFile;
    public string? Sprite;
    public string? SoundFile;
    public float Offset;
    public string? PrefabName;
}

[Serializable]
public class EffectTypeList
{
    public List<EffectTypeEntry> Effects = null!;
}

