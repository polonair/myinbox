namespace MyInbox
{
    public class TelegramUpdate
    {
        public long update_id { get; set; }
        public TelegramMessage message { get; set; }
        public TelegramCallbackQuery callback_query { get; set; }
    }
    public class TelegramCallbackQuery
    {
        public string id { get; set; }
        public TelegramUser from { get; set; }
        public TelegramMessage message { get; set; }
        public string inline_message_id { get; set; }
        public string chat_instance { get; set; }
        public string data { get; set; }
        public string game_short_name { get; set; }
    }
}