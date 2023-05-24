using TaskRunner.App;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(
        (ctx, services) =>
        {
            services.Configure<SettingsProvider>(ctx.Configuration.GetSection("AppSettings"));
            services.AddHostedService<TaskRunnerService>();

            services.AddSingleton<ServiceProvider>(services.BuildServiceProvider());
        }
    )
    .UseWindowsService()
    .Build();

await host.RunAsync();
