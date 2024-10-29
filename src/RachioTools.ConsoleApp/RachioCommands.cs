using Cocona;
using Cocona.Application;
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

    public RachioCommands(RachioApiService rachioApi, ILogger<RachioCommands> logger,
        ICoconaAppContextAccessor contextAccessor)
    {
        _rachioApi = rachioApi;
        _logger = logger;
        _contextAccessor = contextAccessor;
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
            ? outFile
            : $"./out/rachio-person.{timestamp}.json";

        var file = await FileHelper.WriteJson(outFile, person, CancellationToken);

        _logger.LogInformation("Person saved to '{OutFile}'.", file.FullName);
    }

    [Command(Description = "Save the events for a device to a file.")]
    public async Task SaveDeviceEvents(
        [Option(Description = "Path to save file. Defaults to './out/rachio-events.{timestamp}.csv'. Can also use .json.")]
        string? outFile,
        [Option(Description = "Name of the device to retrieve events for. If null, all devices are used.")]
        string? deviceName)
    {
        var events = await _rachioApi.GetDeviceEvents(deviceName, CancellationToken).ToListAsync();

        var timestamp = DateTimeOffset.Now.ToString("yyyyMMdd_HHmmss");

        outFile = !string.IsNullOrWhiteSpace(outFile)
            ? outFile
            : $"./out/rachio-events.{timestamp}.csv";

        var file = await FileHelper.Write(outFile, events, CancellationToken);

        _logger.LogInformation("Device events saved to '{OutFile}'.", file.FullName);
    }

    [Command(Description = "Save the events for a device to a file.")]
    public async Task SaveDeviceEventsSql(
        [Option(Description = "Connection string to database.")]
        string connectionString,
        [Option(Description = "Table name to save. Defaults to 'RachioDeviceEvent'.")]
        string? tableName,
        [Option(Description = "Name of the device to retrieve events for. If null, all devices are used.")]
        string? deviceName)
    {
        var events = await _rachioApi.GetDeviceEvents(deviceName, CancellationToken).ToListAsync();

        await SqlHelper.SaveEvents(events, connectionString, tableName, CancellationToken);

        _logger.LogInformation("Device events saved to database.");
    }

    [Command(Description = "Activate or hibernate a Rachio device.")]
    public async Task SetDeviceHibernate(
        [Option(Description = "Device to activate/hibernate.")]
        string deviceName,
        [Option(Description = "Include or true to hibernate. Exclude or false to activate.")]
        bool hibernate)
    {
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

    [Command(Description = "Run a winterization schedule on a Rachio device. Set the schedule (zones and timings) in the appsettings.json file.")]
    public async Task Winterize([FromService] IOptions<RachioWinterizeSettings> winterizeOptions)
    {
        var winterizeSettings = winterizeOptions.Value;
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

        _logger.LogInformation("Setting device to hibernate.");
        await _rachioApi.SetDeviceHibernate(device.Id, true, CancellationToken);

        _logger.LogInformation("Winterize complete.");
    }
}
