namespace MyGarage.Interfaces;

public interface IPiiAnonymizerService
{
    string AnonymizeText(string? input);
    object AnonymizeObjectGraph(object? input);
}
