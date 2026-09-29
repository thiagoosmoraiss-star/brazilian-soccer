namespace Game.Core.Logging
{
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warning = 2,
        Error = 3,
    }

    /// <summary>
    /// Logging abstraction for the pure zone. The Unity zone provides an implementation
    /// backed by the Unity console; tests and headless runs can use any implementation.
    /// </summary>
    public interface ILog
    {
        void Write(LogLevel level, string message);
    }

    public sealed class NullLog : ILog
    {
        public static readonly NullLog Instance = new NullLog();
        private NullLog() { }
        public void Write(LogLevel level, string message) { }
    }

    public static class LogExtensions
    {
        public static void Info(this ILog log, string message) => log.Write(LogLevel.Info, message);
        public static void Warning(this ILog log, string message) => log.Write(LogLevel.Warning, message);
        public static void Error(this ILog log, string message) => log.Write(LogLevel.Error, message);
    }
}
