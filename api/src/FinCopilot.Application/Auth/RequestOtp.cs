using FluentValidation;
using MediatR;

namespace FinCopilot.Application.Auth;

public sealed record RequestOtpCommand(string Phone) : IRequest<RequestOtpResult>;

public sealed record RequestOtpResult(Guid ChallengeId, DateTimeOffset ExpiresAt);

public sealed class RequestOtpValidator : AbstractValidator<RequestOtpCommand>
{
    public RequestOtpValidator() => RuleFor(x => x.Phone).ValidPhone();
}

public sealed class RequestOtpHandler(OtpService otp) : IRequestHandler<RequestOtpCommand, RequestOtpResult>
{
    public async Task<RequestOtpResult> Handle(RequestOtpCommand request, CancellationToken ct)
    {
        var challenge = await otp.SendAsync(PhoneNumber.Normalize(request.Phone), ct);
        return new RequestOtpResult(challenge.Id, challenge.ExpiresAt);
    }
}
