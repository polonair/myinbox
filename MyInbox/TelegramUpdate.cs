namespace MyInbox
{
    public class TelegramUpdate
    {
        public long update_id { get; set; }
        public TelegramMessage message { get; set; }
    }
}