namespace Auth.Api.Dtos;

public sealed record TokenResponse(string AccessToken, string TokenType, int ExpiresIn);
