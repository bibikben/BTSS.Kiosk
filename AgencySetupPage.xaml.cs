using System.Collections.ObjectModel;
using System.Globalization;
using BTSS.IAR.Kiosk.Services.IarApi;

namespace BTSS.IAR.Kiosk;

public partial class AgencySetupPage : ContentPage
{
    private readonly IIarApiClient _api;
    private readonly ObservableCollection<AgencyVm> _items = new();

    public AgencySetupPage(IIarApiClient api)
    {
        InitializeComponent();
        _api = api;
        AgenciesView.ItemsSource = _items;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAsync();
    }

    private async void OnRefreshClicked(object sender, EventArgs e)
    {
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        try
        {
            _items.Clear();
            var agencies = await _api.GetAgenciesAsync(CancellationToken.None);
            foreach (var a in agencies)
            {
                _items.Add(new AgencyVm
                {
                    Id = a.Id,
                    Name = a.Name ?? "",
                    StationLatitude = a.StationLatitude?.ToString("0.######", CultureInfo.InvariantCulture) ?? "",
                    StationLongitude = a.StationLongitude?.ToString("0.######", CultureInfo.InvariantCulture) ?? ""
                });
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        try
        {
            int ok = 0, fail = 0;

            foreach (var vm in _items)
            {
                double? lat = TryParseDouble(vm.StationLatitude);
                double? lng = TryParseDouble(vm.StationLongitude);

                var dto = new AgencyDto(vm.Id, vm.Name, lat, lng);
                var success = await _api.UpsertAgencyAsync(dto, CancellationToken.None);
                if (success) ok++; else fail++;
            }

            await DisplayAlert("Saved", $"Updated: {ok}\nFailed: {fail}", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private static double? TryParseDouble(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return v;
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out v)) return v;
        return null;
    }

    public sealed class AgencyVm
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string StationLatitude { get; set; } = "";
        public string StationLongitude { get; set; } = "";
    }
}
