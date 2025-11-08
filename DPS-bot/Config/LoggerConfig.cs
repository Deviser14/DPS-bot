public class LoggerConfig
{
    public string LogDirectory { get; set; } = "Data/Logs";
    public string MinimumLevel { get; set; } = "Debug";
    public bool WriteToConsole { get; set; } = true;

}