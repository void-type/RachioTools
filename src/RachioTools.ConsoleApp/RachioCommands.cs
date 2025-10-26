using Cocona;
using Cocona.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RachioTools.Api;
using RachioTools.ConsoleApp.Configuration;
using RachioTools.ConsoleApp.Helpers;

namespace RachioTools.ConsoleApp;

public class RachioCommands
{
    private readonly RachioApiService _rachioApi;
    private readonly ILogger<RachioCommands> _logger;
    private readonly ICoconaAppContextAccessor _contextAccessor;
    private readonly IConfiguration _configuration;
    private readonly IOptions<RachioWinterizeSettings> _winterizeOptions;

    public RachioCommands(RachioApiService rachioApi, ILogger<RachioCommands> logger,
        ICoconaAppContextAccessor contextAccessor, IConfiguration configuration,
        IOptions<RachioWinterizeSettings> winterizeOptions)
    {
        _rachioApi = rachioApi;
        _logger = logger;
        _contextAccessor = contextAccessor;
        _configuration = configuration;
        _winterizeOptions = winterizeOptions;
    }

    public CancellationToken CancellationToken => _contextAccessor?.Current?.CancellationToken ?? CancellationToken.None;

    [Command(Description = "Save the information for your person entity to a file. This includes all devices and their zones.")]
    public async Task SavePerson(
        [Option(Description = "Path to save file. Defaults to './out/rachio-person.{timestamp}.json'.")]
        string? outFile)
    {
        var person = await _rachioApi.GetPerson(CancellationToken);

        if (person is null)
        {
            _logger.LogError("Person not found.");
            return;
        }

        var timestamp = DateTimeOffset.Now.ToString("yyyyMMdd_HHmmss");

        outFile = !string.IsNullOrWhiteSpace(outFile)
            ? outFile.Replace("{timestamp}", timestamp)
            : $"./out/rachio-person.{timestamp}.json";

        var file = await FileHelper.WriteJson(outFile, person, CancellationToken);

        _logger.LogInformation("Person saved to '{OutFile}'.", file.FullName);
    }

    [Command(Description = "Save the events for a device to a file.")]
    public async Task SaveDeviceEvents(
        [Option(Description = "Path to save file. Defaults to './out/rachio-events.{timestamp}.csv'. Can also use .json.")]
        string? outFile)
    {
        var winterizeSettings = _winterizeOptions.Value;
        var deviceName = winterizeSettings.DeviceName;

        if (string.IsNullOrEmpty(deviceName))
        {
            _logger.LogError("Device name not found in winterize configuration.");
            return;
        }

        var events = await _rachioApi.GetDeviceEvents(deviceName, CancellationToken).ToListAsync();

        var timestamp = DateTimeOffset.Now.ToString("yyyyMMdd_HHmmss");

        outFile = !string.IsNullOrWhiteSpace(outFile)
            ? outFile
            : $"./out/rachio-events.{timestamp}.csv";

        var file = await FileHelper.Write(outFile, events, CancellationToken);

        _logger.LogInformation("Device events saved to '{OutFile}'.", file.FullName);
    }

    [Command(Description = "Save the events for a device to a SQL database.")]
    public async Task SaveDeviceEventsSql()
    {
        var winterizeSettings = _winterizeOptions.Value;
        var deviceName = winterizeSettings.DeviceName;

        if (string.IsNullOrEmpty(deviceName))
        {
            _logger.LogError("Device name not found in winterize configuration.");
            return;
        }

        var connectionString = _configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection connection string not found in configuration.");

        var events = await _rachioApi.GetDeviceEvents(deviceName, CancellationToken).ToListAsync();

        await SqlHelper.SaveEvents(events, connectionString, CancellationToken);

        _logger.LogInformation("Device events saved to database.");
    }

