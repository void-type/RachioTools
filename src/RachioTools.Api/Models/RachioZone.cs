namespace RachioTools.Api.Models;

public class RachioZone
{
    public string? Id { get; set; }
    public int? ZoneNumber { get; set; }
    public string? Name { get; set; }
    public bool? Enabled { get; set; }
    public RachioCustomNozzle? CustomNozzle { get; set; }
    public double? AvailableWater { get; set; }
    public double? RootZoneDepth { get; set; }
    public double? ManagementAllowedDepletion { get; set; }
    public double? Efficiency { get; set; }
    public int? YardAreaSquareFeet { get; set; }
    public int? IrrigationAmount { get; set; }
    public double? DepthOfWater { get; set; }
    public int? Runtime { get; set; }
}
