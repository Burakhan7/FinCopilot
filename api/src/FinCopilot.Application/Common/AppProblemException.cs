namespace FinCopilot.Application.Common;

/// <summary>
/// İş kuralı hatası. Api katmanı bunu ProblemDetails'e çevirir; <see cref="Code"/> istemcinin
/// dallanması için sabit bir anahtardır (mesaj metni değişebilir, kod değişmez).
/// </summary>
public sealed class AppProblemException(int statusCode, string code, string title) : Exception(title)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
    public string Title { get; } = title;

    public static AppProblemException BadRequest(string code, string title) => new(400, code, title);
    public static AppProblemException Unauthorized(string code, string title) => new(401, code, title);
    public static AppProblemException Forbidden(string code, string title) => new(403, code, title);
    public static AppProblemException Conflict(string code, string title) => new(409, code, title);
    public static AppProblemException Locked(string code, string title) => new(423, code, title);
    public static AppProblemException TooManyRequests(string code, string title) => new(429, code, title);
}
