namespace MyGarage.Interfaces
{
    public interface IAuthConfigProvider
    {
        int GetPasswordResetTokenExpiryMinutes();
        string GetPasswordResetUrlBase();
    }
}
