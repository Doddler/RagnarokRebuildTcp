using System.IO;
using Assets.Scripts.Sprites;
using Assets.Scripts.Editor.Sprites;
using NUnit.Framework;

namespace Assets.Scripts.Editor.Tests
{
    class RoSpriteTests
    {
        private static readonly RoCompressedBitmapImage RoCompressedImage = new RoCompressedBitmapImage
        {
            ImageHeight = 5,
            ImageWidth = 5,
            CompressedSize = 15,
            CompressedPaletteIndexes = new byte[]
            {
                0x00, 0x05, 0x02, 0x03, 0x00,
                0x04, 0x03, 0x09, 0x08, 0x00,
                0x02, 0x04, 0x00, 0x07, 0x07,
            }
        };

        private static readonly RoBitmapImage DecompressedImage = new RoBitmapImage
        {
            ImageHeight = 5,
            ImageWidth = 5,
            PaletteIndexes = new byte[]
            {
                0x00, 0x00, 0x00, 0x00, 0x00,
                0x02, 0x03, 0x00, 0x00, 0x00,
                0x00, 0x03, 0x09, 0x08, 0x00,
                0x00, 0x04, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x07,
            }
        };

        public class PreviewHarness : UnityEditor.EditorWindow
        {
            public RoSpriteAssetInspector Inspector;
            public bool? ParentStayedDisabledOnClick;
            public UnityEngine.Vector2 ZoomClickPosition;
            private void OnGUI()
            {
                if (Inspector == null) return;
                var eventType = UnityEngine.Event.current.type;
                var wasEnabled = UnityEngine.GUI.enabled;
                try
                {
                    UnityEngine.GUI.enabled = false;
                    Inspector.OnInspectorGUI();
                    if (eventType == UnityEngine.EventType.MouseDown)
                        ParentStayedDisabledOnClick = !UnityEngine.GUI.enabled;
                    if (eventType == UnityEngine.EventType.Repaint)
                    {
                        // Aim inside the track, beyond the label and before the numeric field.
                        var rect = Inspector.ZoomControlRect;
                        var trackStart = rect.xMin + UnityEditor.EditorGUIUtility.labelWidth;
                        var trackEnd = rect.xMax - UnityEditor.EditorGUIUtility.fieldWidth;
                        ZoomClickPosition = new UnityEngine.Vector2(
                            UnityEngine.Mathf.Lerp(trackStart, trackEnd, 0.75f), rect.center.y);
                    }
                }
                finally
                {
                    UnityEngine.GUI.enabled = wasEnabled;
                }
            }
        }