    [Command(Description = "Activate or hibernate a Rachio device.")]
    public async Task SetDeviceHibernate(
        [Option(Description = "Include or true to hibernate. Exclude or false to activate.")]
        bool hibernate)
    {
        var winterizeSettings = _winterizeOptions.Value;
        var deviceName = winterizeSettings.DeviceName;

        if (string.IsNullOrEmpty(deviceName))
        {
            _logger.LogError("Device name not found in winterize configuration.");
            return;
        }

        var person = await _rachioApi.GetPerson(CancellationToken);

        if (person is null)
        {
            _logger.LogError("Person not found.");
            return;
        }

        var deviceId = person.Devices?
            .FirstOrDefault(x => deviceName.Equals(x.Name, StringComparison.OrdinalIgnoreCase))?
            .Id;

        if (deviceId is null)
        {
            _logger.LogError("Device '{DeviceName}' not found.", deviceName);
            return;
        }

        await _rachioApi.SetDeviceHibernate(deviceId, hibernate, CancellationToken);

        var status = hibernate ? "hibernate" : "active";

        _logger.LogInformation("Device '{DeviceName}' set to {Status}.", deviceName, status);
    }

    [Command(Description = "Run a winterization schedule on a Rachio device then hibernates the device. Set the schedule (zones and timings) in the appsettings.json file.")]
    public async Task Winterize()
    {
        var winterizeSettings = _winterizeOptions.Value;
        var winterizeSchedule = winterizeSettings.Zones;

        if (winterizeSchedule is null || winterizeSchedule.Count == 0)
        {
            _logger.LogError("No winterize schedule found.");
            return;
        }

        var person = await _rachioApi.GetPerson(CancellationToken);

        if (person is null)
        {
            _logger.LogError("Person not found.");
            return;
        }

        var device = person.Devices?
            .FirstOrDefault(x => x.Name?.Equals(winterizeSettings.DeviceName, StringComparison.OrdinalIgnoreCase) ?? false);

        if (device is null || device.Id is null)
        {
            _logger.LogError("Device '{DeviceName}' not found.", winterizeSettings.DeviceName);
            return;
        }

        var zones = device.Zones ?? [];

        if (zones.Count == 0)
        {
            _logger.LogError("No zones found for device '{DeviceName}'.", winterizeSettings.DeviceName);
            return;
        }

        var totalRunTimeSeconds = winterizeSchedule.Sum(x => x.DurationSeconds + x.RestAfterSeconds);
        var totalRunTimeTimeSpan = TimeSpan.FromSeconds(totalRunTimeSeconds);

        var zoneCount = winterizeSchedule
            .Select(x => x.ZoneName)
            .Distinct()
            .Count();

        _logger.LogInformation("Winterizing {ZoneCount} zones over {StepCount} steps. Total run time will be {TotalRunTimeTimeSpan}.",
            zoneCount, winterizeSchedule.Count, totalRunTimeTimeSpan);

        foreach (var (step, index) in winterizeSchedule.Select((step, index) => (step, index)))
        {
            var zoneId = zones
                .Find(x => step.ZoneName.Equals(x.Name, StringComparison.OrdinalIgnoreCase))?
                .Id;

            if (zoneId is null)
            {
                _logger.LogWarning("Zone '{ZoneName}' not found on device. Skipping.", step.ZoneName);
                continue;
            }

            _logger.LogInformation("({StepNumber}/{StepCount}) Starting zone '{ZoneName}' for {DurationSeconds} seconds.",
                index + 1, winterizeSchedule.Count, step.ZoneName, step.DurationSeconds);

            await _rachioApi.StartZone(zoneId!, step.DurationSeconds, CancellationToken);
            await Task.Delay(step.DurationSeconds * 1000, CancellationToken);

            _logger.LogInformation("Zone '{ZoneName}' complete. Resting for {RestAfterSeconds} seconds.",
                step.ZoneName, step.RestAfterSeconds);

            await Task.Delay(step.RestAfterSeconds * 1000, CancellationToken);
        }

        await SetDeviceHibernate(hibernate: true);

        _logger.LogInformation("Winterize complete.");
    }
}
