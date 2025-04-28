// ReSharper disable ClassCanBeSealed.Global
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable ClassNeverInstantiated.Global

#pragma warning disable CS8618

namespace Client.Models.Discord;

public sealed class AuthorizeSend : SendPayload<AuthorizeSend.Args>
{
    public override string cmd => "AUTHORIZE";

    public AuthorizeSend(string clientId, string[] scopes, string nonce)
    {
        args       = new Args { client_id = clientId, scopes = scopes };
        this.nonce = nonce;
    }

    public class Args
    {
        public string   client_id    { get; set; }
        public string[] scopes       { get; set; }
        public string   redirect_uri { get; set; }
    }
}

public sealed class AuthorizeReceive : ReceivePayload<AuthorizeReceive.Data>
{
    public class Data
    {
        public string code    { get; set; }
        public string message { get; set; }
    }
}
