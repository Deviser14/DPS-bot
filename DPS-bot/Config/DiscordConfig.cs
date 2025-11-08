public class DiscordConfig
{
    public string Token { get; set; } = string.Empty;
    public string CommandPrefix { get; set; } = "!";
    public ulong GuildId { get; set; }
    public ulong ChannelId { get; set; }
}
