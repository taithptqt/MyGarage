using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Queries;

public interface IQcChecklistTemplateDataService
{
    Task<string> GetServiceOrderCategoryAsync(long serviceOrderId, CancellationToken ct = default);
    Task<List<QcChecklistTemplate>> GetTemplatesAsync(string normalizedServiceType, bool includeInactive, CancellationToken ct = default);
    Task<QcChecklistTemplate?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<QcChecklistTemplate> CreateAsync(QcChecklistTemplate entity, CancellationToken ct = default);
    Task<QcChecklistTemplate?> UpdateAsync(
        long id,
        string name,
        string? description,
        string? serviceCategory,
        bool? isActive,
        List<QcChecklistTemplateItem>? replacementItems,
        CancellationToken ct = default);
    Task<(bool Found, bool Deactivated)> DeleteOrDeactivateAsync(long id, CancellationToken ct = default);
}
