namespace MayShow.Interfaces;

interface ILogger
{
    void LogInfo(string message, params object[]? arguments);
}