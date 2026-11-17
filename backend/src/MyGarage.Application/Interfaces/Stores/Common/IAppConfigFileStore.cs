using System;
using System.Threading;
using System.Threading.Tasks;

namespace MyGarage.Application.Interfaces.Stores.Common;

/// <summary>
/// Persists and loads JSON configuration snapshots for administrative settings without exposing filesystem paths to Application or Api layers.
/// </summary>
public interface IAppConfigFileStore
{
    Task<T> ReadOrDefaultAsync<T>(string fileName, Func<T> createDefault, CancellationToken ct = default)
        where T : class;

    Task<T> ReadStrictOrDefaultAsync<T>(string fileName, Func<T> createDefault, CancellationToken ct = default)
        where T : class => ReadOrDefaultAsync(fileName, createDefault, ct);

    Task WriteAsync<T>(string fileName, T settings, CancellationToken ct = default)
        where T : class;
}
