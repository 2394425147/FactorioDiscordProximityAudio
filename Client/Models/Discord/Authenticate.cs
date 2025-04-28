// ReSharper disable ClassCanBeSealed.Global
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable ClassNeverInstantiated.Global

#pragma warning disable CS8618

namespace Client.Models.Discord;

public sealed class AuthenticateSend : SendPayload<AuthenticateSend.Args>
{
    public AuthenticateSend(string accessToken, string nonce)
    {
        args       = new Args { access_token = accessToken };
        this.nonce = nonce;
    }

    public class Args
    {
        public string access_token { get; set; }
    }

    public override string cmd { get; set; } = "AUTHENTICATE";
}

public sealed class AuthenticateReceive : ReceivePayload<AuthenticateReceive.Data>
{
    public class Data
    {
        public Application application  { get; set; }
        public string      expires      { get; set; }
        public string[]    scopes       { get; set; }
        public User        user         { get; set; }
        public string      access_token { get; set; }
    }

    public class Application
    {
        public string                   id                       { get; set; }
        public string                   name                     { get; set; }
        public object                   icon                     { get; set; }
        public string                   description              { get; set; }
        public object                   type                     { get; set; }
        public Bot                      bot                      { get; set; }
        public string                   summary                  { get; set; }
        public bool                     is_monetized             { get; set; }
        public bool                     is_verified              { get; set; }
        public bool                     is_discoverable          { get; set; }
        public bool                     bot_public               { get; set; }
        public bool                     bot_require_code_grant   { get; set; }
        public Integration_types_config integration_types_config { get; set; }
        public string                   verify_key               { get; set; }
        public int                      flags                    { get; set; }
        public bool                     hook                     { get; set; }
        public bool                     storefront_available     { get; set; }
    }

    public class Bot
    {
        public string id                     { get; set; }
        public string username               { get; set; }
        public object avatar                 { get; set; }
        public string discriminator          { get; set; }
        public int    public_flags           { get; set; }
        public int    flags                  { get; set; }
        public bool   bot                    { get; set; }
        public object banner                 { get; set; }
        public object accent_color           { get; set; }
        public object global_name            { get; set; }
        public object avatar_decoration_data { get; set; }
        public object collectibles           { get; set; }
        public object banner_color           { get; set; }
        public object clan                   { get; set; }
        public object primary_guild          { get; set; }
    }

    public class Integration_types_config
    {
        public _ _ { get; set; }
    }

    public class _
    {
        public Oauth2_install_params oauth2_install_params { get; set; }
    }

    public class Oauth2_install_params
    {
        public string[] scopes      { get; set; }
        public string   permissions { get; set; }
    }
}
