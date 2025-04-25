namespace Client
{
    public sealed class LogItem(string message, LogItem.LogType type, string? details = null)
    {
        public readonly string   message = message;
        public readonly LogType  type    = type;
        public          DateTime time    = DateTime.Now;
        public readonly string?  details = details;

        public enum LogType
        {
            Info,
            Warning,
            Error,
        }

        public static Color GetColor(LogType logType)
        {
            return logType switch
            {
                LogType.Info    => Color.White,
                LogType.Warning => Color.Orange,
                LogType.Error   => Color.Red,
                _               => Color.Gray
            };
        }
    }
}
