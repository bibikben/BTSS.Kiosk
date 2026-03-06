using BTSS.IAR.Kiosk.DispatchEmail.Printing;
using BTSS.IAR.Kiosk.DispatchEmail.Reporting;
using BTSS.IAR.Kiosk.Services;
using BTSS.IAR.Kiosk.Services.DispatchEmail;
using BTSS.IAR.Kiosk.Services.IarApi;

namespace BTSS.IAR.Kiosk;

public partial class CallsPage : ContentPage
{
    private readonly IIarApiClient _api;
    private readonly IIarPivotReportBuilder _pivot;
    private readonly IPrintService _printer;

    public CallsPage(IIarApiClient api, IIarPivotReportBuilder pivot, IPrintService printer)
    {
        InitializeComponent();
        _api = api;
        _pivot = pivot;
        _printer = printer;

        // Default: today
        StartDatePicker.Date = DateTime.Today;
        EndDatePicker.Date = DateTime.Today;
    }

    private async void OnSearchClicked(object sender, EventArgs e)
    {
        try
        {
            var agencyId = AppSettings.IarApiAgencyId;
            if (agencyId <= 0)
            {
                await DisplayAlert("Calls", "Please set a numeric Agency Identifier in Admin → Email → IAR API.", "OK");
                return;
            }

            var start = StartDatePicker.Date ?? DateTime.Now;
            var end = EndDatePicker.Date?.AddDays(1).AddSeconds(-1) ?? DateTime.Now.AddDays(1).AddSeconds(-1);

            var list = await _api.GetListOfCallsAsync(agencyId, start, end, CallTypeEntry.Text, CancellationToken.None);
            CallsView.ItemsSource = list.OrderByDescending(x => x.UpdatedAt ?? DateTime.MinValue).ToList();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Calls", ex.Message, "OK");
        }
    }

    private async void OnPrintClicked(object sender, EventArgs e)
    {
        try
        {
            if (sender is not Button b) return;
            var callId = b.CommandParameter as string;
            if (string.IsNullOrWhiteSpace(callId)) return;

            var agencyId = AppSettings.IarApiAgencyId;
            if (agencyId <= 0)
            {
                await DisplayAlert("Print", "Please set a numeric Agency Identifier in Admin.", "OK");
                return;
            }

            var record = await _api.GetCallRecordAsync(agencyId, callId, CancellationToken.None);
            if (record == null)
            {
                await DisplayAlert("Print", "Call record not found.", "OK");
                return;
            }

            PivotTableResult pivot = _pivot.Build(record);
            var updated = record.Details?.UpdatedAt ?? record.Details?.UpdatedAtISO;
            var title = $"IAR Call  {(updated?.ToLocalTime().ToString("MM-dd-yyyy HH:mm:ss") ?? "")}  ID: {record.Details?.Id}";
            var footer = $"Type: {record.Headers?.Type}  Address: {record.Headers?.Address}";
            await _printer.PrintAsync(title, pivot, footer);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Print", ex.Message, "OK");
        }
    }
}
