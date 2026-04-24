using MqttViewer.Infrastructure;

namespace MqttViewer.Models;

public sealed class ConnectionSettings : ObservableObject
{
    private string _host = "127.0.0.1";
    private int _port = 1883;
    private string _clientId = string.Empty;
    private string? _username;
    private string? _password;
    private string _topicFilter = "#";

    public string Host
    {
        get => _host;
        set => SetProperty(ref _host, value);
    }

    public int Port
    {
        get => _port;
        set => SetProperty(ref _port, value);
    }

    public string ClientId
    {
        get => _clientId;
        set => SetProperty(ref _clientId, value);
    }

    public string? Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    public string? Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public string TopicFilter
    {
        get => _topicFilter;
        set => SetProperty(ref _topicFilter, value);
    }

    public string ProtocolVersion => "MQTT 3.1.1";
}
