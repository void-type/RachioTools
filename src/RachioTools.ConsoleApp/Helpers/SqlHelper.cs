using RachioTools.Api.Models;
using System.Data.SqlClient;

namespace RachioTools.ConsoleApp.Helpers;

public static class SqlHelper
{
    private const string EventTableName = "RachioDeviceEvent";

    public static async Task SaveEvents(List<RachioDeviceEvent> events, string connectionString, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        // Create table if it doesn't exist
        var createTableCommand = new SqlCommand($@"
            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='{EventTableName}' AND xtype='U')
            BEGIN
                CREATE TABLE [dbo].[{EventTableName}](
                    [Id] [nvarchar](50) NOT NULL,
                    [DeviceId] [nvarchar](50) NOT NULL,
                    [Category] [nvarchar](500) NULL,
                    [Type] [nvarchar](500) NULL,
                    [EventDate] [bigint] NOT NULL,
                    [Summary] [nvarchar](500) NOT NULL,
                    [SubType] [nvarchar](500) NULL,
                    [Hidden] [bit] NOT NULL,
                    [Topic] [nvarchar](500) NULL,
                    CONSTRAINT [PK_{EventTableName}] PRIMARY KEY CLUSTERED
                    (
                        [Id] ASC
                    )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
                ) ON [PRIMARY]
            END", connection);

        await createTableCommand.ExecuteNonQueryAsync(cancellationToken);

        // Insert events that don't exist
        foreach (var eventItem in events.OrderBy(e => e.EventDate))
        {
            var insertCommand = new SqlCommand($@"
				IF NOT EXISTS (SELECT 1 FROM [dbo].[{EventTableName}] WHERE [Id] = @Id)
				BEGIN
					INSERT INTO [dbo].[{EventTableName}]
					([Id], [DeviceId], [Category], [Type], [EventDate], [Summary], [SubType], [Hidden], [Topic])
					VALUES
					(@Id, @DeviceId, @Category, @Type, @EventDate, @Summary, @SubType, @Hidden, @Topic)
				END", connection);

            var parameters = insertCommand.Parameters;

            parameters.AddWithValue("@Id", eventItem.Id);
            parameters.AddWithValue("@DeviceId", eventItem.DeviceId);
            parameters.AddWithValue("@Category", (object?)eventItem.Category ?? DBNull.Value);
            parameters.AddWithValue("@Type", (object?)eventItem.Type ?? DBNull.Value);
            parameters.AddWithValue("@EventDate", eventItem.EventDate);
            parameters.AddWithValue("@Summary", eventItem.Summary);
            parameters.AddWithValue("@SubType", (object?)eventItem.SubType ?? DBNull.Value);
            parameters.AddWithValue("@Hidden", eventItem.Hidden);
            parameters.AddWithValue("@Topic", (object?)eventItem.Topic ?? DBNull.Value);

            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
