namespace BTSS.Installer;

internal sealed class InstallWorkflow
{
    private readonly ServerRegistrationClient _registrationClient = new();

    public async Task<InstallResult> ExecuteAsync(InstallPlan plan, IReadOnlyList<DisplayInfoModel> displays, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        var result = new InstallResult();
        Directory.CreateDirectory(plan.InstallRoot);

        if (plan.InstallDisplayAdmin)
        {
            progress?.Report("Copying Display/Admin files...");
            var source = PayloadLocator.ResolvePayloadFolder(plan.PayloadRoot, "BTSS.IAR");
            PayloadLocator.CopyDirectory(source, plan.DisplayInstallDir);
            ConfigWriters.WriteDisplayPreferences(plan);
            result.Messages.Add($"Display/Admin installed to {plan.DisplayInstallDir}");
        }

        if (plan.InstallKiosk)
        {
            progress?.Report("Copying Kiosk files...");
            var source = PayloadLocator.ResolvePayloadFolder(plan.PayloadRoot, "BTSS.IAR.Kiosk");
            PayloadLocator.CopyDirectory(source, plan.KioskInstallDir);
            ConfigWriters.WriteKioskCompatibility(plan);
            result.Messages.Add($"Kiosk installed to {plan.KioskInstallDir}");
        }

        if (plan.InstallService)
        {
            progress?.Report("Copying Service files...");
            var source = PayloadLocator.ResolvePayloadFolder(plan.PayloadRoot, "BTSS.Service");
            PayloadLocator.CopyDirectory(source, plan.ServiceInstallDir);
            ConfigWriters.WriteServiceSettings(plan);

            var serviceExe = Directory.GetFiles(plan.ServiceInstallDir, "BTSS.Service.exe", SearchOption.AllDirectories).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(serviceExe))
                throw new FileNotFoundException("BTSS.Service.exe was not found after copying payload files.");

            progress?.Report("Installing Windows service as LocalSystem...");
            ServiceControl.InstallOrUpdate(serviceExe);
            result.Messages.Add("Windows service installed/updated as LocalSystem.");
        }

        progress?.Report("Registering device and detected displays with server...");
        try
        {
            await _registrationClient.RegisterDeviceAsync(plan, displays, cancellationToken);
            result.Messages.Add("Device registration posted to server.");
        }
        catch (Exception ex)
        {
            result.Warnings.Add("Server registration failed: " + ex.Message);
        }

        return result;
    }
}
