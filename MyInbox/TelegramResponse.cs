namespace MyInbox
{
    public class TelegramResponse
    {
        public bool ok { get; set; }
        public List<TelegramUpdate> result { get; set; }
    }
}