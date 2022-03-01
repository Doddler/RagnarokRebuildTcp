using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RebuildData.Server.Logging
{
	public enum ProfilerEvent
	{
		CharacterUpdate,
		MonsterUpdate,
		PlayerUpdate,
		CombatEntityUpdate,
		MonsterStateMachineUpdate,
		MonsterStateMachineChangeSuccess,
		CharacterMoveUpdate,
		MapMoveEntity,
		MapUpdatePlayerAfterMove,
		MapGatherPlayers,
		MapGatherEntities,
		PathfinderCall,
		PathFoundDirect,
		PathFoundIndirect,
		PathNotFound,
	}

	public static class Profiler
	{
		private static int[] eventCount;
		private static int eventTypeCount = 0;
		private static bool writeLogToFile = false;
		private static float writeLogFrameTime = 0f;

		private static FileStream logStream;
		private static StreamWriter logWriter;

		private static int logLen = 0;
		
		[System.Diagnostics.Conditional("DEBUG")]
		public static void Event(ProfilerEvent e)
		{
			eventCount[(int)e]++;
		}

		[System.Diagnostics.Conditional("DEBUG")]
		public static void Init(float logIfOver)
		{

			writeLogToFile = logIfOver > 0;
			writeLogFrameTime = logIfOver;
			
			eventTypeCount = Enum.GetValues(typeof(ProfilerEvent)).Length;
			eventCount = new int[eventTypeCount];

			if (!writeLogToFile)
				return;

			if (!Directory.Exists("log"))
				Directory.CreateDirectory("log");

			logStream = new FileStream("log/profiler.csv", FileMode.Create);
			logWriter = new StreamWriter(logStream);

			logWriter.Write("Timestamp,FrameTime");

			for (var i = 0; i < eventTypeCount; i++)
			{
				logWriter.Write(",");
				logWriter.Write((ProfilerEvent)i);
			}
			logWriter.Write(Environment.NewLine);
		}

		[System.Diagnostics.Conditional("DEBUG")]
		public static void FinishFrame(float elapsedTime)
		{
			if (!writeLogToFile || elapsedTime < writeLogFrameTime)
				return;

			if (logLen == 10000)
			{
				ServerLogger.LogWarning("Wrote 10000 items to profiler log, stopping.");
				logLen++;
			}

			if (logLen > 10000)
				return;

			var ms = elapsedTime * 1000f;

			logWriter.Write($"[{DateTime.Now.ToLongTimeString()},{ms}");

			for (var i = 0; i < eventTypeCount; i++)
			{
				var e = (ProfilerEvent) i;
				logWriter.Write(",");
				logWriter.Write(eventCount[i]);
				eventCount[i] = 0;
			}

			logWriter.Write(Environment.NewLine);
			logLen++;
		}

		public static void Close()
		{
			logWriter.Dispose();
			logStream.Dispose();
		}
	}
}
