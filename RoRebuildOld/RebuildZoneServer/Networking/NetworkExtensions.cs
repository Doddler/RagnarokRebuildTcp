using System;
using System.Collections.Generic;
using System.Text;
using Lidgren.Network;
using RebuildData.Shared.Data;

namespace RebuildZoneServer.Networking
{
	public static class NetworkExtensions
	{
		public static void Write(this NetBuffer buffer, Position position)
		{
			buffer.Write((short)position.X);
			buffer.Write((short)position.Y);
		}

        public static void Write(this OutboundMessage buffer, Position position)
        {
            buffer.Write((short)position.X);
            buffer.Write((short)position.Y);
        }
	}
}
