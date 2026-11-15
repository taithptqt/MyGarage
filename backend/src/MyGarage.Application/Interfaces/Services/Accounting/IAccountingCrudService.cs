namespace MyGarage.Interfaces
{
    public interface IAccountingCrudService :
        IAccountingSchemaService,
        IAccountingTableQueryService,
        IAccountingTableCommandService,
        IAccountingSqlQueryService,
        IAccountingSqlCommandService
    {
    }
}
