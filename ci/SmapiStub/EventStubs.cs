using System;

namespace StardewModdingAPI.Events;

public interface IModEvents
{
    IGameLoopEvents GameLoop { get; }
    IDisplayEvents Display { get; }
}

public interface IDisplayEvents
{
    event EventHandler<RenderedWorldEventArgs> RenderedWorld;
}

public sealed class RenderedWorldEventArgs : EventArgs
{
    public object SpriteBatch { get; set; } = null!;
}

public interface IGameLoopEvents
{
    event EventHandler<GameLaunchedEventArgs> GameLaunched;
}

public sealed class GameLaunchedEventArgs : EventArgs
{
}
