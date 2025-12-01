namespace Koornik.Application.Core.Services;

public interface ILoggerService
{
    void LogException(Exception ex);
    void LogError(string className, string methodName, string errorMessage);
    void LogError(string errorMessageTemplate, params object[] args);
    void LogInfo(string infoMessage);
    void LogInfo(string infoMessage, params object[] args);
    void LogDebug(string className, string methodName, string debugMessage);
}