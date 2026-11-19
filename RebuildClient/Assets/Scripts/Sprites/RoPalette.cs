using System;
using System.IO;
using UnityEngine;

namespace Assets.Scripts.Sprites
{
    /// <summary>Applies Ragnarok's indexed SPR palette override rules to decoded colors.</summary>
    public static class RoPalette
    {
        private const int BytesPerColor = 4;
        private const int PaletteColorCount = 256;
        private const int TransparentColorIndex = PaletteColorCount - 1;
        public static Color32[] ApplyOverride(Color32[] sprPalette, byte[] overrideBytes)
        {
            if (sprPalette == null)
                throw new ArgumentNullException(nameof(sprPalette));

            if (overrideBytes == null)
                throw new ArgumentNullException(nameof(overrideBytes));

            if (sprPalette.Length < PaletteColorCount || overrideBytes.Length < sprPalette.Length * BytesPerColor)
                throw new InvalidDataException("Palette override must contain at least 256 RGBA colors.");

            var result = new Color32[sprPalette.Length];
            var transparentOffset = TransparentColorIndex * BytesPerColor;
            var transparentR = overrideBytes[transparentOffset];
            var transparentG = overrideBytes[transparentOffset + 1];
            var transparentB = overrideBytes[transparentOffset + 2];

            for (var i = 0; i < result.Length; i++)
            {
                var offset = i * BytesPerColor;
                var matchesTransparentColor = overrideBytes[offset] == transparentR &&
                                              overrideBytes[offset + 1] == transparentG &&
                                              overrideBytes[offset + 2] == transparentB;
                result[i] = matchesTransparentColor
                    ? sprPalette[i]
                    : new Color32(overrideBytes[offset], overrideBytes[offset + 1],
                        overrideBytes[offset + 2], overrideBytes[offset + 3]);
            }

            return result;
        }
    }
}
