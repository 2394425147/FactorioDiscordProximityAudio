// ReSharper disable ClassCanBeSealed.Global
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable ClassNeverInstantiated.Global

#pragma warning disable CS8618

namespace Client.Models.Discord;

public class VoiceState
{
    public bool mute      { get; set; }
    public bool deaf      { get; set; }
    public bool self_mute { get; set; }
    public bool self_deaf { get; set; }
    public bool suppress  { get; set; }
}
