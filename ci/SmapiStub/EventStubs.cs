using System;

namespace StardewModdingAPI.Events;

public interface IModEvents
{
    IGameLoopEvents GameLoop { get; }
}

public interface IGameLoopEvents
{
    event EventHandler<GameLaunchedEventArgs> GameLaunched;
}

public sealed class GameLaunchedEventArgs : EventArgs
{
}
