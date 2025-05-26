using Sharprompt;
using Zentitle.Licensing.Client;
using Zentitle.Licensing.Client.Persistence.Storage;

namespace SharedActivation.Console;

public static class LicenseStorage
{
    private const string AppDirectory = "Z2_SharedActivation_Console";

    public static async Task<IActivationStorage> Initialize()
    {
        IActivationStorage storage = new PlainTextFileActivationStorage(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppDirectory,
                "license.json"));

        DisplayHelper.WriteWarning($"- Using {storage.GetType().Name} storage with file: {storage.StorageId}");

        var data = await storage.Load();
        if (data.IsEmpty())
        {
            return storage;
        }

        var deleteExistingFiles = Prompt.Confirm("Do you want to delete existing activation data in the persistent storage?");
        if (deleteExistingFiles)
        {
            System.Console.WriteLine("Deleting already persisted activation data...");
            await storage.Clear();
        }

        return storage;
    }
}