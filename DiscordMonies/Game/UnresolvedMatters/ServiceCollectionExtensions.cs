namespace DiscordMonies.Game.UnresolvedMatters;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddUnresolvedMatterHandler<TResolver>(this IServiceCollection collection) where TResolver : class, IUnresolvedMatterResolver
    {
        return collection.AddUnresolvedMatterHandler(typeof(TResolver));
    }
    
    public static IServiceCollection AddUnresolvedMatterHandler(this IServiceCollection collection, Type resolverType)
    {
        if (!typeof(IUnresolvedMatterResolver).IsAssignableFrom(resolverType))
        {
            throw new InvalidOperationException
            (
                $"Unresolved Matter Handlers must implement {nameof(IUnresolvedMatterResolver)}<>."
            );
        }

        foreach (var implementedInterface in resolverType.GetInterfaces()
                     .Where(i => typeof(IUnresolvedMatterResolver).IsAssignableFrom(i))
                     .Distinct())
        {
            collection.AddScoped(implementedInterface, resolverType);
        }

        return collection;
    }
}