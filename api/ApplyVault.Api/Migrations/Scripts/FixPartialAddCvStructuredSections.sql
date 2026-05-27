-- Run this only if a previous failed migration left UserCvSections without UserCvEntries.
-- Then run: Update-Database (or dotnet ef database update)

IF OBJECT_ID(N'[dbo].[UserCvEntries]', N'U') IS NOT NULL
    DROP TABLE [dbo].[UserCvEntries];

IF OBJECT_ID(N'[dbo].[UserCvSections]', N'U') IS NOT NULL
    DROP TABLE [dbo].[UserCvSections];

IF COL_LENGTH('dbo.UserCvDocuments', 'StructuredImportedAt') IS NOT NULL
    ALTER TABLE [dbo].[UserCvDocuments] DROP COLUMN [StructuredImportedAt];

DELETE FROM [__EFMigrationsHistory]
WHERE [MigrationId] = N'20260527143935_AddCvStructuredSections';
