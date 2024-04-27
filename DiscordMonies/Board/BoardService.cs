using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;

namespace DiscordMonies.Board;

public sealed class BoardService
{
    private readonly FrozenDictionary<string, BoardInfo> boards;

    public BoardService()
    {
        const string ResourcePrefix = "DiscordMonies.Resources.Boards.";

        // Resources names are of the form
        // "DiscordMonies.Resources.Boards.Standard.board.json"

        var asm = typeof(BoardService).Assembly;
        var maps =
            from name in asm.GetManifestResourceNames()
            where name.StartsWith(ResourcePrefix)
            let stripped = name[ResourcePrefix.Length..].Split('.', 2)
            group name by stripped[0];

        boards = maps.ToFrozenDictionary(g => g.Key, mapResources =>
        {
            T DeserializeResource<T>(string name)
            {
                var resourceName = mapResources.First(r => r.EndsWith(name));
                var stream = asm.GetManifestResourceStream(resourceName) ?? throw new UnreachableException();
                return JsonSerializer.Deserialize<T>(stream) ?? throw new InvalidDataException("Board JSON should not be null value");
            }

            return new BoardInfo
            {
                Board = DeserializeResource<Board>("board.json"),
                TitleDeeds = DeserializeResource<ImmutableArray<TitleDeed>>("titledeeds.json"),
                ChanceCards = DeserializeResource<ImmutableArray<LuckCard>>("chance.json"),
                ChestCards = DeserializeResource<ImmutableArray<LuckCard>>("communitychest.json")
            };
        });
    }

    public BoardInfo GetBoard(string name)
    {
        return boards[name];
    }
}

public sealed class BoardInfo
{
    public required Board Board { get; init; }
    public required ImmutableArray<TitleDeed> TitleDeeds { get; init; }
    public required ImmutableArray<LuckCard> ChanceCards { get; init; }
    public required ImmutableArray<LuckCard> ChestCards { get; init; }
}