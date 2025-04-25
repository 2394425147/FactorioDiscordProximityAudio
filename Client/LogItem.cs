namespace Client
{
    public sealed class LogItem(string message, LogItem.LogType type)
    {
        public string   message = message;
        public LogType  type    = type;
        public DateTime time    = DateTime.Now;

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
