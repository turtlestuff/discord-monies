using Remora.Discord.Hosting.Extensions;
using Remora.Discord.Caching.Extensions;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Interactivity.Extensions;
using Remora.Discord.Extensions.Extensions;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.Commands.Services;
using Remora.Rest.Core;

var builder = Host.CreateApplicationBuilder(args);

var dmConfig = builder.Configuration.GetSection("DiscordMonies").Get<DiscordMoniesConfiguration>() ?? throw new("Could not bind configuration.");

builder.Services
    .AddDiscordService(_ => dmConfig.DiscordToken)
    .AddDiscordCaching()
    .AddDiscordCommands(enableSlash: true)
    .AddInteractivity()
    .AddCommandGroupsFromAssembly(typeof(Program).Assembly)
    .AddRespondersFromAssembly(typeof(Program).Assembly);

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
        await api.BulkOverwriteGuildApplicationCommandsAsync(appInfo.Entity.ID, new(277922530973581312), []);
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
