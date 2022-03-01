using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RebuildData.Database.Context;

namespace RebuildData.Database
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    var str = hostContext.Configuration.GetConnectionString("DefaultConnection");

                    services.AddDbContextPool<RebuildContext>(builder => builder.UseSqlServer(hostContext.Configuration.GetConnectionString("DefaultConnection")));
                    services.AddHostedService<Worker>();
                });
    }
}
