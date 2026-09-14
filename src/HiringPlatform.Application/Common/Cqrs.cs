using System.Reflection;

using HiringPlatform.Domain.Common;

using Microsoft.Extensions.DependencyInjection;

namespace HiringPlatform.Application.Common;

// Port of cv-sv core/cqrs.ts: callers build a typed request and send it through the mediator,
// which routes it to exactly one handler. Commands mutate, queries read.

public interface IRequest<TResponse>;

public interface ICommand<TResponse> : IRequest<TResponse>;

public interface IQuery<TResponse> : IRequest<TResponse>;

public interface IHandler<in TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    Task<Result<TResponse>> Handle(TRequest request, CancellationToken ct);
}

public sealed class Mediator(
    IServiceProvider services
)
{
    public async Task<Result<TResponse>> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
    {
        var handlerType = typeof(IHandler<,>).MakeGenericType(request.GetType(), typeof(TResponse));
        var handler = services.GetService(handlerType)
            ?? throw new InvalidOperationException($"No handler registered for type: {request.GetType().Name}");

        try
        {
            var method = handlerType.GetMethod(nameof(IHandler<IRequest<TResponse>, TResponse>.Handle))!;
            return await (Task<Result<TResponse>>)method.Invoke(handler, [request, ct])!;
        }
        // like cv-sv createHandler: invariant violations become failed results, not 500s
        catch (DomainException ex)
        {
            return ApplicationError.Validation(ex.Message);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is DomainException)
        {
            return ApplicationError.Validation(ex.InnerException.Message);
        }
    }
}

public static class ApplicationServiceCollectionExtensions
{
    /// <summary>Registers the mediator and every handler in this assembly (cv-sv registerAll equivalent).</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<Mediator>();
        services.AddScoped<AccessGuard>();

        var handlers =
            from type in typeof(Mediator).Assembly.GetTypes()
            where type is { IsAbstract: false, IsInterface: false }
            from iface in type.GetInterfaces()
            where iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IHandler<,>)
            select (iface, type);

        foreach (var (iface, type) in handlers)
            services.AddScoped(iface, type);
        return services;
    }
}
