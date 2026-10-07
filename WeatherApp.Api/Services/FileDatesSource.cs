namespace WeatherApp.Api.Services;

public interface IDatesSource
{
    Task<IReadOnlyList<string>> ReadLinesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Reads raw date lines from a text file. Kept behind an interface so the service can be tested without disk I/O.</summary>
public sealed class FileDatesSource : IDatesSource
{
    private readonly string _path;

    public FileDatesSource(string path) => _path = path;

    public async Task<IReadOnlyList<string>> ReadLinesAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path))
        {
            throw new FileNotFoundException("Dates file not found.", _path);
        }

        return await File.ReadAllLinesAsync(_path, cancellationToken);
    }
}