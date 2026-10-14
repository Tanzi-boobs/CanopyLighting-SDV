// Compile-only API shim. The output mod DLL must run with the real SMAPI assembly.
namespace StardewModdingAPI
{
    public abstract class Mod
    {
        protected IModHelper Helper { get; private set; } = null!;
        protected IMonitor Monitor { get; private set; } = null!;
        public abstract void Entry(IModHelper helper);
    }

    public interface IModHelper
    {
        T ReadConfig<T>() where T : class, new();
        void WriteConfig<T>(T config);
        IModEvents Events { get; }
    }

    public interface IModEvents { IInputEvents Input { get; } }
    public interface IInputEvents
    {
        event EventHandler<StardewModdingAPI.Events.ButtonPressedEventArgs> ButtonPressed;
    }

    public interface IMonitor { void Log(string message, LogLevel level = LogLevel.Debug); }

    public enum LogLevel { Trace, Debug, Info, Warn, Error, Alert }
    public enum SButton { MouseLeft }
}

namespace StardewModdingAPI.Events
{
    public sealed class ButtonPressedEventArgs : EventArgs
    {
        public StardewModdingAPI.SButton Button { get; set; }
        public void Suppress() { }
    }
}