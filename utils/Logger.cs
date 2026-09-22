namespace SurveillanceAI.Utils;

public static class Logger
{
    private static readonly object _lock = new();

    private static void Write(string message, ConsoleColor color, string level)
    {
        lock (_lock)
        {
            var time = DateTime.Now.ToString("HH:mm:ss");

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($"[{time}] ");

            Console.ForegroundColor = color;
            Console.Write($"[{level}] ");

            Console.ResetColor();
            Console.WriteLine(message);
        }
    }

    public static void Info(string message)
        => Write(message, ConsoleColor.Cyan, "INFO");

    public static void Warn(string message)
        => Write(message, ConsoleColor.Yellow, "WARN");

    public static void Error(string message)
        => Write(message, ConsoleColor.Red, "ERROR");

    public static void Success(string message)
        => Write(message, ConsoleColor.Green, "OK");

    public static void Alert(string message)
        => Write(message, ConsoleColor.Magenta, "ALERT");
}