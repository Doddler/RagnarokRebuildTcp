using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI.Utility
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ScrollRect))]
    public class VirtualList : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform viewport;
        [SerializeField] private RectTransform content;
        [SerializeField] private GameObject itemPrefab;

        [Header("Layout")]
        [SerializeField, Min(0.01f)] private float itemHeight = 20f;
        [SerializeField, Min(0f)] private float spacing;
        [SerializeField, Min(0f)] private float paddingTop;
        [SerializeField, Min(0f)] private float paddingBottom;
        [SerializeField, Min(0f)] private float paddingLeft;
        [SerializeField, Min(0f)] private float paddingRight;
        [SerializeField, Min(0)] private int extraVisibleItems = 1;

        private readonly List<PooledItem> pooledItems = new List<PooledItem>();
        private readonly List<int> missingDataIndices = new List<int>();
        private readonly List<PooledItem> recyclableItems = new List<PooledItem>();

        private Action<GameObject, int> bindItem;
        private int itemCount;
        private int firstVisibleIndex = -1;
        private int activePoolSize;
        private float previousViewportHeight = -1f;
        private bool initialized;
        private bool scrollListenerAttached;

        public int ItemCount => itemCount;
        public int ActiveItemCount { get; private set; }

        private float ItemStride => itemHeight + spacing;

        private sealed class PooledItem
        {
            public GameObject GameObject;
            public RectTransform RectTransform;
            public int DataIndex = -1;
        }

        private void OnEnable()
        {
            if (itemPrefab == null)
                return;

            Initialize();
            AttachScrollListener();
            Rebuild();
        }

        private void OnDisable()
        {
            if (scrollListenerAttached)
            {
                scrollRect.onValueChanged.RemoveListener(OnScrollPositionChanged);
                scrollListenerAttached = false;
            }
        }

        private void LateUpdate()
        {
            if (viewport != null && !Mathf.Approximately(previousViewportHeight, viewport.rect.height))
            {
                previousViewportHeight = viewport.rect.height;
                Rebuild();
            }
        }

        public void SetItemCount(int count, Action<GameObject, int> itemBinder, bool resetScrollPosition = true)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), "Item count cannot be negative.");

            Initialize();

            if (!initialized)
                return;

            itemCount = count;
            bindItem = itemBinder;

            if (resetScrollPosition)
                SetScrollOffset(0f);

            Rebuild();
        }

        public void SetItems<T>(IReadOnlyList<T> items, Action<GameObject, T, int> itemBinder,
            bool resetScrollPosition = true)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));
            if (itemBinder == null)
                throw new ArgumentNullException(nameof(itemBinder));

            SetItemCount(items.Count, (item, index) => itemBinder(item, items[index], index), resetScrollPosition);
        }

        public void SetItems<TData, TRow>(
            IReadOnlyList<TData> items,
            Action<TRow, TData, int> itemBinder,
            bool resetScrollPosition = true)
            where TRow : Component
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));
            if (itemBinder == null)
                throw new ArgumentNullException(nameof(itemBinder));

            var componentCache = new Dictionary<GameObject, TRow>();
            SetItemCount(
                items.Count,
                (go, index) =>
                {
                    if (!componentCache.TryGetValue(go, out var row))
                        componentCache[go] = row = go.GetComponent<TRow>();
                    itemBinder(row, items[index], index);
                },
                resetScrollPosition);
        }

        public void SetItemPrefab(GameObject prefab)
        {
            if (prefab == null)
                throw new ArgumentNullException(nameof(prefab));
            if (initialized && itemPrefab != prefab)
                throw new InvalidOperationException($"{nameof(VirtualList)} on {name} is already initialized.");

            itemPrefab = prefab;
            Initialize();

            if (isActiveAndEnabled)
            {
                AttachScrollListener();
                Rebuild();
            }
        }

        public void Clear()
        {
            SetItemCount(0, null);
        }

        public void RefreshVisibleItems()
        {
            RefreshVisibleItems(true);
        }

        public void ScrollToIndex(int index, float alignment = 0f)
        {
            Initialize();

            if (!initialized || itemCount == 0)
                return;

            index = Mathf.Clamp(index, 0, itemCount - 1);
            alignment = Mathf.Clamp01(alignment);

            var itemTop = paddingTop + index * ItemStride;
            var availableSpace = Mathf.Max(0f, viewport.rect.height - itemHeight);
            SetScrollOffset(itemTop - availableSpace * alignment);
            RefreshVisibleItems(false);
        }

        private void Initialize()
        {
            if (initialized)
                return;

            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.viewport = viewport;
            scrollRect.content = content;

            var contentAnchorMin = content.anchorMin;
            var contentAnchorMax = content.anchorMax;
            contentAnchorMin.y = 1f;
            contentAnchorMax.y = 1f;
            content.anchorMin = contentAnchorMin;
            content.anchorMax = contentAnchorMax;
            content.pivot = new Vector2(content.pivot.x, 1f);

            previousViewportHeight = viewport.rect.height;
            initialized = true;
        }

        private void AttachScrollListener()
        {
            if (scrollListenerAttached)
                return;

            scrollRect.onValueChanged.AddListener(OnScrollPositionChanged);
            scrollListenerAttached = true;
        }

        private void Rebuild()
        {
            if (!initialized)
                return;

            UpdateContentHeight();
            ClampScrollOffset();
            EnsurePoolSize();
            firstVisibleIndex = -1;
            RefreshVisibleItems(true);
        }

        private void UpdateContentHeight()
        {
            var itemsHeight = itemCount > 0 ? itemCount * itemHeight + (itemCount - 1) * spacing : 0f;
            var requiredHeight = paddingTop + itemsHeight + paddingBottom;
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                Mathf.Max(viewport.rect.height, requiredHeight));
        }

        private void EnsurePoolSize()
        {
            var requiredCount = itemCount == 0
                ? 0
                : Mathf.Min(itemCount, Mathf.CeilToInt(viewport.rect.height / ItemStride) + extraVisibleItems * 2 + 1);

            activePoolSize = requiredCount;

            while (pooledItems.Count < requiredCount)
            {
                var item = Instantiate(itemPrefab, content);
                var itemTransform = item.GetComponent<RectTransform>();

                item.SetActive(false);
                itemTransform.anchorMin = new Vector2(0f, 1f);
                itemTransform.anchorMax = new Vector2(1f, 1f);
                itemTransform.pivot = new Vector2(0.5f, 1f);
                itemTransform.sizeDelta = new Vector2(-(paddingLeft + paddingRight), itemHeight);
                itemTransform.anchoredPosition = new Vector2((paddingLeft - paddingRight) * 0.5f, 0f);

                pooledItems.Add(new PooledItem
                {
                    GameObject = item,
                    RectTransform = itemTransform
                });
            }

            while (pooledItems.Count > requiredCount)
            {
                var last = pooledItems[pooledItems.Count - 1];
                pooledItems.RemoveAt(pooledItems.Count - 1);
                Destroy(last.GameObject);
            }
        }

        private void OnScrollPositionChanged(Vector2 _)
        {
            RefreshVisibleItems(false);
        }

        private void RefreshVisibleItems(bool forceRebind)
        {
            if (!initialized)
                return;

            var visibleStart = Mathf.FloorToInt((GetScrollOffset() - paddingTop) / ItemStride);
            visibleStart = Mathf.Clamp(visibleStart - extraVisibleItems, 0, Mathf.Max(0, itemCount - activePoolSize));

            if (!forceRebind && visibleStart == firstVisibleIndex)
                return;

            firstVisibleIndex = visibleStart;
            var visibleEnd = Mathf.Min(itemCount, visibleStart + activePoolSize);

            missingDataIndices.Clear();
            recyclableItems.Clear();

            for (var dataIndex = visibleStart; dataIndex < visibleEnd; dataIndex++)
                missingDataIndices.Add(dataIndex);

            if (forceRebind)
            {
                for (var poolIndex = 0; poolIndex < pooledItems.Count; poolIndex++)
                    pooledItems[poolIndex].DataIndex = -1;
            }

            for (var poolIndex = 0; poolIndex < pooledItems.Count; poolIndex++)
            {
                var pooledItem = pooledItems[poolIndex];
                if (pooledItem.DataIndex >= visibleStart &&
                    pooledItem.DataIndex < visibleEnd &&
                    missingDataIndices.Remove(pooledItem.DataIndex))
                {
                    SetItemActive(pooledItem, true);
                    continue;
                }

                recyclableItems.Add(pooledItem);
            }

            for (var missingIndex = 0; missingIndex < missingDataIndices.Count; missingIndex++)
            {
                var pooledItem = recyclableItems[missingIndex];
                var dataIndex = missingDataIndices[missingIndex];

                pooledItem.DataIndex = dataIndex;
                PositionItem(pooledItem.RectTransform, dataIndex);
                bindItem?.Invoke(pooledItem.GameObject, dataIndex);
                SetItemActive(pooledItem, true);
            }

            for (var recyclableIndex = missingDataIndices.Count;
                 recyclableIndex < recyclableItems.Count;
                 recyclableIndex++)
            {
                var pooledItem = recyclableItems[recyclableIndex];
                pooledItem.DataIndex = -1;
                SetItemActive(pooledItem, false);
            }

            ActiveItemCount = visibleEnd - visibleStart;
        }

        private static void SetItemActive(PooledItem pooledItem, bool active)
        {
            if (pooledItem.GameObject.activeSelf != active)
                pooledItem.GameObject.SetActive(active);
        }

        private void PositionItem(RectTransform itemTransform, int dataIndex)
        {
            itemTransform.anchoredPosition = new Vector2(
                (paddingLeft - paddingRight) * 0.5f,
                -(paddingTop + dataIndex * ItemStride));
        }

        private float GetScrollOffset()
        {
            return Mathf.Max(0f, content.anchoredPosition.y);
        }

        private void SetScrollOffset(float offset)
        {
            var position = content.anchoredPosition;
            position.y = Mathf.Clamp(offset, 0f, GetMaximumScrollOffset());
            content.anchoredPosition = position;
        }

        private void ClampScrollOffset()
        {
            SetScrollOffset(GetScrollOffset());
        }

        private float GetMaximumScrollOffset()
        {
            return Mathf.Max(0f, content.rect.height - viewport.rect.height);
        }
    }
}
