using System;
using System.Collections.Generic;
using System.Text;

namespace RebuildData.Shared.Util
{
	public class EasyStack<T>
	{
		private T[] items;
		private int count = 0;
		private int max = 0;

		public EasyStack(int capacity)
		{
			items = new T[capacity];
			max = capacity;
			count = 0;
		}

		public void Add(T item)
		{
			items[count] = item;
			count++;
			if(count > max)
				throw new Exception("Attempting to add too many items to EasyStack of type " + typeof(T));
		}

		public T Take()
		{
			count--;
			if(count < 0)
				throw new Exception("EasyStack of type " + typeof(T) + " is out of items and cannot take.");
			return items[count];
		}
	}
}
