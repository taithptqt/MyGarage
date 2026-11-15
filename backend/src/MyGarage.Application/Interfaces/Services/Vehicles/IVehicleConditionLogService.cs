namespace MyGarage.Interfaces
{
    public interface IVehicleConditionLogService :
        IVehicleConditionLogCrudService,
        IVehicleConditionLogReadService,
        IVehicleConditionLogValidationService,
        IVehicleConditionLogCommandService
    {
    }
}
