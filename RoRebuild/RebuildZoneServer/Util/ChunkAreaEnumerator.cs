using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using RebuildData.Shared.Data;
using RebuildZoneServer.Sim;

namespace RebuildZoneServer.Util
{
	public struct ChunkAreaEnumerator
	{
		private readonly Chunk[] chunks;

		private readonly int offsetX;
		private readonly int offsetY;
		private readonly int chunkWidth;
		private readonly int width;
		private readonly int height;

		private int idx;
		private int id;

		public ChunkAreaEnumerator(Chunk[] chunks, int chunkWidth, Area area)
		{
			this.chunks = chunks;
			this.chunkWidth = chunkWidth;

			offsetX = area.MinX;
			offsetY = area.MinY;
			width = area.Width;
			height = area.Height;
			idx = -1;
			id = 0;
		}

		public void Reset()
		{
			idx = -1;
		}

		public Chunk Current => chunks[id];

		public bool MoveNext()
		{
			idx++;
			var xPos = (idx % width) + offsetX;
			var yPos = (idx / width) + offsetY;
			id = xPos + yPos * chunkWidth;

			return idx < width * height;
		}

		public void Dispose()
		{
			
		}

		public ChunkAreaEnumerator GetEnumerator()
		{
			return this;
		}
	}
}
