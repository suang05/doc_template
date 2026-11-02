using SmkDoc.Domain.Common;
using SmkDoc.Domain.Enums;

namespace SmkDoc.Application.Modules.Integration.DataConnections.Helpers;

internal static class DataConnectionValidatorRules
{
    public static bool BeAValidDatabaseProvider(string provider)
    {
        return !string.IsNullOrWhiteSpace(provider) &&
               Enumeration.GetAll<DatabaseProvider>().Any(p => p.Name.Equals(provider, StringComparison.OrdinalIgnoreCase));
    }
}