        [UnityEngine.TestTools.UnityTest]
        public System.Collections.IEnumerator PreviewZoomWorksInsideDisabledImportedAssetInspector()
        {
            var asset = UnityEngine.ScriptableObject.CreateInstance<RoSpriteAsset>();
            asset.sprFileName = "preview-test";
            asset.sprVersion = "2.1";
            asset.images = new[] { new UnityEngine.Texture2D(2, 2) };
            asset.palette = new UnityEngine.Texture2D(16, 16);
            var inspector = (RoSpriteAssetInspector)UnityEditor.Editor.CreateEditor(asset);
            var window = UnityEngine.ScriptableObject.CreateInstance<PreviewHarness>();
            window.Inspector = inspector;
            window.position = new UnityEngine.Rect(100, 100, 500, 650);
            try
            {
                window.ShowUtility();
                var deadline = UnityEditor.EditorApplication.timeSinceStartup + 5;
                while (inspector.ZoomControlRect.width <= 0 && UnityEditor.EditorApplication.timeSinceStartup < deadline)
                {
                    window.Repaint();
                    yield return null;
                }
                Assert.Greater(inspector.ZoomControlRect.width, 0, "The preview must draw its zoom slider before input.");

                var before = inspector.PreviewZoom;
                window.SendEvent(new UnityEngine.Event
                {
                    type = UnityEngine.EventType.MouseDown,
                    mousePosition = window.ZoomClickPosition,
                    button = 0,
                    clickCount = 1
                });
                window.SendEvent(new UnityEngine.Event
                {
                    type = UnityEngine.EventType.MouseUp,
                    mousePosition = window.ZoomClickPosition,
                    button = 0
                });
                Assert.Greater(inspector.PreviewZoom, before, "Preview zoom must accept input in a disabled imported-asset Inspector.");
                Assert.AreEqual(true, window.ParentStayedDisabledOnClick, "The viewer must restore its parent's GUI state on input.");
            }
            finally
            {
                window.Close();
                UnityEngine.Object.DestroyImmediate(inspector);
                UnityEngine.Object.DestroyImmediate(asset.images[0]);
                UnityEngine.Object.DestroyImmediate(asset.palette);
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void SprReimportUpdatesPairedDataAndPreservesReferences()
        {
            var folder = "Assets/Sprites/__SprSupportTest_" + System.Guid.NewGuid().ToString("N");
            var output = folder.Replace("Assets/Sprites/", "Assets/Sprites/Imported/");
            var sprPath = folder + "/sample.spr";
            var actPath = folder + "/sample.act";
            try
            {
                Directory.CreateDirectory(folder);
                WriteTestSprite(sprPath, 2);
                using (var writer = new BinaryWriter(File.Create(actPath)))
                {
                    writer.Write(new byte[] { 65, 67, 1, 1 });
                    writer.Write((ushort)1);
                    writer.Write(new byte[10]);
                    writer.Write(1); // one frame
                    writer.Write(new byte[32]);
                    writer.Write(1); // one layer
                    writer.Write(0); writer.Write(0); // position
                    writer.Write(0); writer.Write(0); // image, mirror
                }
                UnityEditor.AssetDatabase.Refresh(UnityEditor.ImportAssetOptions.ForceSynchronousImport);
                var data = UnityEditor.AssetDatabase.LoadAssetAtPath<Assets.Scripts.RoSpriteData>(output + "/sample.asset");
                Assert.IsNotNull(data);
                UnityEditor.AssetDatabase.TryGetGUIDAndLocalFileIdentifier(data, out string guid, out long id);
                UnityEditor.AssetDatabase.TryGetGUIDAndLocalFileIdentifier(data.Sprites[0], out string spriteGuid, out long spriteId);
                WriteTestSprite(sprPath, 3);
                UnityEditor.AssetDatabase.ImportAsset(sprPath, UnityEditor.ImportAssetOptions.ForceUpdate);
                data = UnityEditor.AssetDatabase.LoadAssetAtPath<Assets.Scripts.RoSpriteData>(output + "/sample.asset");
                Assert.AreEqual(3, data.SpriteSizes[0].x);
                UnityEditor.AssetDatabase.TryGetGUIDAndLocalFileIdentifier(data, out string nextGuid, out long nextId);
                UnityEditor.AssetDatabase.TryGetGUIDAndLocalFileIdentifier(data.Sprites[0], out string nextSpriteGuid, out long nextSpriteId);
                Assert.AreEqual(guid, nextGuid);
                Assert.AreEqual(id, nextId);
                Assert.AreEqual(spriteGuid, nextSpriteGuid);
                Assert.AreEqual(spriteId, nextSpriteId);

                // Edit the first layer's X position in the ACT 1.1 fixture.
                using (var writer = new BinaryWriter(File.OpenWrite(actPath)))
                {
                    const int FirstLayerOffset = 16 + 4 + 32 + 4; // Header, frame count, reserved frame bytes, layer count.
                    writer.BaseStream.Position = FirstLayerOffset;
                    writer.Write(7);
                }
                UnityEditor.AssetDatabase.ImportAsset(actPath, UnityEditor.ImportAssetOptions.ForceUpdate);
                data = UnityEditor.AssetDatabase.LoadAssetAtPath<Assets.Scripts.RoSpriteData>(output + "/sample.asset");
                Assert.AreEqual(7, data.Actions[0].Frames[0].Layers[0].Position.x);
                UnityEditor.AssetDatabase.TryGetGUIDAndLocalFileIdentifier(data, out string actGuid, out long actId);
                UnityEditor.AssetDatabase.TryGetGUIDAndLocalFileIdentifier(data.Sprites[0], out string actSpriteGuid, out long actSpriteId);
                Assert.AreEqual(guid, actGuid);
                Assert.AreEqual(id, actId);
                Assert.AreEqual(spriteGuid, actSpriteGuid);
                Assert.AreEqual(spriteId, actSpriteId);

                var atlasPath = output + "/Atlas/sample_atlas.png";
                var atlasBytes = File.ReadAllBytes(atlasPath);
                var sprBytes = File.ReadAllBytes(sprPath);
                var actBytes = File.ReadAllBytes(actPath);
                var palettePath = folder + "/Palette/sample_0.pal";
                Directory.CreateDirectory(Path.GetDirectoryName(palettePath));
                File.WriteAllBytes(palettePath, new byte[] { 1 });

                var error = Assert.Throws<InvalidDataException>(() => RoSpriteImporter.ImportPair(sprPath, actPath));
                StringAssert.Contains(palettePath, error.Message.Replace('\\', '/'));
                CollectionAssert.AreEqual(atlasBytes, File.ReadAllBytes(atlasPath));
                CollectionAssert.AreEqual(sprBytes, File.ReadAllBytes(sprPath));
                CollectionAssert.AreEqual(actBytes, File.ReadAllBytes(actPath));
                Assert.IsFalse(File.Exists(output + "/sample_0.asset"));
            }
            finally
            {
                UnityEditor.AssetDatabase.DeleteAsset(folder);
                UnityEditor.AssetDatabase.DeleteAsset(output);
            }
        }

        [Test]
        public void RoActParsesAnimationAndCreatesLegacyCompatibleRuntimeActions()
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
            {
                writer.Write(new byte[] { 65, 67, 5, 2 }); // ACT 2.5
                writer.Write((ushort)1);
                writer.Write(new byte[10]);
                writer.Write((uint)1); // frame count
                writer.Write(new byte[32]);
                writer.Write((uint)2); // layers
                WriteActLayer(writer, 3, 4, 1, 0, 1f, 1f, 0, 0, 0, 0);
                WriteActLayer(writer, 5, 6, 2, 1, 1.5f, 0.75f, 90, 16, 24, 1);
                writer.Write(0); // frame sound
                writer.Write((uint)1); // position count
                writer.Write(7); writer.Write(8); writer.Write(9); writer.Write(10);
                writer.Write(1); // sound count
                var soundName = new byte[40];
                System.Text.Encoding.ASCII.GetBytes("atk").CopyTo(soundName, 0);
                writer.Write(soundName);
                writer.Write(3.5f); // delay in legacy ACT units
            }

            stream.Position = 0;
            using var reader = new BinaryReader(stream);
            var parsed = RoAct.Read(reader);
            var parsedFrame = parsed.Actions[0].Frames[0];

            Assert.AreEqual(25, parsed.Version);
            Assert.AreEqual(1, parsed.Actions.Length);
            Assert.AreEqual(3.5f, parsed.ActionDelays[0]);
            Assert.AreEqual(2, parsedFrame.Layers.Length);
            Assert.AreEqual(2, parsedFrame.Layers[1].Index, "Parsed ACT indexes remain SPR-type-local.");
            Assert.AreEqual(new UnityEngine.Vector2(1.5f, 0.75f), parsedFrame.Layers[1].Scale);
            Assert.AreEqual((UnityEngine.Color)new UnityEngine.Color32(255, 255, 255, 255), parsedFrame.Layers[1].Color);
            Assert.AreEqual(16, parsedFrame.Layers[1].Width);
            Assert.AreEqual(24, parsedFrame.Layers[1].Height);
            Assert.AreEqual(1, parsedFrame.Pos.Length);
            Assert.AreEqual(7, parsedFrame.Pos[0].Unknown1);
            Assert.IsFalse(parsedFrame.IsAttackFrame);
            Assert.AreEqual("atk", parsed.Sounds[0]);

            var runtimeAction = RoSpriteImporter.CreateRuntimeActions(parsed, bitmapImageCount: 10, trueColorImageCount: 3)[0];
            Assert.AreEqual(84, runtimeAction.Delay);
            var runtimeFrame = runtimeAction.Frames[0];
            Assert.IsTrue(runtimeFrame.IsAttackFrame);
            Assert.AreEqual(12, runtimeFrame.Layers[1].Index, "True-color ACT indexes receive the legacy bitmap offset in runtime data.");
            Assert.AreEqual(2, parsedFrame.Layers[1].Index, "Runtime conversion must not mutate the parsed ACT model.");
        }

        private static void WriteActLayer(BinaryWriter writer, int x, int y, int index, int type,
            float scaleX, float scaleY, int angle, int width, int height, int mirror)
        {
            writer.Write(x); writer.Write(y); writer.Write(index); writer.Write(mirror);
            writer.Write(new byte[] { 255, 255, 255, 255 });
            writer.Write(scaleX); writer.Write(scaleY); writer.Write(angle); writer.Write(type);
            writer.Write(width); writer.Write(height);
        }

        private static void WriteTestSprite(string path, ushort width)
        {
            using var writer = new BinaryWriter(File.Create(path));
            writer.Write(new byte[] { 83, 80, 0, 2 });
            writer.Write((ushort)1); writer.Write((ushort)0);
            writer.Write(width); writer.Write((ushort)1);
            for (var i = 0; i < width; i++) writer.Write((byte)1);
            for (var i = 0; i < 256; i++)
                writer.Write(new byte[] { (byte)i, 0, 0, 255 });
        }

        [TestCase(4096, false)]
        [TestCase(4097, true)]
        public void ActActionCountLimitIsEnforced(int actionCount, bool rejected)
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
            {
                WriteActHeader(writer, 0, (ushort)actionCount);
                for (var i = 0; i < actionCount; i++) writer.Write(0); // Empty actions.
            }
            stream.Position = 0;
            using var reader = new BinaryReader(stream);
            if (rejected) Assert.Throws<InvalidDataException>(() => RoAct.Read(reader));
            else Assert.AreEqual(actionCount, RoAct.Read(reader).Actions.Length);
        }

