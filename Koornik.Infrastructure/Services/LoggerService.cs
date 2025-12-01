using Koornik.Application.Core.Services;
using Serilog;

namespace Koornik.Infrastructure.Services;

public class LoggerService : ILoggerService
{
    private readonly ILogger _logger;
    
    public LoggerService(ILogger logger)
    {
        _logger = logger;
    }
    
    public void LogException(Exception ex)
    {
        _logger.Error(ex, "An exception occurred:");
    }

    public void LogError(string className, string methodName, string errorMessage)
    {
        _logger.Error("[{ClassName}][{MethodName}] {ErrorMessage}",
            className,
            methodName,
            errorMessage);
    }

    public void LogError(string errorMessageTemplate, params object[] args)
    {
        _logger.Error(errorMessageTemplate, args);
    }

    public void LogInfo(string infoMessage)
    {
        _logger.Information(infoMessage);
    }

    public void LogInfo(string infoMessage, params object[] args)
    {
        _logger.Information(infoMessage, args);
    }

    public void LogDebug(string className, string methodName, string debugMessage)
    {
        _logger.Debug("[{ClassName}][{MethodName}] {DebugMessage}",
            className,
            methodName,
            debugMessage);
    }
}