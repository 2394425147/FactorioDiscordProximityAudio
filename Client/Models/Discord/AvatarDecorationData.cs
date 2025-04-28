// ReSharper disable ClassCanBeSealed.Global
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable ClassNeverInstantiated.Global

#pragma warning disable CS8618

namespace Client.Models.Discord;

public class AvatarDecorationData
{
    public string asset      { get; set; }
    public string sku_id     { get; set; }
    public object expires_at { get; set; }
}