        [TestCase("frame")]
        [TestCase("layer")]
        [TestCase("sound")]
        public void ActRejectsExcessiveRecordCounts(string record)
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
            {
                WriteActHeader(writer, 1, record == "sound" ? (ushort)0 : (ushort)1);
                if (record == "layer")
                {
                    writer.Write(1); // One frame before the excessive layer count.
                    writer.Write(new byte[32]);
                }
                writer.Write(65537);
            }
            stream.Position = 0;
            using var reader = new BinaryReader(stream);
            Assert.Throws<InvalidDataException>(() => RoAct.Read(reader));
        }

        [TestCase(-1, false)]
        [TestCase(0, false)]
        [TestCase(1, true)]
        public void ActValidatesFrameSoundReferences(int soundIndex, bool rejected)
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
            {
                WriteActHeader(writer, 1, 1);
                writer.Write(1); // One frame.
                writer.Write(new byte[32]);
                writer.Write(0); // No layers.
                writer.Write(soundIndex);
                writer.Write(1); // One sound.
                writer.Write(new byte[40]);
            }
            stream.Position = 0;
            using var reader = new BinaryReader(stream);
            if (rejected) Assert.Throws<InvalidDataException>(() => RoAct.Read(reader));
            else Assert.AreEqual(soundIndex, RoAct.Read(reader).Actions[0].Frames[0].Sound);
        }

        private static void WriteActHeader(BinaryWriter writer, byte minorVersion, ushort actionCount)
        {
            writer.Write(new byte[] { (byte)'A', (byte)'C', minorVersion, 2 });
            writer.Write(actionCount);
            writer.Write(new byte[10]);
        }

        [TestCase(1023, true)]
        [TestCase(1024, false)]
        public void PaletteOverrideRequiresCompleteColors(int byteCount, bool rejected)
        {
            var palette = new UnityEngine.Color32[256];
            palette[0] = new UnityEngine.Color32(10, 20, 30, 255);
            var bytes = new byte[byteCount];
            if (rejected)
            {
                Assert.Throws<InvalidDataException>(() => RoPalette.ApplyOverride(palette, bytes));
                return;
            }

            // Slot 255's RGB marks entries that retain their original SPR color.
            bytes[4] = 90;
            bytes[5] = 80;
            bytes[6] = 70;
            bytes[7] = 255;
            var result = RoPalette.ApplyOverride(palette, bytes);
            Assert.AreEqual(palette[0], result[0]);
            Assert.AreEqual(new UnityEngine.Color32(90, 80, 70, 255), result[1]);
            Assert.AreEqual(default(UnityEngine.Color32), palette[1], "Overrides must not mutate the SPR palette.");
        }

        [Test]
        public void LongAndTrailingZeroRunsRoundTrip()
        {
            var pixels = new byte[602];
            pixels[300] = 7;
            var image = new RoBitmapImage { ImageWidth = 301, ImageHeight = 2, PaletteIndexes = pixels };
            var decoded = RoSpr.DecompressBitmapImage(RoSpr.CompressBitmapImage(image));
            Assert.AreEqual(pixels, decoded.PaletteIndexes);
        }

        [Test]
        public void InvalidRunsAreRejected()
        {
            Assert.Throws<InvalidDataException>(() => RoSpr.DecompressBitmapImage(new RoCompressedBitmapImage
            {
                ImageWidth = 1,
                ImageHeight = 1,
                CompressedSize = 2,
                CompressedPaletteIndexes = new byte[] { 0, 2 }
            }));
            Assert.Throws<InvalidDataException>(() => RoSpr.DecompressBitmapImage(new RoCompressedBitmapImage
            {
                ImageWidth = 1,
                ImageHeight = 1,
                CompressedSize = 1,
                CompressedPaletteIndexes = new byte[] { 0 }
            }));
        }

        [Test]
        public void BothVersionsParseNonSquareImagesPaletteAndAlpha()
        {
            foreach (var minor in new byte[] { 0, 1 })
            {
                using var stream = new MemoryStream();
                using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
                {
                    writer.Write(new byte[] { 83, 80, minor, 2 });
                    writer.Write((ushort)1);
                    writer.Write((ushort)1);
                    writer.Write((ushort)2);
                    writer.Write((ushort)3);
                    var pixels = minor == 0 ? new byte[] { 1, 0, 2, 3, 0, 0 }
                        : new byte[] { 1, 0, 1, 2, 3, 0, 2 };
                    if (minor == 1) writer.Write((ushort)pixels.Length);
                    writer.Write(pixels);
                    writer.Write((ushort)1);
                    writer.Write((ushort)2);
                    writer.Write(new byte[] { 31, 21, 11, 1, 41, 22, 12, 2 });
                    for (var i = 0; i < 1024; i++) writer.Write((byte)(i % 251));
                }
                stream.Position = 0;
                using var reader = new BinaryReader(stream);
                var spr = new RoSpr(reader);
                Assert.AreEqual($"2.{minor}", spr.Version);
                Assert.AreEqual(2, spr.BitmapImages[0].ImageWidth);
                Assert.AreEqual(3, spr.BitmapImages[0].ImageHeight);
                CollectionAssert.AreEqual(new byte[] { 1, 0, 2, 3, 0, 0 }, spr.BitmapImages[0].PaletteIndexes);
                Assert.AreEqual(1, spr.TrueColorImages[0].ImageWidth);
                Assert.AreEqual(2, spr.TrueColorImages[0].ImageHeight);
                CollectionAssert.AreEqual(new[]
                {
                    new UnityEngine.Color32(1, 11, 21, 31),
                    new UnityEngine.Color32(2, 12, 22, 41)
                }, spr.TrueColorImages[0].ImageData);
                Assert.AreEqual(256, spr.PaletteColors.Length);
                for (var i = 0; i < 256; i++)
                    Assert.AreEqual(new UnityEngine.Color32((byte)(i * 4 % 251),
                        (byte)((i * 4 + 1) % 251), (byte)((i * 4 + 2) % 251),
                        (byte)((i * 4 + 3) % 251)), spr.PaletteColors[i]);
            }
        }

        [Test]
        public void RoSpriteV21DecompressSuccessfully()
        {
            var result = RoSpr.DecompressBitmapImage(RoCompressedImage);
            Assert.AreEqual(DecompressedImage.ImageHeight, result.ImageHeight);
            Assert.AreEqual(DecompressedImage.ImageWidth, result.ImageWidth);
            Assert.AreEqual(DecompressedImage.PaletteIndexes, result.PaletteIndexes);
        }

        [Test]
        public void RoSpriteV21CompressSuccessfully()
        {
            var result = RoSpr.CompressBitmapImage(DecompressedImage);
            Assert.AreEqual(RoCompressedImage.ImageHeight, result.ImageHeight);
            Assert.AreEqual(RoCompressedImage.ImageWidth, result.ImageWidth);
            Assert.AreEqual(RoCompressedImage.CompressedSize, result.CompressedSize);
            Assert.AreEqual(RoCompressedImage.CompressedPaletteIndexes, result.CompressedPaletteIndexes);
        }
    }
}
