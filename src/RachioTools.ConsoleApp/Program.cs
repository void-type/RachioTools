using Cocona;
using Microsoft.Extensions.DependencyInjection;
using RachioTools.Api;
using RachioTools.ConsoleApp;
using RachioTools.ConsoleApp.Configuration;

var builder = CoconaApp.CreateBuilder();
var services = builder.Services;
var configuration = builder.Configuration;

services.AddRachioApi(configuration);

services.Configure<RachioWinterizeSettings>(configuration.GetSection("Winterize"));

var app = builder.Build();

app.AddCommands<RachioCommands>();

await app.RunAsync();
