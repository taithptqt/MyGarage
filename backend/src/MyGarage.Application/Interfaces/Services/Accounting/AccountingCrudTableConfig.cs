using System.Collections.Generic;

namespace MyGarage.Services;

public sealed record AccountingCrudTableConfig(
    string TableName,
    HashSet<string> WritableColumns,
    bool HasUpdatedAt = true);
