using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Scripts.Editor;
using Assets.Scripts.MapEditor.Editor;
using Assets.Scripts.Sprites;
using RebuildSharedData.ClientTypes;
using UnityEditor;
using UnityEngine;

namespace Assets.Editor
{
    public partial class RagnarokCopyFromRealClient
    {
        private static readonly string[] RagnarokConfigFiles =
        {
            "Assets/StreamingAssets/ClientConfigGenerated/maps.json",
            "Assets/StreamingAssets/ClientConfigGenerated/monsterclass.json",
            "Assets/StreamingAssets/ClientConfigGenerated/monsterdatabase.json",
            "Assets/StreamingAssets/ClientConfigGenerated/npcdatabase.json",
            "Assets/StreamingAssets/ClientConfigGenerated/items.json",
            "Assets/StreamingAssets/ClientConfigGenerated/playerclass.json",
            "Assets/StreamingAssets/ClientConfigGenerated/skillinfo.json",
            "Assets/StreamingAssets/ClientConfigGenerated/skilltree.json",
            "Assets/StreamingAssets/ClientConfigGenerated/effects.json",
            "Assets/StreamingAssets/ClientConfig/headdata.json"
        };

        private sealed class ImportSelection
        {
            public readonly HashSet<string> Maps = new(StringComparer.OrdinalIgnoreCase);
            public readonly HashSet<int> ItemIds = new();
            public readonly HashSet<int> SkillIds = new();
            public readonly HashSet<string> MonsterSprites = new(StringComparer.OrdinalIgnoreCase);
            public readonly HashSet<string> NpcSprites = new(StringComparer.OrdinalIgnoreCase);
            public readonly HashSet<string> EffectNames = new(StringComparer.OrdinalIgnoreCase);
            public readonly HashSet<string> EffectSounds = new(StringComparer.OrdinalIgnoreCase);
            public readonly List<PlayerClassData> Jobs = new();
        }

        public static List<string> ValidateProfile(RagnarokCopyProfile profile)
        {
            var errors = new List<string>();
            if (profile == null)
            {
                errors.Add("The profile JSON could not be loaded.");
                return errors;
            }

            profile.EnsureDefaults();
            if (string.IsNullOrWhiteSpace(profile.name))
                errors.Add("Profile name is required.");

            var dataDir = RagnarokDirectory.GetRagnarokDataDirectorySafe;
            if (string.IsNullOrWhiteSpace(dataDir) || !Directory.Exists(dataDir))
            {
                errors.Add("Set a valid Ragnarok data directory first.");
                return errors;
            }

            foreach (var path in RagnarokConfigFiles.Where(path => !File.Exists(path)))
                errors.Add($"Required generated configuration is missing: {path}");

            if (errors.Count == 0)
                BuildImportSelection(profile, dataDir, errors);

            return errors;
        }

        public static void CopyFromProfile(RagnarokCopyProfile profile, string dataFolderPath)
        {
            var errors = ValidateProfile(profile);
            if (errors.Count > 0)
            {
                EditorUtility.DisplayDialog("Copy Data Profile", string.Join("\n", errors), "OK");
                return;
            }

            var dataDir = dataFolderPath;
            var prompt = $"Import assets from the '{profile.name}' profile? Existing imported assets will be kept. A full copy can take hours.";
            if (!EditorUtility.DisplayDialog("Copy Data Profile", prompt, "Import", "Cancel"))
                return;

            try
            {
                if (profile.all)
                {
                    CopyFullProfileData(dataDir);
                    EditorUtility.DisplayDialog("Copy Data Profile", $"Profile '{profile.name}' is complete.", "OK");
                    return;
                }

                var selectionErrors = new List<string>();
                var selection = BuildImportSelection(profile, dataDir, selectionErrors);
                var copied = CopyRawFiles(dataDir, selection);
                copied += ImportCommonResources(dataDir);

                EffectStrImporter.Import(selection.EffectNames);
                RagnarokMapImporterWindow.ImportAllMissingMaps(selection.Maps);
                ItemIconImporter.ImportItems(selection.ItemIds, selection.SkillIds, replaceAtlas: false);
                RagnarokMapImporterWindow.UpdateAddressables(processModels: false);
                RoLightingManagerWindow.CreateOrOpen();

                Debug.Log(
                    $"[Ragnarok Copy Utility] " +
                    $"Profile: {profile.name}] complete. Copied {copied} raw file(s), " +
                    $"selected {selection.Maps.Count} map(s), {selection.Jobs.Count} job(s), " +
                    $"{selection.MonsterSprites.Count} monster sprite(s), {selection.NpcSprites.Count} NPC sprite(s), " +
                    $"{selection.ItemIds.Count} item icon(s), and {selection.SkillIds.Count} skill icon(s)."
                );
                EditorUtility.DisplayDialog(
                    "[Ragnarok Copy Utility] ",
                    $"Profile '{profile.name}' is complete. Open the main Unity scene and enter Play mode.",
                    "OK"
                );
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog(
                    "[Ragnarok Copy Utility]",
                    $"Profile: {profile.name} failed. The import stopped with an error. See the Unity Console for details.\n\n" + exception.Message,
                    "OK"
                );
            }
        }

