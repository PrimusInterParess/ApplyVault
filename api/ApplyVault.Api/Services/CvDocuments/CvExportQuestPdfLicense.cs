using System.Runtime.CompilerServices;
using QuestPDF.Infrastructure;

namespace ApplyVault.Api.Services;

internal static class CvExportQuestPdfLicense
{
    [ModuleInitializer]
    internal static void Register()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }
}
