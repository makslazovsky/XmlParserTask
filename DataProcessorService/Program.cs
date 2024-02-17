using DataProcessorService;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureServices((context, services) =>
{
    services.AddDbContext<AppDbContext>(options =>
        options.UseSqlite(context.Configuration.GetConnectionString("AppDbContext")));

    services.AddHostedService<Worker>();
});

var host = builder.Build();

await host.RunAsync();