        private static ImportSelection BuildImportSelection(
            RagnarokCopyProfile profile,
            string dataDir,
            List<string> errors)
        {
            var selection = new ImportSelection();
            var resources = profile.resources;
            selection.Maps.UnionWith(resources.maps.Where(value => !string.IsNullOrWhiteSpace(value)));
            selection.ItemIds.UnionWith(resources.items);
            selection.SkillIds.UnionWith(resources.skills);
            selection.EffectNames.UnionWith(resources.effects.Where(value => !string.IsNullOrWhiteSpace(value)));

            try
            {
                ResolveMaps(selection, dataDir, errors);
                ResolveMonsters(resources, selection, errors);

                ResolveNpcs(resources, selection, errors);
                ResolveJobs(resources, selection, errors);

                ResolveJobSkills(selection);
                ResolveEffects(selection, errors);
                ResolveItems(selection, errors);
                ResolveSkills(selection, errors);

                WarnForMissingSprites(selection, dataDir);
                WarnForMissingHeads(dataDir);
                WarnForMissingBaselineFiles(dataDir);
            }
            catch (Exception exception)
            {
                errors.Add("Could not read generated client configuration: " + exception.Message);
            }

            return selection;
        }

        private static void ResolveMaps(
            ImportSelection selection,
            string dataDir,
            List<string> errors)
        {
            var maps = JsonUtility.FromJson<Wrapper<ClientMapEntry>>(File.ReadAllText(RagnarokConfigFiles[0])).Items;
            var knownMaps = new HashSet<string>(maps.Select(map => map.Code), StringComparer.OrdinalIgnoreCase);
            foreach (var map in selection.Maps.Where(map => !knownMaps.Contains(map)))
                errors.Add($"Unknown map code: {map}");
            foreach (var map in selection.Maps)
                foreach (var extension in new[] { ".gnd", ".gat", ".rsw" })
                    if (!File.Exists(Path.Combine(dataDir, map + extension)))
                        Debug.LogWarning($"[Ragnarok Copy Utility] Map source file is missing: {map}{extension}");
        }

