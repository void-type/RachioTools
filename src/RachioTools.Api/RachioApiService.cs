using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using RachioTools.Api.Models;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;

namespace RachioTools.Api;

public class RachioApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RachioApiService> _logger;
    private readonly long _defaultCreateDateUnix = DateTimeOffset.Now.AddYears(-5).ToUnixTimeMilliseconds();

    public RachioApiService(HttpClient httpClient, ILogger<RachioApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Retrieve the information for a person entity. This includes all devices (controllers) and their zones.
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task<RachioPerson?> GetPerson(CancellationToken cancellationToken)
    {
        var personInfo = await _httpClient.GetFromJsonAsync<RachioPersonInfo>("person/info", cancellationToken);

        var personId = personInfo?.Id ?? throw new InvalidOperationException("Person not found.");

        return await _httpClient.GetFromJsonAsync<RachioPerson>($"person/{personId}", cancellationToken);
    }

    /// <summary>
    /// Start a zone.
    /// </summary>
    public async Task StartZone(string zoneId, int durationSeconds, CancellationToken cancellationToken)
    {
        var zoneStart = new
        {
            id = zoneId,
            duration = durationSeconds
        };

        var response = await _httpClient.PutAsJsonAsync("zone/start", zoneStart, cancellationToken: cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Get all available events for a device.
    /// </summary>
    /// <param name="deviceName">The name of the device to retrieve events for. If null, all devices are used.</param>
    public async IAsyncEnumerable<RachioDeviceEvent> GetDeviceEvents(string? deviceName, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var person = await GetPerson(cancellationToken)
            ?? throw new InvalidOperationException("Person not found.");

        if (person.Devices is null)
        {
            yield break;
        }

        var devices = deviceName is not null
            ? person.Devices.Where(d => d.Name == deviceName)
            : person.Devices;

        foreach (var device in devices)
        {
            var endTime = DateTimeOffset.Now;
            var startTime = endTime.AddMonths(-1);

            // To prevent infinite loops, we'll use the device's creation date as the lower bound.
            // If the device's creation date is not available, we'll go back a set number years.
            var createDateUnix = device.CreateDate ?? _defaultCreateDateUnix;

            while (startTime.ToUnixTimeMilliseconds() >= createDateUnix)
            {
                var query = new Dictionary<string, string?>
                {
                    ["startTime"] = startTime.ToUnixTimeMilliseconds().ToString(),
                    ["endTime"] = endTime.ToUnixTimeMilliseconds().ToString()
                };

                var uri = QueryHelpers.AddQueryString($"device/{device.Id}/event", query);

                var events = await _httpClient.GetFromJsonAsync<List<RachioDeviceEvent>>(uri, cancellationToken);

                if (events != null)
                {
                    foreach (var e in events)
                    {
                        yield return e;
                    }

                    _logger.LogInformation("Found {EventCount} events for the month of {StartTime}.", events.Count, startTime);
                }

                endTime = startTime.AddMilliseconds(-1);
                startTime = endTime.AddMonths(-1);
            }
        }
    }

    public async Task SetDeviceHibernate(string deviceId, bool hibernate, CancellationToken cancellationToken)
    {
        var action = hibernate ? "off" : "on";

        var body = new
        {
            id = deviceId
        };

        await _httpClient.PutAsJsonAsync($"device/{action}", body, cancellationToken);
    }
}
