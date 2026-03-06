using BTSS.IAR.Kiosk.Services;

namespace BTSS.IAR.Kiosk;

public partial class KorzhReportsPage : ContentPage
{
    public KorzhReportsPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        var baseUrl = (AppSettings.IarApiBaseUrl ?? "http://localhost:5080").TrimEnd('/');
        var url = $"{baseUrl}/korzh/reports";
        UrlLabel.Text = url;
        Web.Source = url;
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        if (Navigation.NavigationStack.Count > 1)
            await Navigation.PopAsync();
    }
}
