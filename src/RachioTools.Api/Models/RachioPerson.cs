namespace RachioTools.Api.Models;

public class RachioPerson
{
    public string? Id { get; set; }
    public string? Username { get; set; }
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public List<RachioDevice>? Devices { get; set; }
    public bool? Enabled { get; set; }
}