        private static void ResolveMonsters(
            RagnarokCopyProfileResources resources,
            ImportSelection selection,
            List<string> errors)
        {
            var monsterDatabase = JsonUtility.FromJson<MonsterDbFile>(File.ReadAllText(RagnarokConfigFiles[2]));
            var monsterClasses = JsonUtility.FromJson<Wrapper<MonsterClassData>>(File.ReadAllText(RagnarokConfigFiles[1]))
                .Items.ToDictionary(monster => monster.Id);
            var requestedMonsterCodes = new HashSet<string>(resources.monsters, StringComparer.OrdinalIgnoreCase);
            foreach (var code in requestedMonsterCodes.Where(code =>
                         monsterDatabase.Items.All(monster => !string.Equals(monster.Code, code, StringComparison.OrdinalIgnoreCase))))
                errors.Add($"Unknown monster code: {code}");

            var selectedMonsters = monsterDatabase.Items.Where(monster =>
                requestedMonsterCodes.Contains(monster.Code) ||
                (monster.Spawns ?? new List<MonsterDbSpawnEntry>()).Any(spawn => selection.Maps.Contains(spawn.Map)));
            foreach (var monster in selectedMonsters)
            {
                if (monsterClasses.TryGetValue(monster.Id, out var monsterClass) &&
                    !string.IsNullOrWhiteSpace(monsterClass.SpriteName) &&
                    !monsterClass.SpriteName.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    selection.MonsterSprites.Add(monsterClass.SpriteName);
                foreach (var drop in monster.Drops ?? new List<MonsterDbDropEntry>())
                    selection.ItemIds.Add(drop.ItemId);
            }
        }

        private static void ResolveNpcs(
            RagnarokCopyProfileResources resources,
            ImportSelection selection,
            List<string> errors)
        {
            var npcDatabase = JsonUtility.FromJson<NpcDbFile>(File.ReadAllText(RagnarokConfigFiles[3]));
            var requestedNpcIds = new HashSet<int>(resources.npcs);
            foreach (var id in requestedNpcIds.Where(id => npcDatabase.Items.All(npc => npc.Id != id)))
                errors.Add($"Unknown NPC ID: {id}");
            foreach (var npc in npcDatabase.Items.Where(npc =>
                         requestedNpcIds.Contains(npc.Id) || selection.Maps.Contains(npc.Map)))
            {
                if (!string.IsNullOrWhiteSpace(npc.SpriteCode))
                    selection.NpcSprites.Add(npc.SpriteCode);
                selection.ItemIds.UnionWith(npc.SellsItems ?? new List<int>());
            }
        }

        private static void ResolveJobs(
            RagnarokCopyProfileResources resources,
            ImportSelection selection,
            List<string> errors)
        {
            var playerClasses = JsonUtility.FromJson<Wrapper<PlayerClassData>>(File.ReadAllText(RagnarokConfigFiles[5])).Items;
            foreach (var jobName in new HashSet<string>(resources.jobs, StringComparer.OrdinalIgnoreCase))
            {
                var job = playerClasses.FirstOrDefault(playerClass =>
                    string.Equals(playerClass.Name, jobName, StringComparison.OrdinalIgnoreCase));
                if (job == null)
                    errors.Add($"Unknown job name: {jobName}");
                else
                    selection.Jobs.Add(job);
            }
        }

        private static void ResolveJobSkills(ImportSelection selection)
        {
            var skillTrees = JsonUtility.FromJson<Wrapper<ClientSkillTree>>(File.ReadAllText(RagnarokConfigFiles[7])).Items;
            var skillTreesByClass = skillTrees.ToDictionary(tree => tree.ClassId);
            var visitedTrees = new HashSet<int>();

            void AddJobSkills(int classId)
            {
                if (!visitedTrees.Add(classId) || !skillTreesByClass.TryGetValue(classId, out var tree))
                    return;
                foreach (var skill in tree.Skills)
                    selection.SkillIds.Add((int)skill.Skill);
                if (tree.ExtendsClass >= 0)
                    AddJobSkills(tree.ExtendsClass);
            }

            foreach (var job in selection.Jobs)
                AddJobSkills(job.Id);
        }

        private static void ResolveEffects(
            ImportSelection selection,
            List<string> errors)
        {
            var effects = JsonUtility.FromJson<EffectTypeList>(File.ReadAllText(RagnarokConfigFiles[8])).Effects;
            foreach (var effectName in selection.EffectNames.Where(effectName =>
                         effects.All(effect => !string.Equals(effect.Name, effectName, StringComparison.OrdinalIgnoreCase))))
                errors.Add($"Unknown effect name: {effectName}");
            foreach (var effect in effects.Where(effect => selection.EffectNames.Contains(effect.Name)))
                if (!string.IsNullOrWhiteSpace(effect.SoundFile))
                    selection.EffectSounds.Add(effect.SoundFile);
        }

        private static void ResolveItems(ImportSelection selection, List<string> errors)
        {
            var items = JsonUtility.FromJson<Wrapper<ItemData>>(File.ReadAllText(RagnarokConfigFiles[4])).Items;
            var knownItems = new HashSet<int>(items.Select(item => item.Id));
            foreach (var itemId in selection.ItemIds.Where(itemId => !knownItems.Contains(itemId)))
                errors.Add($"Unknown item ID: {itemId}");
        }

        private static void ResolveSkills(ImportSelection selection, List<string> errors)
        {
            var skills = JsonUtility.FromJson<Wrapper<SkillData>>(File.ReadAllText(RagnarokConfigFiles[6])).Items;
            var knownSkills = new HashSet<int>(skills.Select(skill => (int)skill.SkillId));
            foreach (var skillId in selection.SkillIds.Where(skillId => !knownSkills.Contains(skillId)))
                errors.Add($"Unknown skill ID: {skillId}");
        }

        private static void WarnForMissingSprites(ImportSelection selection, string dataDir)
        {
            foreach (var sprite in selection.MonsterSprites)
                WarnForMissingSpritePair(
                    Path.Combine(dataDir, "sprite/몬스터"),
                    Path.GetFileNameWithoutExtension(sprite)
                );
            foreach (var sprite in selection.NpcSprites)
                WarnForMissingSpritePair(
                    Path.Combine(dataDir, "sprite/npc"),
                    sprite
                );
            foreach (var job in selection.Jobs)
            {
                WarnForMissingSpritePair(
                    Path.Combine(dataDir, "sprite/인간족/몸통/남"),
                    Path.GetFileNameWithoutExtension(job.SpriteMale)
                );
                WarnForMissingSpritePair(
                    Path.Combine(dataDir, "sprite/인간족/몸통/여"),
                    Path.GetFileNameWithoutExtension(job.SpriteFemale)
                );
            }
        }

        private static void WarnForMissingHeads(string dataDir)
        {
            var heads = JsonUtility.FromJson<Wrapper<PlayerHeadData>>(File.ReadAllText(RagnarokConfigFiles[9])).Items;
            if (heads.Length == 0)
            {
                Debug.LogWarning("[Ragnarok Copy Utility] No default hairstyle is defined in headdata.json.");
                return;
            }

            foreach (var head in heads[0].MaleIds)
                WarnForMissingSpritePair(
                    Path.Combine(dataDir, "sprite/인간족/머리통/남"),
                    head
                );
            foreach (var head in heads[0].FemaleIds)
                WarnForMissingSpritePair(
                    Path.Combine(dataDir, "sprite/인간족/머리통/여"),
                    head
                );
        }

        private static void WarnForMissingBaselineFiles(string dataDir)
        {
            foreach (var file in RagnarokClientDataImportDefinitions.MiscellaneousFiles)
                if (!File.Exists(Path.Combine(dataDir, file.SourceRelativePath)))
                    Debug.LogWarning($"[Ragnarok Copy Utility] Baseline file is missing: {file.SourceRelativePath}");
        }

        private static void WarnForMissingSpritePair(string sourceDirectory, string spriteName)
        {
            if (!Directory.Exists(sourceDirectory))
            {
                Debug.LogWarning($"[Ragnarok Copy Utility] Sprite source directory is missing: {sourceDirectory}");
                return;
            }

            foreach (var extension in new[] { ".spr", ".act" })
                if (!Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.TopDirectoryOnly).Any(path =>
                        string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(Path.GetFileNameWithoutExtension(path), spriteName, StringComparison.OrdinalIgnoreCase)))
                    Debug.LogWarning($"[Ragnarok Copy Utility] Sprite file is missing: {spriteName}{extension}");
        }

