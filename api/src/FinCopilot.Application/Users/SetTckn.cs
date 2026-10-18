using FinCopilot.Application.Abstractions;
using FinCopilot.Application.Common;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FinCopilot.Application.Users;

/// <summary>TCKN ilk banka bağlamada bir kez alınır; sonradan değiştirilemez.</summary>
public sealed record SetTcknCommand(Guid UserId, string Tckn) : IRequest;

public sealed class SetTcknValidator : AbstractValidator<SetTcknCommand>
{
    public SetTcknValidator() =>
        RuleFor(x => x.Tckn).Must(Tckn.IsValid).WithMessage("Geçerli bir T.C. kimlik numarası girin.");
}

public sealed class SetTcknHandler(IAppDbContext db, ISecretProtector protector, ITcknHasher hasher)
    : IRequestHandler<SetTcknCommand>
{
    public async Task Handle(SetTcknCommand request, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, ct)
            ?? throw AppProblemException.Unauthorized("user_not_found", "Kullanıcı bulunamadı.");

        if (user.TcknEnc is not null)
            throw AppProblemException.Conflict("tckn_already_set", "Kimlik numarası daha önce kaydedildi.");

        var hash = hasher.Hash(request.Tckn);
        if (await db.Users.AnyAsync(u => u.TcknHash == hash, ct))
            throw AppProblemException.Conflict("tckn_in_use", "Bu kimlik numarası başka bir hesapta kayıtlı.");

        user.TcknEnc = protector.Protect(request.Tckn, SecretPurpose.Tckn);
        user.TcknHash = hash;
        await db.SaveChangesAsync(ct);
    }
}
