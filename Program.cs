using DCMusicBot.Commands.Handler;
using DCMusicBot.Services;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services;
using NetCord.Hosting.Services.ApplicationCommands;
using NetCord.Hosting.Services.Commands;
using NetCord.Services.Commands;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration));

builder.Services
    .AddScoped<BasicService>()
    .AddScoped<MusicService>()
    .AddScoped<VoiceChannelActionService>();

builder.Services
    .AddDiscordGateway(options =>
    {
        options.Intents = GatewayIntents.All;
    })
    .AddCommands(options =>
    {
        options.ResultHandler = new MyCommandResultHandler<CommandContext>();
    })
    .AddApplicationCommands();

var host = builder.Build();

// Add commands from modules
host.AddModules(typeof(Program).Assembly);

// Add handlers to handle the commands
host.UseGatewayHandlers();

await host.RunAsync();