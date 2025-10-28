using Assets.Scripts.Sprites;
using NUnit.Framework;

namespace Assets.Scripts.Editor.Tests
{
    class RoSpriteTests
    {
        private static readonly CompressedBitmapImage CompressedImage = new CompressedBitmapImage
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

        private static readonly BitmapImage DecompressedImage = new BitmapImage
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
            var result = RoSpr<RoSprV21>.DecompressBitmapImage(CompressedImage);
            Assert.AreEqual(DecompressedImage.ImageHeight, result.ImageHeight);
            Assert.AreEqual(DecompressedImage.ImageWidth, result.ImageWidth);
            Assert.AreEqual(DecompressedImage.PaletteIndexes, result.PaletteIndexes);
        }
        
        [Test]
        public void RoSpriteV21CompressSuccessfully()
        {
            var result = RoSpr<RoSprV21>.CompressBitmapImage(DecompressedImage);
            Assert.AreEqual(CompressedImage.ImageHeight, result.ImageHeight);
            Assert.AreEqual(CompressedImage.ImageWidth, result.ImageWidth);
            Assert.AreEqual(CompressedImage.CompressedSize, result.CompressedSize);
            Assert.AreEqual(CompressedImage.CompressedPaletteIndexes, result.CompressedPaletteIndexes);
        }
    }
}