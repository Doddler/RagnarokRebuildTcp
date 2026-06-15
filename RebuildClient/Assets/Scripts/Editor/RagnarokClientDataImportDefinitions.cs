namespace Assets.Editor
{
    internal static class RagnarokClientDataImportDefinitions
    {
        internal const string MapConfigurationPath = "Assets/StreamingAssets/ClientConfigGenerated/maps.json";
        internal const string MapSceneDirectory = "Assets/Scenes/Maps";
        internal const string WaterSourceRelativePath = "texture/워터";
        internal const string WaterDestinationDirectory = "Assets/Maps/Texture/Water";

        internal readonly struct JobSpriteMapping
        {
            public readonly string SourceName;
            public readonly string DestinationName;

            public JobSpriteMapping(string sourceName, string destinationName)
            {
                SourceName = sourceName;
                DestinationName = destinationName;
            }
        }

        internal readonly struct FixedFileImport
        {
            public readonly string SourceRelativePath;
            public readonly string DestinationPath;

            public FixedFileImport(string sourceRelativePath, string destinationPath)
            {
                SourceRelativePath = sourceRelativePath;
                DestinationPath = destinationPath;
            }
        }

        internal readonly struct TemporaryMonsterAlias
        {
            public readonly string SourceSpriteName;
            public readonly string AliasSpriteName;

            public TemporaryMonsterAlias(string sourceSpriteName, string aliasSpriteName)
            {
                SourceSpriteName = sourceSpriteName;
                AliasSpriteName = aliasSpriteName;
            }
        }

        internal static readonly JobSpriteMapping[] JobSpriteMappings =
        {
            new JobSpriteMapping("성직자", "Acolyte"),
            new JobSpriteMapping("궁수", "Archer"),
            new JobSpriteMapping("마법사", "Mage"),
            new JobSpriteMapping("상인", "Merchant"),
            new JobSpriteMapping("초보자", "Novice"),
            new JobSpriteMapping("검사", "Swordsman"),
            new JobSpriteMapping("도둑", "Thief"),
            new JobSpriteMapping("슈퍼노비스", "SuperNovice"),
            new JobSpriteMapping("기사", "Knight"),
            new JobSpriteMapping("위저드", "Wizard"),
            new JobSpriteMapping("프리스트", "Priest"),
            new JobSpriteMapping("헌터", "Hunter"),
            new JobSpriteMapping("어세신", "Assassin"),
            new JobSpriteMapping("제철공", "Blacksmith"),
            new JobSpriteMapping("크루세이더", "Crusader"),
            new JobSpriteMapping("세이지", "Sage"),
            new JobSpriteMapping("바드", "Bard"),
            new JobSpriteMapping("무희", "Dancer"),
            new JobSpriteMapping("몽크", "Monk"),
            new JobSpriteMapping("로그", "Rogue"),
            new JobSpriteMapping("연금술사", "Alchemist"),
            new JobSpriteMapping("운영자", "GameMaster"),
            new JobSpriteMapping("신페코크루세이더", "PecoCrusader"),
            new JobSpriteMapping("페코페코_기사", "PecoKnight")
        };

        internal static readonly string[] ShieldSpriteSourceNameExceptions =
        {
            "운영자",
            "신페코크루세이더",
            "페코페코_기사"
        };

        internal static readonly FixedFileImport[] MiscellaneousFiles =
        {
            new FixedFileImport("sprite/cursors.act", "Assets/Sprites/Misc/cursors.act"),
            new FixedFileImport("sprite/cursors.spr", "Assets/Sprites/Misc/cursors.spr"),
            new FixedFileImport("sprite/이팩트/emotion.act", "Assets/Sprites/Misc/emotion.act"),
            new FixedFileImport("sprite/이팩트/emotion.spr", "Assets/Sprites/Misc/emotion.spr"),
            new FixedFileImport("sprite/이팩트/숫자.act", "Assets/Sprites/Misc/damagenumbers.act"),
            new FixedFileImport("sprite/이팩트/숫자.spr", "Assets/Sprites/Misc/damagenumbers.spr")
        };

        internal static readonly TemporaryMonsterAlias[] TemporaryMonsterAliases =
        {
            new TemporaryMonsterAlias("andre", "andre_larva"),
            new TemporaryMonsterAlias("deniro", "deniro_larva"),
            new TemporaryMonsterAlias("piere", "piere_larva"),
            new TemporaryMonsterAlias("andre", "soldier_andre"),
            new TemporaryMonsterAlias("deniro", "soldier_deniro"),
            new TemporaryMonsterAlias("piere", "soldier_piere"),
            new TemporaryMonsterAlias("vagabond_wolf", "were_wolf"),
            new TemporaryMonsterAlias("frilldora", "raptice"),
            new TemporaryMonsterAlias("poison_spore", "deathspore")
        };
    }
}
