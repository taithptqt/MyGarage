using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Interfaces;

public interface IQcChecklistTemplateCommandService
{
    Task<QcTemplateListEnvelopeDto> GetTemplatesAsync(
        string? serviceType = null,
        long? serviceOrderId = null,
        bool includeInactive = false,
        CancellationToken ct = default);

    Task<IReadOnlyList<QcTemplateDto>> GetTemplatesListAsync(
        string? serviceType = null,
        bool includeInactive = true,
        CancellationToken ct = default);

    Task<QcChecklistTemplateCommandResult<QcTemplateDto>> GetByIdAsync(
        long id,
        CancellationToken ct = default);

    Task<QcChecklistTemplateCommandResult<QcTemplateDto>> CreateAsync(
        QcTemplateUpsertDto? payload,
        CancellationToken ct = default);

    Task<QcChecklistTemplateCommandResult<QcTemplateDto>> UpdateAsync(
        long id,
        QcTemplateUpsertDto? payload,
        CancellationToken ct = default);

    Task<QcChecklistTemplateCommandResult<QcTemplateDeleteResultDto>> DeleteAsync(
        long id,
        CancellationToken ct = default);
}
