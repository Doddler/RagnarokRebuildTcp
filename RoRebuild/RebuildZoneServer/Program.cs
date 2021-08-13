using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Threading;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RebuildData.Server.Config;
using RebuildData.Server.Logging;
using RebuildData.Server.Pathfinding;
using RebuildZoneServer.Data.Management;
using RebuildZoneServer.Networking;
using RebuildZoneServer.Sim;
using RebuildZoneServer.Util;
using Serilog;

namespace RebuildZoneServer
{
	class Program
	{
		static void Main(string[] args)
		{
            CreateHostBuilder(args).Build().Run();
		}

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseSerilog(ServerLogger.GetLogger())
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<WebSetup>();
                });

    }
}
