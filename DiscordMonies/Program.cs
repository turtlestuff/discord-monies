using DiscordMonies;
using DiscordMonies.Board;
using DiscordMonies.Game;
using DiscordMonies.Game.UnresolvedMatters;
using DiscordMonies.Game.UnresolvedMatters.DiceRoll;
using DiscordMonies.Game.UnresolvedMatters.Payment;
using DiscordMonies.Game.UnresolvedMatters.PayOrGamble;
using DiscordMonies.Game.UnresolvedMatters.PurchaseDecision;
using DiscordMonies.Game.UnresolvedMatters.RequiredBailPayment;
using DiscordMonies.Game.UnresolvedMatters.RequiredBailPaymentComplete;
using DiscordMonies.Lobby;
using DiscordMonies.Remora;
using Remora.Commands.Extensions;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.Caching.Extensions;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Commands.Services;
using Remora.Discord.Extensions.Extensions;
using Remora.Discord.Hosting.Extensions;
using Remora.Discord.Interactivity.Extensions;
using Remora.Rest.Core;

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
    .AddAutocompleteProvider<OwnedSpaceAutocompleteProvider>()
    .AddAutocompleteProvider<ColorSetAutocompleteProvider>()
    .AddUnresolvedMatterHandler<DiceRollMatterHandler>()
    .AddUnresolvedMatterHandler<PaymentMatterHandler>()
    .AddUnresolvedMatterHandler<PurchaseDecisionMatterHandler>()
    .AddUnresolvedMatterHandler<RequiredBailPaymentMatterHandler>()
    .AddUnresolvedMatterHandler<RequiredBailPaymentCompleteMatterHandler>()
    .AddUnresolvedMatterHandler<PayOrGambleMatterHandler>();

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

namespace DiscordMonies
{
    internal sealed class DiscordMoniesConfiguration
    {
        public required string DiscordToken { get; set; }
        public ulong? DebugServerId { get; set; }
    }
}
