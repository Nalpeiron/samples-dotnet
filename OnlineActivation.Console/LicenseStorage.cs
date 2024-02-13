using System.Reflection;
using Sharprompt;
using Zentitle.Licensing.Client.Persistence.Storage;

namespace OnlineActivation.Console;

public static class LicenseStorage
{
    public static IActivationStorage Initialize()
    {
        var storageFile = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "licenseData.json");
        System.Console.WriteLine($"Using license storage file: {storageFile}");
        if (File.Exists(storageFile))
        {
            var deleteExistingFiles = Prompt.Confirm("Do you want to delete existing activation data in the persistent storage?");
            if (deleteExistingFiles)
            {
                System.Console.WriteLine("Deleting already persisted activation data...");
                File.Delete(storageFile);
            }
        }

        return new FileActivationStorage(storageFile);
    }
}