namespace OpsDesk.Application.Auth.Interfaces;

public interface IPasswordValidator
{
    void Validate(string password);
}