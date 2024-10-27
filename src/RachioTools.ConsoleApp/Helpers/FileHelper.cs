using System.Globalization;
using System.Text.Json;

namespace RachioTools.ConsoleApp.Helpers;

public static class FileHelper
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public static async Task<FileInfo> WriteJson<T>(string outFile, T content, CancellationToken cancellationToken)
    {
        var file = EnsureFileWritable(outFile);

        await WriteJson(file, content, cancellationToken);

        return file;
    }

    public static async Task<FileInfo> WriteJson<T>(FileInfo file, T content, CancellationToken cancellationToken)
    {
        var fileJson = JsonSerializer.Serialize(content, _jsonOptions);

        await File.WriteAllTextAsync(file.FullName, fileJson, cancellationToken);

        return file;
    }

    public static async Task<FileInfo> WriteCsv<T>(string outFile, IEnumerable<T> content, CancellationToken cancellationToken)
    {
        var file = EnsureFileWritable(outFile);

        await WriteCsv(file, content, cancellationToken);

        return file;
    }

    public static async Task<FileInfo> WriteCsv<T>(FileInfo file, IEnumerable<T> content, CancellationToken cancellationToken)
    {
        await using var writer = new StreamWriter(file.FullName);
        await using var csv = new CsvHelper.CsvWriter(writer, CultureInfo.InvariantCulture);

        await csv.WriteRecordsAsync(content, cancellationToken);

        return file;
    }

    public static async Task<FileInfo> Write<T>(string outFile, IEnumerable<T> content, CancellationToken cancellationToken)
    {
        var file = EnsureFileWritable(outFile);

        switch (file.Extension)
        {
            case ".json":
                await WriteJson(file, content, cancellationToken);
                break;

            // In the event of an unsupported file type, default to CSV.
            default:
                file = new FileInfo(Path.ChangeExtension(file.FullName, ".csv"));
                await WriteCsv(file, content, cancellationToken);
                break;
        }

        return file;
    }

    private static FileInfo EnsureFileWritable(string outFile)
    {
        var file = new FileInfo(outFile);
        file.Directory?.Create();
        return file;
    }
}
