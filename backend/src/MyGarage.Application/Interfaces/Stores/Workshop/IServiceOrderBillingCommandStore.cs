using System.Collections.Generic;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Stores.Workshop;

public interface IServiceOrderBillingCommandStore
{
    void RemoveServiceOrderDetail(ServiceOrderDetail detail);
    void AddServiceOrderDetail(ServiceOrderDetail detail);
    void AddServiceOrderDetails(IEnumerable<ServiceOrderDetail> details);
    void AddInsuranceAssets(IEnumerable<ServiceOrderInsuranceAsset> rows);
    void RemoveInsuranceAssets(IEnumerable<ServiceOrderInsuranceAsset> rows);
}
