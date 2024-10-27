namespace RachioTools.Api.Models;

public class RachioDeviceEvent
{
    public string? Id { get; set; }
    public string? DeviceId { get; set; }
    public string? Category { get; set; }
    public string? Type { get; set; }
    public string? SubType { get; set; }
    public long EventDate { get; set; }
    public string? Summary { get; set; }
    public bool Hidden { get; set; }
    public string? Topic { get; set; }
}
