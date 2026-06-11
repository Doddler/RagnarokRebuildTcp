using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Assets.Scripts.Sprites
{
    public struct SpriteAtlasRegion
    {
        public int Slice;
        public Vector2 UvScale;
        public Vector2 UvOffset;
        public Vector4 UvClampRect;
    }

    public sealed class SpriteAtlasArray : IDisposable
    {
        public const int SliceSize = 2048;
        public const int DefaultSliceCount = 16;
        public const int MaxSliceCount = 64;

        private Texture2DArray _array;
        private Texture2DArray _clearTemplate;
        private TextureFormat _format;
        private bool _formatLocked;
        private readonly int _initialSliceCount;

        private sealed class Shelf
        {
            public int Y;
            public int Height;
            public int X;
        }

        private sealed class Page
        {
            public int Slice;
            public readonly List<Shelf> Shelves = new();
            public int NextShelfY;
        }

        private sealed class Entry
        {
            public SpriteAtlasRegion Region;
            public int RefCount;
            public int X, Y, Width, Height;
        }

        private struct FreeRect
        {
            public int Slice, X, Y, Width, Height;
        }

        private readonly List<Page> _pages = new();
        private readonly Dictionary<Texture2D, Entry> _entries = new();
        private readonly List<FreeRect> _freeRects = new();

        public Texture2DArray Texture => _array;
        public int EntryCount => _entries.Count;
        public int RejectedCount { get; private set; }
        public int SliceCount { get; private set; }

        public SpriteAtlasArray(int initialSliceCount = DefaultSliceCount)
        {
            _initialSliceCount = Mathf.Clamp(initialSliceCount, 1, MaxSliceCount);
            SliceCount = _initialSliceCount;
            RebuildPages();
        }

        private void RebuildPages()
        {
            _pages.Clear();
            for (var slice = 0; slice < SliceCount; slice++)
                _pages.Add(new Page { Slice = slice });
        }

        public bool TryAdd(Texture2D atlas, out SpriteAtlasRegion region)
        {
            region = default;
            if (atlas == null) return false;

            if (_entries.TryGetValue(atlas, out var existing))
            {
                existing.RefCount++;
                region = existing.Region;
                return true;
            }

            if (!EnsureArray(atlas.format))
            {
                RejectedCount++;
                return false;
            }

            int w = atlas.width, h = atlas.height;
            if (w > SliceSize || h > SliceSize || w < 4 || h < 4)
            {
                RejectedCount++;
                return false;
            }

            if (!TryAllocate(w, h, out var slice, out var x, out var y)
                && !TryGrowAndAllocate(w, h, out slice, out x, out y))
            {
                RejectedCount++;
                return false;
            }

            Graphics.CopyTexture(atlas, 0, 0, 0, 0, w, h, _array, slice, 0, x, y);

            const float pageSize = SliceSize;
            const float inset = 0.5f / pageSize; //shader clamps to this rect, replacing the source texture's Clamp wrap
            var entry = new Entry
            {
                RefCount = 1,
                X = x, Y = y, Width = w, Height = h,
                Region = new SpriteAtlasRegion
                {
                    Slice = slice,
                    UvScale = new Vector2(w / pageSize, h / pageSize),
                    UvOffset = new Vector2(x / pageSize, y / pageSize),
                    UvClampRect = new Vector4(
                        x / pageSize + inset, y / pageSize + inset,
                        (x + w) / pageSize - inset, (y + h) / pageSize - inset),
                },
            };
            _entries[atlas] = entry;
            region = entry.Region;
            return true;
        }

        public void Release(Texture2D atlas)
        {
            if (atlas == null) return;
            if (!_entries.TryGetValue(atlas, out var entry)) return;
            entry.RefCount--;
            if (entry.RefCount > 0) return;

            _entries.Remove(atlas);
            if (_entries.Count == 0)
            {
                ResetLayout();
                return;
            }
            if (_array != null && _clearTemplate != null)
                Graphics.CopyTexture(_clearTemplate, 0, 0, entry.X, entry.Y, entry.Width, entry.Height,
                    _array, entry.Region.Slice, 0, entry.X, entry.Y);
            _freeRects.Add(new FreeRect
            {
                Slice = entry.Region.Slice,
                X = entry.X, Y = entry.Y,
                Width = entry.Width, Height = entry.Height,
            });
        }

        private void ResetLayout()
        {
            _freeRects.Clear();
            if (SliceCount > _initialSliceCount && _array != null)
            {
                UnityEngine.Object.Destroy(_array);
                _array = null;
                SliceCount = _initialSliceCount;
            }
            else if (_array != null && _clearTemplate != null)
            {
                for (var slice = 0; slice < SliceCount; slice++)
                    Graphics.CopyTexture(_clearTemplate, 0, 0, _array, slice, 0);
            }
            RebuildPages();
        }

        private bool EnsureArray(TextureFormat format)
        {
            if (_formatLocked && format != _format)
                return false;
            if (_array != null)
                return true;

            if (!SystemInfo.SupportsTextureFormat(format))
                return false;

            try
            {
                if (_clearTemplate == null)
                {
                    _clearTemplate = CreateArray(format, 1, "RoSpriteAtlasClearTemplate");
                    ClearTemplateToTransparentBlack(_clearTemplate);
                }

                _array = CreateArray(format, SliceCount, "RoSpriteAtlasArray");
                for (var slice = 0; slice < SliceCount; slice++)
                    Graphics.CopyTexture(_clearTemplate, 0, 0, _array, slice, 0);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"SpriteAtlasArray: failed to create Texture2DArray ({format}): {e.Message}");
                if (_array != null) { UnityEngine.Object.Destroy(_array); _array = null; }
                if (_clearTemplate != null) { UnityEngine.Object.Destroy(_clearTemplate); _clearTemplate = null; }
                return false;
            }

            _format = format;
            _formatLocked = true;
            return true;
        }

        private static Texture2DArray CreateArray(TextureFormat format, int slices, string name)
        {
            return new Texture2DArray(SliceSize, SliceSize, slices, format, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
        }

        private static void ClearTemplateToTransparentBlack(Texture2DArray template)
        {
            //zero bytes decode to transparent black in BC formats and RGBA alike
            int size = (int)GraphicsFormatUtility.ComputeMipmapSize(SliceSize, SliceSize, template.graphicsFormat);
            var zeros = new NativeArray<byte>(size, Allocator.Temp, NativeArrayOptions.ClearMemory);
            template.SetPixelData(zeros, 0, 0);
            template.Apply(false, true);
            zeros.Dispose();
        }

        private bool TryGrowAndAllocate(int w, int h, out int slice, out int x, out int y)
        {
            slice = x = y = 0;
            if (_array == null || SliceCount >= MaxSliceCount)
                return false;

            Grow(SliceCount + 1);
            return TryAllocate(w, h, out slice, out x, out y);
        }

        private void Grow(int newCount)
        {
            int oldCount = SliceCount;
            var newArray = CreateArray(_format, newCount, "RoSpriteAtlasArray");
            for (var slice = 0; slice < oldCount; slice++)
                Graphics.CopyTexture(_array, slice, 0, newArray, slice, 0);

            UnityEngine.Object.Destroy(_array);
            _array = newArray;
            SliceCount = newCount;

            for (var slice = oldCount; slice < newCount; slice++)
            {
                Graphics.CopyTexture(_clearTemplate, 0, 0, _array, slice, 0);
                _pages.Add(new Page { Slice = slice });
            }
        }

        private bool TryAllocate(int w, int h, out int slice, out int x, out int y)
        {
            int alignedW = Align4(w);
            int potH = NextPot(Align4(h));

            for (var i = 0; i < _freeRects.Count; i++)
            {
                var fr = _freeRects[i];
                if (fr.Width != w || fr.Height != h) continue;
                slice = fr.Slice; x = fr.X; y = fr.Y;
                _freeRects.RemoveAt(i);
                return true;
            }

            for (var p = 0; p < _pages.Count; p++)
            {
                var page = _pages[p];

                for (var s = 0; s < page.Shelves.Count; s++)
                {
                    var shelf = page.Shelves[s];
                    if (shelf.Height != potH) continue;
                    if (shelf.X + w > SliceSize) continue;
                    return PlaceInShelf(page, shelf, w, out slice, out x, out y);
                }

                if (page.NextShelfY + potH <= SliceSize)
                {
                    var shelf = new Shelf { Y = page.NextShelfY, Height = potH, X = 0 };
                    page.Shelves.Add(shelf);
                    page.NextShelfY = Align4(page.NextShelfY + potH);
                    return PlaceInShelf(page, shelf, w, out slice, out x, out y);
                }
            }

            //scavenge leftover width in taller shelves before failing
            for (var p = 0; p < _pages.Count; p++)
            {
                var page = _pages[p];
                for (var s = 0; s < page.Shelves.Count; s++)
                {
                    var shelf = page.Shelves[s];
                    if (shelf.Height < potH) continue;
                    if (shelf.X + w > SliceSize) continue;
                    return PlaceInShelf(page, shelf, w, out slice, out x, out y);
                }
            }

            slice = x = y = 0;
            return false;
        }

        private static bool PlaceInShelf(Page page, Shelf shelf, int w, out int slice, out int x, out int y)
        {
            slice = page.Slice; x = shelf.X; y = shelf.Y;
            shelf.X = Align4(shelf.X + w);
            return true;
        }

        private static int Align4(int v) => (v + 3) & ~3;

        private static int NextPot(int v)
        {
            var p = 4;
            while (p < v) p <<= 1;
            return p;
        }

        public string DumpOccupancy()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"SpriteAtlasArray: {_entries.Count} atlases in {SliceCount}/{MaxSliceCount} slices, {RejectedCount} rejected, {_freeRects.Count} free rects, format={_format}");
            foreach (var page in _pages)
            {
                if (page.Shelves.Count == 0) continue;
                sb.AppendLine($"  slice {page.Slice}: {page.Shelves.Count} shelves, used {page.NextShelfY}/{SliceSize} rows");
            }
            return sb.ToString();
        }

        public void Dispose()
        {
            if (_array != null)
            {
                UnityEngine.Object.Destroy(_array);
                _array = null;
            }
            if (_clearTemplate != null)
            {
                UnityEngine.Object.Destroy(_clearTemplate);
                _clearTemplate = null;
            }
            _entries.Clear();
            _freeRects.Clear();
            _pages.Clear();
        }
    }
}
