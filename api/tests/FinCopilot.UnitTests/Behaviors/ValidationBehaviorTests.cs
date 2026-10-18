using FinCopilot.Application.Behaviors;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace FinCopilot.UnitTests.Behaviors;

public class ValidationBehaviorTests
{
    public sealed record Ping(string Phone) : IRequest<string>;

    public sealed class PingHandler : IRequestHandler<Ping, string>
    {
        public Task<string> Handle(Ping request, CancellationToken cancellationToken) => Task.FromResult("pong");
    }

    public sealed class PingValidator : AbstractValidator<Ping>
    {
        public PingValidator() => RuleFor(x => x.Phone).NotEmpty().Matches(@"^5\d{9}$");
    }

    private static IMediator CreateMediator()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(ValidationBehaviorTests).Assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });
        services.AddValidatorsFromAssembly(typeof(ValidationBehaviorTests).Assembly);
        return services.BuildServiceProvider().GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task Valid_request_reaches_handler()
    {
        Assert.Equal("pong", await CreateMediator().Send(new Ping("5321234567")));
    }

    [Fact]
    public async Task Invalid_request_throws_before_handler()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() => CreateMediator().Send(new Ping("abc")));

        Assert.Contains(ex.Errors, e => e.PropertyName == nameof(Ping.Phone));
    }
}
