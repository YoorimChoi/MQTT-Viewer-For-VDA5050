using MQTTnet;
using MQTTnet.Formatter;
using MQTTnet.Protocol;
using MqttViewer.Models;

namespace MqttViewer.Services;

public sealed class MqttMonitorService : IMqttMonitorService
{
    private readonly IMqttClient _client;
    private readonly SemaphoreSlim _connectionGate = new(1, 1);
    private volatile ConnectionStatus _status = ConnectionStatus.Disconnected;

    public MqttMonitorService()
    {
        _client = new MqttClientFactory().CreateMqttClient();

        _client.ConnectedAsync += _ =>
        {
            SetStatus(ConnectionStatus.Connected);
            return Task.CompletedTask;
        };

        _client.DisconnectedAsync += _ =>
        {
            if (_status != ConnectionStatus.Error)
            {
                SetStatus(ConnectionStatus.Disconnected);
            }

            return Task.CompletedTask;
        };

        _client.ApplicationMessageReceivedAsync += e =>
        {
            var applicationMessage = e.ApplicationMessage;
            if (applicationMessage is null)
            {
                return Task.CompletedTask;
            }

            var payloadText = applicationMessage.ConvertPayloadToString();

            var incomingMessage = new IncomingMqttMessage
            {
                Topic = applicationMessage.Topic,
                Payload = payloadText,
                Qos = (int)applicationMessage.QualityOfServiceLevel,
                Retain = applicationMessage.Retain,
                ReceivedAt = DateTime.Now
            };

            MessageReceived?.Invoke(this, incomingMessage);
            return Task.CompletedTask;
        };
    }

    public event EventHandler<ConnectionStatus>? ConnectionStatusChanged;

    public event EventHandler<string>? ConnectionErrorOccurred;

    public event EventHandler<IncomingMqttMessage>? MessageReceived;

    public bool IsConnected => _client.IsConnected;

    public async Task ConnectAsync(ConnectionSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await _connectionGate.WaitAsync(cancellationToken);
        try
        {
            if (_client.IsConnected)
            {
                await _client.DisconnectAsync(cancellationToken: cancellationToken);
            }

            SetStatus(ConnectionStatus.Connecting);

            var optionsBuilder = new MqttClientOptionsBuilder()
                .WithClientId(string.IsNullOrWhiteSpace(settings.ClientId) ? Guid.NewGuid().ToString("N") : settings.ClientId)
                .WithTcpServer(settings.Host, settings.Port)
                .WithProtocolVersion(MqttProtocolVersion.V311)
                .WithCleanSession();

            if (!string.IsNullOrWhiteSpace(settings.Username))
            {
                optionsBuilder = optionsBuilder.WithCredentials(settings.Username, settings.Password);
            }

            await _client.ConnectAsync(optionsBuilder.Build(), cancellationToken);

            if (!string.IsNullOrWhiteSpace(settings.TopicFilter))
            {
                var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
                    .WithTopicFilter(settings.TopicFilter, MqttQualityOfServiceLevel.AtMostOnce)
                    .Build();

                await _client.SubscribeAsync(subscribeOptions, cancellationToken);
            }

            SetStatus(ConnectionStatus.Connected);
        }
        catch (Exception ex)
        {
            SetStatus(ConnectionStatus.Error);
            ConnectionErrorOccurred?.Invoke(this, ex.Message);
            throw;
        }
        finally
        {
            _connectionGate.Release();
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await _connectionGate.WaitAsync(cancellationToken);
        try
        {
            if (!_client.IsConnected)
            {
                SetStatus(ConnectionStatus.Disconnected);
                return;
            }

            await _client.DisconnectAsync(cancellationToken: cancellationToken);
            SetStatus(ConnectionStatus.Disconnected);
        }
        catch (Exception ex)
        {
            SetStatus(ConnectionStatus.Error);
            ConnectionErrorOccurred?.Invoke(this, ex.Message);
            throw;
        }
        finally
        {
            _connectionGate.Release();
        }
    }

    private void SetStatus(ConnectionStatus status)
    {
        _status = status;
        ConnectionStatusChanged?.Invoke(this, status);
    }

    public void Dispose()
    {
        _client?.Dispose();
        _connectionGate.Dispose();
    }
}
