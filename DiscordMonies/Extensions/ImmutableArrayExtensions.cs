namespace DiscordMonies.Extensions;

public static class ImmutableArrayExtensions
{
    public static int FirstIndexAfterIndex<T>(this IEnumerable<T> enumerable, int loc, Func<T, bool> predicate)
    {
        var list = enumerable.ToList();
        return Enumerable.Range(loc, list.Count)
            .Select(i => i % list.Count)
            .First(i => predicate(list[i]));
    }
}