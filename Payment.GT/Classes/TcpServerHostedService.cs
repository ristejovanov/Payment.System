using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Payment.GT.Classes.Interface;
using System.Net.Sockets;

namespace Payment.GT.Classes
{


    public sealed class TcpServerHostedService : BackgroundService
    {
        private readonly IServiceProvider _sp;
        private readonly ILogger<TcpServerHostedService> _log;
        private readonly int _port;
        private TcpListener? _listener;

        public TcpServerHostedService(IConfiguration cfg, IServiceProvider sp, ILogger<TcpServerHostedService> log)
        {
            _sp = sp;
            _log = log;
            _port = cfg.GetValue("TcpPort", 9000);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _listener = new TcpListener(System.Net.IPAddress.Any, _port);
            _listener.Start();
            _log.LogInformation("GT Gateway listening on port {Port}", _port);

            while (!stoppingToken.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                _ = Task.Run(async () =>
                {
                    using var scope = _sp.CreateScope();
                    var processor = scope.ServiceProvider.GetRequiredService<IGatewayProcessor>();
                    var connectionHandler = scope.ServiceProvider.GetRequiredService<IConnectionHandler>();

                    await connectionHandler.RunAsync(stoppingToken);
                }, stoppingToken);
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            try { _listener?.Stop(); } catch { }
            return base.StopAsync(cancellationToken);
        }
    }

}
