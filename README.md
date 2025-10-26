# RachioTools

You'll need an API key. See [Rachio docs](https://rachio.readme.io/reference/getting-started).

Use `-h` to see help and available commands.

## Year-End Process

Attach compressor to sprinklers.

Setup appsettings.Production.json with your API Key, device name, zone timings, and SQL connection string.

`cd src/RachioTools.ConsoleApp`

Run the winterize process and hibernate the device.

`dotnet run -- winterize`
`dotnet run -- set-device-hibernate --hibernate true`

Create tables if they don't exist, then save device events that are missing.

`dotnet run -- save-device-events-sql`
