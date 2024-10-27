namespace RachioTools.ConsoleApp.Configuration;

public class RachioWinterizeSettings
{
    public string DeviceName { get; set; } = string.Empty;
    public List<RachioWinterizeSettingsZone> Zones { get; set; } = [];
}
