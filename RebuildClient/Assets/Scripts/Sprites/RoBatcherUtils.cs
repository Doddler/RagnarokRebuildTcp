using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

public class CollectionDictionary<TKey, TCollection, TValue> : IEnumerable
	where TCollection : ICollection<TValue>, new()
{
	protected readonly Dictionary<TKey, TCollection> InternalDictionary = new();

	public TCollection this[TKey key] => InternalDictionary[key];

	public IEnumerable<TKey> Keys => InternalDictionary.Keys;

	public void AddItems(TKey key, params TValue[] items)
	{
		EnsureCollectionExistsForAdd(key);

		foreach (var item in items)
		{
			InternalDictionary[key].Add(item);
		}
	}

	public void AddItem(TKey key, TValue item)
	{
		EnsureCollectionExistsForAdd(key);
		InternalDictionary[key].Add(item);
	}

	private void EnsureCollectionExistsForAdd(TKey key)
	{
		if (!ContainsKey(key))
		{
			InternalDictionary[key] = new TCollection();
		}
	}

	public void RemoveItem(TKey key, TValue item)
	{
		if (!ContainsKey(key))
		{
			return;
		}

		InternalDictionary[key].Remove(item);

		if (!InternalDictionary[key].Any())
		{
			InternalDictionary.Remove(key);
		}
	}

	public void RemoveAllItems(TKey key)
	{
		InternalDictionary.Remove(key);
	}

	public int Count(TKey key)
	{
		return !ContainsKey(key) ? 0 : InternalDictionary[key].Count;
	}

	public int Count()
	{
		return InternalDictionary.Count;
	}

	public int TotalItemCount()
	{
		return InternalDictionary.Values.Sum(c => c.Count);
	}

	public bool ContainsKey(TKey key)
	{
		return InternalDictionary.ContainsKey(key);
	}

	public void Clear()
	{
		InternalDictionary.Clear();
	}

	public TCollection Get(TKey key)
	{
		return InternalDictionary[key];
	}

	public bool TryGet(TKey key, out TCollection collection)
	{
		return InternalDictionary.TryGetValue(key, out collection!);
	}

	public IEnumerator<KeyValuePair<TKey, TCollection>> GetEnumerator()
	{
		return InternalDictionary.GetEnumerator();
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}
}

internal sealed class GrowableBuffer<T> : IDisposable where T : struct
{
	public GraphicsBuffer Buffer { get; private set; }
	public readonly int Stride = Marshal.SizeOf<T>();
	public int Capacity { get; private set; }
	public int Cursor { get; private set; }

	readonly GraphicsBuffer.Target _target;

	public GrowableBuffer(int initialCapacity, GraphicsBuffer.Target target = GraphicsBuffer.Target.Structured)
	{
		_target = target;
		Capacity = Math.Max(1, initialCapacity);
		Buffer = new GraphicsBuffer(_target, Capacity, Stride);
	}

	public void BeginFrame() => Cursor = 0;

	public int Append(Array data, int srcStart, int count)
	{
		EnsureCapacity(Cursor + count);
		Buffer.SetData(data, srcStart, Cursor, count);
		int baseIndex = Cursor;
		Cursor += count;
		return baseIndex;
	}

	private void EnsureCapacity(int required)
	{
		if (required <= Capacity) return;
		int newCap = Capacity;
		while (newCap < required) newCap *= 2;

		var newBuf = new GraphicsBuffer(_target, newCap, Stride);
		Buffer.Release();
		Buffer = newBuf;
		Capacity = newCap;
	}

	public void Dispose()
	{
		Buffer?.Release();
		Buffer = null;
	}
}

internal sealed class InstanceBufferPool<T> : IDisposable where T : struct
{
	private const int FramesInFlight = 3;
	private int _frameSlot;

	private readonly GrowableBuffer<T>[] _instances;

	public GraphicsBuffer Instances => _instances[_frameSlot].Buffer;

	public InstanceBufferPool(int initialInstances, GraphicsBuffer.Target target = GraphicsBuffer.Target.Structured)
	{
		_instances = new[]
		{
			new GrowableBuffer<T>(initialInstances, target),
			new GrowableBuffer<T>(initialInstances, target),
			new GrowableBuffer<T>(initialInstances, target),
		};
	}

	public void BeginFrame()
	{
		_frameSlot = Time.frameCount % FramesInFlight;
		_instances[_frameSlot].BeginFrame();
	}

	public int AppendInstances(T[] data, int start, int count) => _instances[_frameSlot].Append(data, start, count);

	public void Dispose()
	{
		foreach (var b in _instances) b.Dispose();
	}
}