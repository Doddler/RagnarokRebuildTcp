using System;
using System.Collections.Generic;
using System.Text;

namespace RebuildSharedData.ClientTypes;

//disable null string warning on all our csv and data stuff
#pragma warning disable CS8618

[Serializable]
public class EffectTypeEntry
{
    public int Id;
    public string Name;
    public bool ImportEffect;
    public bool Billboard;
    public string? StrFile;
    public string? SoundFile;
    public float Offset;
    public string? PrefabName;
}

[Serializable]
public class EffectTypeList
{
    public List<EffectTypeEntry> Effects;
}

