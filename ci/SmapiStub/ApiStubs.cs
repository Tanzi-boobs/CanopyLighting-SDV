using StardewModdingAPI.Events;

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

public interface IManifest
{
}

public interface IModRegistry
{
    T? GetApi<T>(string uniqueID) where T : class;
}

public interface IMonitor
{
    void Log(string message, LogLevel level = LogLevel.Info);
}

public interface IModHelper
{
    string DirectoryPath { get; }
    IModRegistry ModRegistry { get; }
    IModEvents Events { get; }
    T ReadConfig<T>() where T : new();
    void WriteConfig<T>(T config);
}

public abstract class Mod
{
    protected IMonitor Monitor { get; } = new NullMonitor();
    protected IManifest ModManifest { get; } = new NullManifest();
    public abstract void Entry(IModHelper helper);

    private sealed class NullMonitor : IMonitor
    {
        public void Log(string message, LogLevel level = LogLevel.Info) { }
    }

    private sealed class NullManifest : IManifest
    {
    }
}
