// ReSharper disable ClassCanBeSealed.Global
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable ClassNeverInstantiated.Global

#pragma warning disable CS8618

namespace Client.Models.Discord;

public static class HttpAuthenticateSend
{
    public static FormUrlEncodedContent Create(string clientId, string clientSecret, string code, string redirectUri)
    {
        var records = new List<KeyValuePair<string, string>>
        {
            new("grant_type", "authorization_code"),
            new("client_id", clientId),
            new("client_secret", clientSecret),
            new("code", code),
            new("redirect_uri", redirectUri)
        };

        return new FormUrlEncodedContent(records);
    }
}

public sealed class HttpAuthenticateReceive
{
    public string token_type    { get; set; }
    public string access_token  { get; set; }
    public int    expires_in    { get; set; }
    public string refresh_token { get; set; }
    public string scope         { get; set; }
}
