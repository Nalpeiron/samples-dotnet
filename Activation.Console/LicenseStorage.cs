using Zentitle.Licensing.Client;
using Zentitle.Licensing.Client.Persistence.Storage;

namespace Activation.Console;

public static class LicenseStorage
{
    private const string AppDirectory = "Z2_OnlineActivation_Console";
    
    public static IActivationStorage Create(bool useCoreLibrary)
    {
        IActivationStorage storage;
        if (useCoreLibrary)
        {
            storage = Zentitle2Core.SecureActivationStorage.WithAppDirectory(
                PredefinedSystemFolder.UserData, AppDirectory, "license.encrypted");
        }
        else
        {
            storage = new PlainTextFileActivationStorage(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppDirectory,
                    "license.json"));
        }
        
        DisplayHelper.WriteWarning($"- Using {storage.GetType().Name} storage with file: {storage.StorageId}");

        return storage;
    }
}
