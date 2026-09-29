using Game.Core.Logging;

namespace Game.App
{
    /// <summary><see cref="ILog"/> backed by UnityEngine.Debug.</summary>
    public sealed class UnityLog : ILog
    {
        public void Write(LogLevel level, string message)
        {
            switch (level)
            {
                case LogLevel.Error: UnityEngine.Debug.LogError(message); break;
                case LogLevel.Warning: UnityEngine.Debug.LogWarning(message); break;
                default: UnityEngine.Debug.Log(message); break;
            }
        }
    }
}
