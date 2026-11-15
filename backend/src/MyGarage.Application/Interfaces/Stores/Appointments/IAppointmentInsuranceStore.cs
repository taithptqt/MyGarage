using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Stores.Appointments;

public interface IAppointmentInsuranceStore
{
    Task<bool> ActiveInsuranceCompanyExistsAsync(long insuranceCompanyId);
    Task<Dictionary<long, string>> GetActiveInsuranceCompanyNamesByIdsAsync(IReadOnlyCollection<long> insuranceCompanyIds);
    Task<bool> HasAppointmentInsuranceAssetsAsync(long appointmentId);
    Task<List<AppointmentInsuranceAsset>> GetAppointmentInsuranceAssetsAsync(long appointmentId);
    Task<List<AppointmentInsuranceAsset>> GetAppointmentInsuranceAssetsByAppointmentIdsAsync(IReadOnlyCollection<long> appointmentIds);
    Task<bool> HasServiceOrderInsuranceAssetsAsync(long serviceOrderId);
    Task<List<AppointmentInsuranceUpload>> GetStagedInsuranceUploadsByUrlsAsync(IReadOnlyCollection<string> urls);
}
