using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Scripts.Editor
{
    public static class RagnarokDirectory
    {
        // These definitions could possibly come from an external file later on
        private static readonly Dictionary<string, string> RelativeDirectoryConversionSounds = new()
        {
            {Path.Combine("wav"), Path.Combine("Sounds")},
            {Path.Combine("wav", "effect"), Path.Combine("Sounds", "Effects")},
        };

        private static readonly Dictionary<string, string> RelativeDirectoryConversionMonsters = new()
        {
            {Path.Combine("sprite", "몬스터"), Path.Combine("Sprites", "Monsters")}
        };
        
        private static readonly Dictionary<string, string> RelativeDirectoryConversionPlayerHead = new()
        {
            {Path.Combine("sprite", "인간족", "머리통", "남"), Path.Combine("Sprites", "Characters", "HeadMale")},
            {Path.Combine("sprite", "인간족", "머리통", "여"), Path.Combine("Sprites", "Characters", "HeadFemale")},
        };
        
        private static readonly Dictionary<string, string> RelativeDirectoryConversionPlayerBody = new()
        {
            {Path.Combine("sprite", "인간족", "몸통", "남"), Path.Combine("Sprites", "Characters", "BodyMale")},
            {Path.Combine("sprite", "인간족", "몸통", "여"), Path.Combine("Sprites", "Characters", "BodyFemale")},
        };
        
        private static readonly Dictionary<string, string> RelativeDirectoryConversionPlayerHeadgear = new()
        {
            {Path.Combine("sprite", "악세사리", "남"), Path.Combine("Sprites", "Headgear", "Male")},
            {Path.Combine("sprite", "악세사리", "여"), Path.Combine("Sprites", "Headgear", "Female")},
            
        };
        
        private static readonly Dictionary<string, string> RelativeDirectoryConversionWeapons = new()
        {
            {Path.Combine("sprite", "인간족", "성직자"), Path.Combine("Sprites", "Weapons", "Acolyte")},
            {Path.Combine("sprite", "인간족", "궁수"), Path.Combine("Sprites", "Weapons", "Archer")},
            {Path.Combine("sprite", "인간족", "마법사"), Path.Combine("Sprites", "Weapons", "Mage")},
            {Path.Combine("sprite", "인간족", "상인"), Path.Combine("Sprites", "Weapons", "Merchant")},
            {Path.Combine("sprite", "인간족", "초보자"), Path.Combine("Sprites", "Weapons", "Novice")},
            {Path.Combine("sprite", "인간족", "검사"), Path.Combine("Sprites", "Weapons", "Swordsman")},
            {Path.Combine("sprite", "인간족", "도둑"), Path.Combine("Sprites", "Weapons", "Thief")},
            {Path.Combine("sprite", "인간족", "슈퍼노비스"), Path.Combine("Sprites", "Weapons", "SuperNovice")},
            {Path.Combine("sprite", "인간족", "기사"), Path.Combine("Sprites", "Weapons", "Knight")},
            {Path.Combine("sprite", "인간족", "위저드"), Path.Combine("Sprites", "Weapons", "Wizard")},
            {Path.Combine("sprite", "인간족", "프리스트"), Path.Combine("Sprites", "Weapons", "Priest")},
            {Path.Combine("sprite", "인간족", "헌터"), Path.Combine("Sprites", "Weapons", "Hunter")},
            {Path.Combine("sprite", "인간족", "어세신"), Path.Combine("Sprites", "Weapons", "Assassin")},
            {Path.Combine("sprite", "인간족", "제철공"), Path.Combine("Sprites", "Weapons", "Blacksmith")},
            {Path.Combine("sprite", "인간족", "크루세이더"), Path.Combine("Sprites", "Weapons", "Crusader")},
            {Path.Combine("sprite", "인간족", "세이지"), Path.Combine("Sprites", "Weapons", "Sage")},
            {Path.Combine("sprite", "인간족", "바드"), Path.Combine("Sprites", "Weapons", "Bard")},
            {Path.Combine("sprite", "인간족", "무희바지"), Path.Combine("Sprites", "Weapons", "Dancer")},
            {Path.Combine("sprite", "인간족", "몽크"), Path.Combine("Sprites", "Weapons", "Monk")},
            {Path.Combine("sprite", "인간족", "로그"), Path.Combine("Sprites", "Weapons", "Rogue")},
            {Path.Combine("sprite", "인간족", "연금술사"), Path.Combine("Sprites", "Weapons", "Alchemist")},
            {Path.Combine("sprite", "인간족", "운영자"), Path.Combine("Sprites", "Weapons", "GameMaster")},
            {Path.Combine("sprite", "인간족", "신페코크루세이더"), Path.Combine("Sprites", "Weapons", "PecoCrusader")},
            {Path.Combine("sprite", "인간족", "페코페코_기사_남"), Path.Combine("Sprites", "Weapons", "PecoKnight")},
        };
        
        private static readonly Dictionary<string, string> RelativeDirectoryConversionShields = new()
        {
            {Path.Combine("sprite", "방패", "성직자"), Path.Combine("Sprites", "Shields", "Acolyte")},
            {Path.Combine("sprite", "방패", "궁수"), Path.Combine("Sprites", "Shields", "Archer")},
            {Path.Combine("sprite", "방패", "마법사"), Path.Combine("Sprites", "Shields", "Mage")},
            {Path.Combine("sprite", "방패", "상인"), Path.Combine("Sprites", "Shields", "Merchant")},
            {Path.Combine("sprite", "방패", "초보자"), Path.Combine("Sprites", "Shields", "Novice")},
            {Path.Combine("sprite", "방패", "검사"), Path.Combine("Sprites", "Shields", "Swordsman")},
            {Path.Combine("sprite", "방패", "도둑"), Path.Combine("Sprites", "Shields", "Thief")},
            {Path.Combine("sprite", "방패", "슈퍼노비스"), Path.Combine("Sprites", "Shields", "SuperNovice")},
            {Path.Combine("sprite", "방패", "기사"), Path.Combine("Sprites", "Shields", "Knight")},
            {Path.Combine("sprite", "방패", "위저드"), Path.Combine("Sprites", "Shields", "Wizard")},
            {Path.Combine("sprite", "방패", "프리스트"), Path.Combine("Sprites", "Shields", "Priest")},
            {Path.Combine("sprite", "방패", "헌터"), Path.Combine("Sprites", "Shields", "Hunter")},
            {Path.Combine("sprite", "방패", "어세신"), Path.Combine("Sprites", "Shields", "Assassin")},
            {Path.Combine("sprite", "방패", "제철공"), Path.Combine("Sprites", "Shields", "Blacksmith")},
            {Path.Combine("sprite", "방패", "크루세이더"), Path.Combine("Sprites", "Shields", "Crusader")},
            {Path.Combine("sprite", "방패", "세이지"), Path.Combine("Sprites", "Shields", "Sage")},
            {Path.Combine("sprite", "방패", "바드"), Path.Combine("Sprites", "Shields", "Bard")},
            {Path.Combine("sprite", "방패", "무희바지"), Path.Combine("Sprites", "Shields", "Dancer")},
            {Path.Combine("sprite", "방패", "몽크"), Path.Combine("Sprites", "Shields", "Monk")},
            {Path.Combine("sprite", "방패", "로그"), Path.Combine("Sprites", "Shields", "Rogue")},
            {Path.Combine("sprite", "방패", "연금술사"), Path.Combine("Sprites", "Shields", "Alchemist")},
            {Path.Combine("sprite", "방패", "운영자"), Path.Combine("Sprites", "Shields", "GameMaster")},
            {Path.Combine("sprite", "방패", "신페코크루세이더"), Path.Combine("Shields", "PecoCrusader")},
            {Path.Combine("sprite", "방패", "페코페코_기사_남"), Path.Combine("Sprites", "Shields", "PecoKnight")},
        };
        
        private static readonly Dictionary<string, string> RelativeDirectoryConversionNpc = new()
        {
            {Path.Combine("sprite", "npc"), Path.Combine("Sprites", "Npcs")},
        };
        
        private static readonly Dictionary<string, string> RelativeDirectoryConversionPlayerPalette = new()
        {
            {Path.Combine("palette", "몸"), Path.Combine("Sprites", "Characters")}, //Need way to separate male/female since the split is on files and not directory
        };
        
        private static readonly Dictionary<string, string> RelativeDirectoryConversionCutins = new()
        {
            {Path.Combine("texture", "유저인터페이스", "illust"), Path.Combine("Sprites", "Cutins")}
        };
        
        public static readonly string[] ExpectedMiscFiles = 
        {
            Path.Combine("sprite", "cursors.act"), Path.Combine("sprite", "cursors.spr"),
            Path.Combine("sprite", "이팩트", "emotion.act"), Path.Combine("sprite", "이팩트", "emotion.spr"),
            Path.Combine("sprite", "이팩트", "숫자.act"), Path.Combine("sprite", "이팩트", "숫자.spr")
        };
        
        public static readonly string[] PalPlayerDirectories = RelativeDirectoryConversionPlayerPalette.Keys.ToArray();
        public static readonly string[] ImgCutinDirectories = RelativeDirectoryConversionCutins.Keys.ToArray();

        public static readonly string[][] ActDirectories =
        {
            RelativeDirectoryConversionMonsters.Keys.ToArray(),
            RelativeDirectoryConversionPlayerHead.Keys.ToArray(),
            RelativeDirectoryConversionPlayerBody.Keys.ToArray(),
            RelativeDirectoryConversionPlayerHeadgear.Keys.ToArray(),
            RelativeDirectoryConversionWeapons.Keys.ToArray(),
            RelativeDirectoryConversionShields.Keys.ToArray(),
            RelativeDirectoryConversionNpc.Keys.ToArray(),
        };
        
        public enum ActDirectoriesID
        {
            ActMonstersDirectories = 0,
            ActPlayerHeadDirectories,
            ActPlayerBodyDirectories,
            ActPlayerHeadgearDirectories,
            ActWeaponsDirectories,
            ActShieldsDirectories,
            ActNpcDirectories
        }
        
        public static readonly Dictionary<string, string> RelativeDirectoryConversion = new Dictionary<string, string>()
            .Concat(RelativeDirectoryConversionSounds)
            .Concat(RelativeDirectoryConversionMonsters)
            .Concat(RelativeDirectoryConversionPlayerHeadgear)
            .Concat(RelativeDirectoryConversionPlayerHead)
            .Concat(RelativeDirectoryConversionPlayerBody)
            .Concat(RelativeDirectoryConversionPlayerPalette)
            .Concat(RelativeDirectoryConversionWeapons)
            .Concat(RelativeDirectoryConversionShields)
            .Concat(RelativeDirectoryConversionNpc)
            .Concat(RelativeDirectoryConversionCutins)
            .ToDictionary(entry => entry.Key, entry => entry.Value);
        
        public static string GetRagnarokDataDirectory
        {
            get
            {
                var path = EditorPrefs.GetString("RagnarokDataPath", null);
                return path == null ? throw new DirectoryNotFoundException("You must set a ragnarok data directory first!") : Path.Combine(path);
            }
        }

        //alternative that does not throw an exception
        public static string GetRagnarokDataDirectorySafe
        {
            get
            {
                var path = EditorPrefs.GetString("RagnarokDataPath", null);
                return Path.Combine(path);
            }
        }


        [MenuItem("Ragnarok/Set Ragnarok Data Directory", priority = 0)]
        public static void SetDataDirectory()
        {
            var defaultName = "Data";
            var oldPath = EditorPrefs.GetString("RagnarokDataPath", null);
            if (!string.IsNullOrWhiteSpace(oldPath) && Directory.Exists(oldPath))
            {
                var di = new DirectoryInfo(oldPath);
                oldPath = di.Parent?.FullName;
                defaultName = di.Name;
            }
            var path = EditorUtility.SaveFolderPanel("Locate Ragnarok Data Folder", oldPath, defaultName);
            if (Directory.Exists(path))
            {
                EditorPrefs.SetString("RagnarokDataPath", path);
                Debug.Log("Ragnarok data directory set to: " + path);
            }
            else
                Debug.LogWarning("Failed to set data directory. Using old directory: " + EditorPrefs.GetString("RagnarokDataPath", null));
        }

        [MenuItem("Ragnarok/Open Ragnarok Data Directory", priority = 1)]
        public static void OpenDataDirectory()
        {
            var oldPath = EditorPrefs.GetString("RagnarokDataPath", null);
            EditorUtility.RevealInFinder(oldPath);
        }
    }
}
