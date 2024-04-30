using Remora.Discord.Hosting.Extensions;
using Remora.Discord.Caching.Extensions;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Interactivity.Extensions;
using Remora.Discord.Extensions.Extensions;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.Commands.Services;
using Remora.Rest.Core;
using DiscordMonies.Lobby;
using DiscordMonies.Game;
using DiscordMonies.Board;
using DiscordMonies.Remora;
using Remora.Commands.Extensions;

var builder = Host.CreateApplicationBuilder(args);

var dmConfig = builder.Configuration.GetSection("DiscordMonies").Get<DiscordMoniesConfiguration>() ?? throw new("Could not bind configuration.");

builder.Services
    .AddDiscordService(_ => dmConfig.DiscordToken)
    .AddDiscordCaching()
    .AddDiscordCommands(enableSlash: true)
    .AddInteractivity()
    .AddCommandGroupsFromAssembly(typeof(Program).Assembly)
    .AddInteractionGroup<LobbyInteractions>()
    .AddInteractionGroup<GameInteractions>()
    .AddPreExecutionEvent<GameCommandPreExecutionEvent>()
    .AddPostExecutionEvent<GameCommandPostExecutionEvent>()
    .AddPreparationErrorEvent<GameCommandPreparationErrorEvent>()
    .AddCondition<RequiresPlayerCondition>()
    .AddCondition<NextRequiredActionCondition>()
    .AddParser<GamePlayerParser>()
    .AddParser<SpaceParser>()
    .AddParser<ColorSetParser>()
    .AddAutocompleteProvider<SpaceAutocompleteProvider>()
    .AddAutocompleteProvider<ColorSetAutocompleteProvider>();

builder.Services
    .AddSingleton<LobbyService>()
    .AddSingleton<GameService>()
    .AddSingleton<BoardService>()
    .AddScoped<GameControllerService>()
    .AddScoped<GameStateInjector>()
    .AddScoped<BoardRenderer>();

builder.Services
    .AddTransient<GameState>(s => s.GetRequiredService<GameStateInjector>().GameState!);

var host = builder.Build();

var slashService = host.Services.GetRequiredService<SlashService>();

if (dmConfig.DebugServerId is ulong serverId)
{
    if (builder.Environment.IsDevelopment())
    {
        var guildId = new Snowflake(serverId);
        await slashService.UpdateSlashCommandsAsync(guildId);
    }
    else
    {
        var appInfo = await host.Services.GetRequiredService<IDiscordRestOAuth2API>().GetCurrentBotApplicationInformationAsync();

        var api = host.Services.GetRequiredService<IDiscordRestApplicationAPI>();
        await api.BulkOverwriteGuildApplicationCommandsAsync(appInfo.Entity.ID, new(serverId), []);
    }
}
else
{
    await slashService.UpdateSlashCommandsAsync();
}

host.Run();

internal sealed class DiscordMoniesConfiguration
{
    public required string DiscordToken { get; set; }
    public ulong? DebugServerId { get; set; }
}
