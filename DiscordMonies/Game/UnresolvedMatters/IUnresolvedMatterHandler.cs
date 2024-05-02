using JetBrains.Annotations;

namespace DiscordMonies.Game.UnresolvedMatters;

public interface IUnresolvedMatterResolver;

public interface IUnresolvedMatterResolver<T> : IUnresolvedMatterResolver where T : IUnresolvedMatter
{
    /// <summary>
    /// Attempts to resolve an unresolved matter.
    /// </summary>
    /// <typeparam name="T">The type of unresolved matter.</typeparam>
    /// <param name="matter">The unresolved matter to resolve.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// If the matter can be resolved, the implementation is expected to call <see cref="GameControllerService.ProcessNextUnresolvedMatter"/>
    /// Otherwise, the implementation is expected to write a message to the channel asking the player how to continue.
    /// </remarks>
    [UsedImplicitly]
    public Task Resolve(T matter);
}