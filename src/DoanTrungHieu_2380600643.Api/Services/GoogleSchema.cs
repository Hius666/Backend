using DoanTrungHieu_2380600643.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace DoanTrungHieu_2380600643.Api.Services;
public static class GoogleSchema
{
    // Existing EnsureCreated databases need an additive upgrade; no existing column or row is changed.
    public static async Task Upgrade(AttendanceDb db)
    {
        if (!db.Database.IsSqlServer()) return;
        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[dbo].[ExternalIdentities]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[ExternalIdentities] (
                    [Provider] nvarchar(30) NOT NULL,
                    [Subject] nvarchar(255) NOT NULL,
                    [AccountId] uniqueidentifier NOT NULL,
                    CONSTRAINT [PK_ExternalIdentities] PRIMARY KEY ([Provider], [Subject]),
                    CONSTRAINT [FK_ExternalIdentities_Accounts_AccountId] FOREIGN KEY ([AccountId])
                        REFERENCES [dbo].[Accounts] ([Id]) ON DELETE CASCADE
                );
                CREATE UNIQUE INDEX [IX_ExternalIdentities_AccountId_Provider]
                    ON [dbo].[ExternalIdentities] ([AccountId], [Provider]);
            END
            """);
    }
}
