namespace BTSS.Installer;

internal static class PayloadLocator
{
    public static string ResolvePayloadFolder(string payloadRoot, string componentFolderName)
    {
        var direct = Path.Combine(payloadRoot, componentFolderName);
        if (Directory.Exists(direct))
            return direct;

        var published = Path.Combine(payloadRoot, "payload", componentFolderName);
        if (Directory.Exists(published))
            return published;

        throw new DirectoryNotFoundException($"Payload folder not found for '{componentFolderName}'. Expected '{direct}' or '{published}'.");
    }

    public static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (var directory in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDir, directory);
            Directory.CreateDirectory(Path.Combine(destinationDir, relative));
        }

        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDir, file);
            var destination = Path.Combine(destinationDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, true);
        }
    }
}
