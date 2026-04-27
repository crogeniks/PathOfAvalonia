using PathOfAvalonia.TreeDomain.Import;

namespace PathOfAvalonia.TreeApp.Services;

public interface IImportService
{
    ImportedBuild Import(string text);
}

public sealed class ImportService : IImportService
{
    public ImportedBuild Import(string text) => BuildImporter.Import(text);
}