        private static int CopyRawFiles(string dataDir, ImportSelection selection)
        {
            var copied = 0;

            foreach (var sprite in selection.MonsterSprites)
                copied += CopySpritePair(
                    Path.Combine(dataDir, "sprite/몬스터"),
                    "Assets/Sprites/Monsters",
                    Path.GetFileNameWithoutExtension(sprite)
                );

            foreach (var sprite in selection.NpcSprites)
                copied += CopySpritePair(
                    Path.Combine(dataDir, "sprite/npc"),
                    "Assets/Sprites/Npcs",
                    sprite
                );

            foreach (var job in selection.Jobs)
            {
                copied += CopySpritePair(
                    Path.Combine(dataDir, "sprite/인간족/몸통/남"),
                    Path.GetDirectoryName(job.SpriteMale),
                    Path.GetFileNameWithoutExtension(job.SpriteMale)
                );
                copied += CopySpritePair(
                    Path.Combine(dataDir, "sprite/인간족/몸통/여"),
                    Path.GetDirectoryName(job.SpriteFemale),
                    Path.GetFileNameWithoutExtension(job.SpriteFemale)
                );

                var mapping = RagnarokClientDataImportDefinitions.JobSpriteMappings.FirstOrDefault(candidate =>
                    string.Equals(candidate.DestinationName, job.Name, StringComparison.OrdinalIgnoreCase));
                if (string.IsNullOrWhiteSpace(mapping.DestinationName))
                {
                    Debug.LogWarning($"[Ragnarok Copy Utility] No weapon/shield sprite mapping exists for job '{job.Name}'.");
                    continue;
                }

                CopyJobProfileSprites(dataDir, mapping);
            }

            copied += CopySoundEffects(dataDir, selection.EffectSounds);

            return copied;
        }

        private static int ImportCommonResources(string dataDir)
        {
            var copied = 0;
            var heads = JsonUtility.FromJson<Wrapper<PlayerHeadData>>(
                File.ReadAllText(RagnarokConfigFiles[9])
            );
            var firstHead = heads.Items.First();
            foreach (var head in firstHead.MaleIds)
                copied += CopySpritePair(
                    Path.Combine(dataDir,
                    "sprite/인간족/머리통/남"),
                    "Assets/Sprites/Characters/HeadMale",
                    head
                );

            foreach (var head in firstHead.FemaleIds)
                copied += CopySpritePair(
                    Path.Combine(dataDir, "sprite/인간족/머리통/여"),
                    "Assets/Sprites/Characters/HeadFemale",
                    head
                );

            foreach (var file in RagnarokClientDataImportDefinitions.MiscellaneousFiles)
                if (CopyFileIfMissing(Path.Combine(dataDir, file.SourceRelativePath), file.DestinationPath))
                    copied++;

            copied += CopySoundEffects(dataDir, RagnarokClientDataImportDefinitions.CommonSoundEffects);

            AssetDatabase.Refresh();
            EffectStrImporter.ImportEffectTextures();

            return copied;
        }

