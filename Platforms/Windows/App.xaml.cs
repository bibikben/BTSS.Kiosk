using Microsoft.Maui;
using Microsoft.Maui.Hosting;

namespace BTSS.IAR.Kiosk.WinUI;

public partial class App : MauiWinUIApplication
{
    public App()
    {
        InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}