using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using MqttViewer.Services;
using MqttViewer.ViewModels;

namespace MqttViewer;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;

    private Brush JsonKeyBrush => _viewModel.IsDarkMode ? CreateBrush("#93C5FD") : CreateBrush("#1E3A8A");

    private Brush JsonStringBrush => _viewModel.IsDarkMode ? CreateBrush("#86EFAC") : CreateBrush("#166534");

    private Brush JsonNumberBrush => _viewModel.IsDarkMode ? CreateBrush("#FDBA74") : CreateBrush("#C2410C");

    private Brush JsonBooleanBrush => _viewModel.IsDarkMode ? CreateBrush("#C4B5FD") : CreateBrush("#6D28D9");

    private Brush JsonNullBrush => _viewModel.IsDarkMode ? CreateBrush("#9CA3AF") : CreateBrush("#6B7280");

    private Brush JsonPunctuationBrush => _viewModel.IsDarkMode ? CreateBrush("#E5E7EB") : CreateBrush("#334155");

    private Brush JsonBracketBrush => _viewModel.IsDarkMode ? CreateBrush("#5EEAD4") : CreateBrush("#0F766E");

    private Brush JsonPlainTextBrush => _viewModel.IsDarkMode ? CreateBrush("#F9FAFB") : CreateBrush("#111827");

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainWindowViewModel(new MqttMonitorService());
        DataContext = _viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.Localizer.PropertyChanged += OnLocalizerPropertyChanged;
        RefreshLocalizedUi();
        UpdatePrettyJsonViewer();
        Closed += OnClosed;
    }

    private void PasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox passwordBox)
        {
            _viewModel.UpdatePassword(passwordBox.Password);
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.Localizer.PropertyChanged -= OnLocalizerPropertyChanged;
        _viewModel.Dispose();
    }

    private void OnLocalizerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "Item[]")
        {
            RefreshLocalizedUi();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainWindowViewModel.SelectedMessagePrettyText) or nameof(MainWindowViewModel.SelectedMessage))
        {
            UpdatePrettyJsonViewer();
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.IsDarkMode))
        {
            UpdatePrettyJsonViewer();
        }
    }

    private void UpdatePrettyJsonViewer()
    {
        var document = new FlowDocument
        {
            PagePadding = new Thickness(0)
        };

        var paragraph = new Paragraph
        {
            Margin = new Thickness(0),
            LineHeight = 18
        };

        if (_viewModel.SelectedMessage is not null)
        {
            if (_viewModel.SelectedMessage.IsJson)
            {
                try
                {
                    using var jsonDocument = JsonDocument.Parse(_viewModel.SelectedMessage.PayloadRaw);
                    AppendJsonElement(paragraph.Inlines, jsonDocument.RootElement, 0);
                }
                catch
                {
                    paragraph.Inlines.Add(new Run(_viewModel.SelectedMessagePrettyText)
                        {
                            Foreground = JsonPlainTextBrush
                        });
                }
            }
            else
            {
                paragraph.Inlines.Add(new Run(_viewModel.SelectedMessagePrettyText)
                {
                    Foreground = JsonPlainTextBrush
                });
            }
        }

        document.Blocks.Add(paragraph);
        PrettyJsonViewer.Document = document;
    }

    private void RefreshLocalizedUi()
    {
        Title = _viewModel.Localizer["AppTitle"];
        ReceivedAtColumn.Header = _viewModel.Localizer["ReceivedAt"];
        SizeColumn.Header = _viewModel.Localizer["Size"];
        RetainColumn.Header = _viewModel.Localizer["Retain"];
        PreviewColumn.Header = _viewModel.Localizer["Preview"];
    }

    private void AppendJsonElement(InlineCollection inlines, JsonElement element, int indentLevel)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                AppendObject(inlines, element, indentLevel);
                break;

            case JsonValueKind.Array:
                AppendArray(inlines, element, indentLevel);
                break;

            case JsonValueKind.String:
                inlines.Add(CreateRun($"\"{element.GetString()}\"", JsonStringBrush));
                break;

            case JsonValueKind.Number:
                inlines.Add(CreateRun(element.GetRawText(), JsonNumberBrush));
                break;

            case JsonValueKind.True:
            case JsonValueKind.False:
                inlines.Add(CreateRun(element.GetRawText(), JsonBooleanBrush));
                break;

            case JsonValueKind.Null:
                inlines.Add(CreateRun("null", JsonNullBrush, FontStyles.Italic));
                break;

            default:
                inlines.Add(CreateRun(element.GetRawText(), JsonPlainTextBrush));
                break;
        }
    }

    private void AppendObject(InlineCollection inlines, JsonElement element, int indentLevel)
    {
        var properties = element.EnumerateObject().ToList();
        inlines.Add(CreateRun("{", JsonBracketBrush));

        if (properties.Count == 0)
        {
            inlines.Add(CreateRun("}", JsonBracketBrush));
            return;
        }

        inlines.Add(new LineBreak());

        for (var index = 0; index < properties.Count; index++)
        {
            AppendIndent(inlines, indentLevel + 1);
            inlines.Add(CreateRun($"\"{properties[index].Name}\"", JsonKeyBrush));
            inlines.Add(CreateRun(": ", JsonPunctuationBrush));
            AppendJsonElement(inlines, properties[index].Value, indentLevel + 1);

            if (index < properties.Count - 1)
            {
                inlines.Add(CreateRun(",", JsonPunctuationBrush));
            }

            inlines.Add(new LineBreak());
        }

        AppendIndent(inlines, indentLevel);
        inlines.Add(CreateRun("}", JsonBracketBrush));
    }

    private void AppendArray(InlineCollection inlines, JsonElement element, int indentLevel)
    {
        var items = element.EnumerateArray().ToList();
        inlines.Add(CreateRun("[", JsonBracketBrush));

        if (items.Count == 0)
        {
            inlines.Add(CreateRun("]", JsonBracketBrush));
            return;
        }

        inlines.Add(new LineBreak());

        for (var index = 0; index < items.Count; index++)
        {
            AppendIndent(inlines, indentLevel + 1);
            AppendJsonElement(inlines, items[index], indentLevel + 1);

            if (index < items.Count - 1)
            {
                inlines.Add(CreateRun(",", JsonPunctuationBrush));
            }

            inlines.Add(new LineBreak());
        }

        AppendIndent(inlines, indentLevel);
        inlines.Add(CreateRun("]", JsonBracketBrush));
    }

    private void AppendIndent(InlineCollection inlines, int indentLevel)
    {
        inlines.Add(new Run(new string(' ', indentLevel * 2))
        {
            Foreground = JsonPlainTextBrush
        });
    }

    private static Run CreateRun(string text, Brush foreground, FontStyle? fontStyle = null)
    {
        var run = new Run(text)
        {
            Foreground = foreground
        };

        if (fontStyle.HasValue)
        {
            run.FontStyle = fontStyle.Value;
        }

        return run;
    }

    private static Brush CreateBrush(string hex)
    {
        return (SolidColorBrush)new BrushConverter().ConvertFrom(hex)!;
    }
}
