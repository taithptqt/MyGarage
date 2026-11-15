namespace MyGarage.Application.Payroll;

public interface IPayrollStore :
    IPayrollTransactionStore,
    ISalaryProfileStore,
    IPayrollRunStore,
    IPayrollCalculationDataStore
{
}
