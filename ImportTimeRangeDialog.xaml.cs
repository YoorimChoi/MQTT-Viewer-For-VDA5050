using System.Globalization;
using System.Windows;
using MqttViewer.Services;

namespace MqttViewer;

public partial class ImportTimeRangeDialog : Window
{
    private const string DisplayFormat = "yyyy-MM-dd HH:mm:ss";

    private readonly DateTime _min;
    private readonly DateTime _max;

    public ImportTimeRangeDialog(DateTime min, DateTime max, int totalCount)
    {
        InitializeComponent();
        DataContext = AppLocalizer.Instance;

        _min = min;
        _max = max;

        AvailableText.Text = AppLocalizer.Instance.Format(
            "ImportRangeAvailable",
            min.ToString(DisplayFormat, CultureInfo.CurrentCulture),
            max.ToString(DisplayFormat, CultureInfo.CurrentCulture),
            totalCount);

        ResetToFullRange();
    }

    /// <summary>Inclusive lower bound chosen by the user (valid only when DialogResult is true).</summary>
    public DateTime RangeStart { get; private set; }

    /// <summary>Inclusive upper bound chosen by the user (valid only when DialogResult is true).</summary>
    public DateTime RangeEnd { get; private set; }

    private void ResetToFullRange()
    {
        StartDatePicker.SelectedDate = _min.Date;
        StartTimePicker.SelectedTime = _min;
        EndDatePicker.SelectedDate = _max.Date;
        EndTimePicker.SelectedTime = _max;
        ErrorText.Visibility = Visibility.Collapsed;
    }

    private void FullRangeButton_OnClick(object sender, RoutedEventArgs e)
    {
        ResetToFullRange();
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void OkButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (!TryCombine(StartDatePicker.SelectedDate, StartTimePicker.SelectedTime, out var start) ||
            !TryCombine(EndDatePicker.SelectedDate, EndTimePicker.SelectedTime, out var end))
        {
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        // Minute precision (no seconds input). Start snaps to the beginning of its
        // minute; end extends to the very end of its minute so that picking the same
        // minute as a message still includes it.
        var startMinute = TruncateToMinute(start);
        var endMinute = TruncateToMinute(end);
        if (endMinute < startMinute)
        {
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        RangeStart = startMinute;
        RangeEnd = endMinute.AddMinutes(1).AddTicks(-1);
        DialogResult = true;
    }

    private static bool TryCombine(DateTime? date, DateTime? time, out DateTime value)
    {
        value = default;
        if (date is null || time is null)
        {
            return false;
        }

        // Date from the calendar, time-of-day from the clock picker.
        value = date.Value.Date + time.Value.TimeOfDay;
        return true;
    }

    private static DateTime TruncateToMinute(DateTime value)
    {
        return value.AddTicks(-(value.Ticks % TimeSpan.TicksPerMinute));
    }
}
