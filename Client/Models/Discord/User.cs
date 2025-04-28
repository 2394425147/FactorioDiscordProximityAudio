// ReSharper disable ClassCanBeSealed.Global
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable ClassNeverInstantiated.Global

#pragma warning disable CS8618

namespace Client.Models.Discord;

public class User
{
    public string                                     id                     { get; set; }
    public string                                     username               { get; set; }
    public string                                     avatar                 { get; set; }
    public string                                     discriminator          { get; set; }
    public int                                        public_flags           { get; set; }
    public int                                        flags                  { get; set; }
    public object                                     banner                 { get; set; }
    public int                                        accent_color           { get; set; }
    public string                                     global_name            { get; set; }
    public AvatarDecorationData avatar_decoration_data { get; set; }
    public object                                     collectibles           { get; set; }
    public string                                     banner_color           { get; set; }
    public object                                     clan                   { get; set; }
    public object                                     primary_guild          { get; set; }
}
