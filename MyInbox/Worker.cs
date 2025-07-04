namespace MyInbox
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly TelegramService _telegram;
        private readonly TimeTrackingService _tracker;

        public Worker(ILogger<Worker> logger, TelegramService telegram, TimeTrackingService tracker)
        {
            _logger = logger;
            _telegram = telegram;
            _tracker = tracker;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                }
                await Task.Delay(1000, stoppingToken);
                await _telegram.StartAsync(stoppingToken);
                await _tracker.StartAsync(stoppingToken);
            }
        }
    }
}
