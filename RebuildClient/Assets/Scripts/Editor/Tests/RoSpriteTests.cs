using Assets.Scripts.Sprites;
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