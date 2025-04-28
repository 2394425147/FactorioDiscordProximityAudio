// ReSharper disable ClassCanBeSealed.Global
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable ClassNeverInstantiated.Global

#pragma warning disable CS8618

namespace Client.Models.Discord;

public sealed class Handshake : ReceivePayload<Handshake.Data>
{
    public class Data
    {
        public int    v      { get; set; }
        public Config config { get; set; }
        public User   user   { get; set; }
    }

    public class Config
    {
        public string cdn_host     { get; set; }
        public string api_endpoint { get; set; }
        public string environment  { get; set; }
    }
}
