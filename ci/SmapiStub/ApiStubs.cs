namespace StardewModdingAPI;

public enum LogLevel
{
    Trace,
    Debug,
    Info,
    Warn,
    Error,
    Alert
}

public interface IMonitor
{
    void Log(string message, LogLevel level = LogLevel.Info);
}

public interface IModHelper
{
    string DirectoryPath { get; }
    T ReadConfig<T>() where T : new();
    void WriteConfig<T>(T config);
}

public abstract class Mod
{
    protected IMonitor Monitor { get; } = new NullMonitor();
    public abstract void Entry(IModHelper helper);

    private sealed class NullMonitor : IMonitor
    {
        public void Log(string message, LogLevel level = LogLevel.Info) { }
    }
}