        private static int CopySoundEffects(string dataDir, IEnumerable<string> sounds)
        {
            var copied = 0;
            foreach (var sound in sounds)
            {
                var wavRoot = Path.Combine(dataDir, "wav");
                if (!Directory.Exists(wavRoot))
                    continue;
                var source = Directory.GetFiles(wavRoot, sound + ".*", SearchOption.AllDirectories).FirstOrDefault();
                if (source == null)
                {
                    Debug.LogWarning($"[Ragnarok Copy Utility] Optional sound not found: {sound}");
                    continue;
                }

                var relativePath = Path.GetRelativePath(wavRoot, source);
                if (CopyFileIfMissing(source, Path.Combine("Assets/Sounds", relativePath)))
                    copied++;
            }

            return copied;
        }

        private static void CopyJobProfileSprites(
            string dataDir,
            RagnarokClientDataImportDefinitions.JobSpriteMapping mapping)
        {
            CopyFolder(
                Path.Combine(dataDir, "sprite/인간족", mapping.SourceName),
                Path.Combine("Assets/Sprites/Weapons", mapping.DestinationName),
                maleFemaleSplit: true,
                updateFileName: UpdateSpriteName
            );
            if (!RagnarokClientDataImportDefinitions.ShieldSpriteSourceNameExceptions.Contains(mapping.SourceName))
                CopyFolder(
                    Path.Combine(dataDir, "sprite/방패", mapping.SourceName),
                    Path.Combine("Assets/Sprites/Shields", mapping.DestinationName),
                    maleFemaleSplit: true,
                    updateFileName: UpdateSpriteName
                );
        }

        private static void CopyFullProfileData(string dataDir)
        {
            CopyFolder(Path.Combine(dataDir, "wav"), "Assets/Sounds", recursive: true);
            CopyFolder(Path.Combine(dataDir, "sprite/몬스터"), "Assets/Sprites/Monsters");
            CopyFolder(Path.Combine(dataDir, "sprite/악세사리/남"), "Assets/Sprites/Headgear/Male");
            CopyFolder(Path.Combine(dataDir, "sprite/악세사리/여"), "Assets/Sprites/Headgear/Female");
            CopyFolder(Path.Combine(dataDir, "sprite/npc"), "Assets/Sprites/Npcs");
            CopyFolder(Path.Combine(dataDir, "sprite/이팩트"), "Assets/Sprites/Effects");
            CopyFolder(Path.Combine(dataDir, "palette/머리"), "Assets/Sprites/Characters/HeadFemale/Palette", filter: "*_여_*.pal", updateFileName: path => path.Replace("머리", ""));
            CopyFolder(Path.Combine(dataDir, "palette/머리"), "Assets/Sprites/Characters/HeadMale/Palette", filter: "*_남_*.pal", updateFileName: path => path.Replace("머리", ""));
            CopyFolder(Path.Combine(dataDir, "sprite/인간족/머리통/남"), "Assets/Sprites/Characters/HeadMale");
            CopyFolder(Path.Combine(dataDir, "sprite/인간족/머리통/여"), "Assets/Sprites/Characters/HeadFemale");
            CopyFolder(Path.Combine(dataDir, "sprite/인간족/몸통/남"), "Assets/Sprites/Characters/BodyMale");
            CopyFolder(Path.Combine(dataDir, "sprite/인간족/몸통/여"), "Assets/Sprites/Characters/BodyFemale");
            CopyFolder(Path.Combine(dataDir, "texture/유저인터페이스/illust"), "Assets/Sprites/Cutins");

            foreach (var mapping in RagnarokClientDataImportDefinitions.JobSpriteMappings)
                CopyJobProfileSprites(dataDir, mapping);

            foreach (var file in RagnarokClientDataImportDefinitions.MiscellaneousFiles)
                CopySingleFile(Path.Combine(dataDir, file.SourceRelativePath), file.DestinationPath);

            foreach (var alias in RagnarokClientDataImportDefinitions.TemporaryMonsterAliases)
                CreateTemporarySpriteIfRequired(alias.SourceSpriteName, alias.AliasSpriteName);

            RunPostCopyProcessing(openLightingManager: true);
        }
    }
}
