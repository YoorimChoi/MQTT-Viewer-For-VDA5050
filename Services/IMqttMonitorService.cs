using MqttViewer.Models;

namespace MqttViewer.Services;

public interface IMqttMonitorService : IDisposable
{
    event EventHandler<ConnectionStatus>? ConnectionStatusChanged;

    event EventHandler<string>? ConnectionErrorOccurred;

    event EventHandler<IncomingMqttMessage>? MessageReceived;

    bool IsConnected { get; }

    Task ConnectAsync(ConnectionSettings settings, CancellationToken cancellationToken = default);

    Task DisconnectAsync(CancellationToken cancellationToken = default);
}
