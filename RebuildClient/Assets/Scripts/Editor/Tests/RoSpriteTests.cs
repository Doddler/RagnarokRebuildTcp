using Assets.Scripts.Sprites;
using NUnit.Framework;

namespace Assets.Scripts.Editor.Tests
{
    class RoSpriteTests
    {
        [Test]
        public void RoSpriteV21DecompressSuccessfully()
        {
            var compressedImage = new CompressedBitmapImage
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

            var uncompressedImageBytesReference = new byte[]
            {
                0x00, 0x00, 0x00, 0x00, 0x00,
                0x02, 0x03, 0x00, 0x00, 0x00,
                0x00, 0x03, 0x09, 0x08, 0x00,
                0x00, 0x04, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x07,
            };

            var uncompressedImageBytes = RoSpr<RoSprV21>.DecompressBitmapImage(compressedImage);
            
            Assert.AreEqual(uncompressedImageBytesReference, uncompressedImageBytes);
        }
        
        [Test]
        public void RoSpriteV21CompressSuccessfully()
        {
            var decompressedImage = new BitmapImage()
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

            var compressedImageBytesReference = new byte[]
            {
                0x00, 0x05, 0x02, 0x03, 0x00,
                0x04, 0x03, 0x09, 0x08, 0x00,
                0x02, 0x04, 0x00, 0x07, 0x07,
            };

            var compressedImageBytes = RoSpr<RoSprV21>.CompressBitmapImage(decompressedImage);
            
            Assert.AreEqual(compressedImageBytesReference, compressedImageBytes);
        }
    }
}