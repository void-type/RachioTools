namespace RachioTools.Api.Models;

public class RachioDevice
{
    public long? CreateDate { get; set; }
    public string? Id { get; set; }
    public string? Status { get; set; }
    public List<RachioZone>? Zones { get; set; }
    public string? TimeZone { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Zip { get; set; }
    public string? Name { get; set; }
    public List<RachioScheduleRule>? ScheduleRules { get; set; }
    public string? SerialNumber { get; set; }
    public long? RainDelayExpirationDate { get; set; }
    public long? RainDelayStartDate { get; set; }
    public string? MacAddress { get; set; }
    public string? Model { get; set; }
    public string? ScheduleModeType { get; set; }
    public double? Elevation { get; set; }
    public List<object>? Webhooks { get; set; }
    public bool? Paused { get; set; }
    public bool? Deleted { get; set; }
    public bool? HomeKitCompatible { get; set; }
    public bool? RainSensorTripped { get; set; }
    public bool? On { get; set; }
    public List<object>? FlexScheduleRules { get; set; }
    public int? UtcOffset { get; set; }
}
