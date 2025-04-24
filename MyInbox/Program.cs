namespace MyInbox
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.AddJsonFile("setings.json", true);
                })
                .ConfigureServices((context, services) =>
                {
                    services.AddHostedService<Worker>();
                    services.AddSingleton<TelegramService>();
                    services.AddSingleton<TimeTrackingService>();
                })
                .Build()
                .Run();
        }
    }
}