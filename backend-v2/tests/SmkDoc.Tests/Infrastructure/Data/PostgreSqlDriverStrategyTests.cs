using System.Data;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using SmkDoc.Infrastructure.Data.Drivers;
using Xunit;

namespace SmkDoc.Tests.Infrastructure.Data;

public sealed class PostgreSqlDriverStrategyTests
{
    [Fact]
    public void ProviderName_ShouldBePostgreSQL()
    {
        // Arrange
        var strategy = new PostgreSqlDriverStrategy();

        // Assert
        strategy.ProviderName.Should().Be("PostgreSQL");
    }

    [Fact]
    public async Task SerializeReaderToJsonStreamAsync_WhenEmptyReader_OutputsEmptyJsonArray()
    {
        // Arrange
        using var table = new DataTable();
        table.Columns.Add("id", typeof(int));
        using var reader = table.CreateDataReader();

        using var destination = new MemoryStream();

        // Act
        await PostgreSqlDriverStrategy.SerializeReaderToJsonStreamAsync(reader, destination, CancellationToken.None);

        // Assert
        var json = Encoding.UTF8.GetString(destination.ToArray());
        json.Should().Be("[]");
    }

    [Fact]
    public async Task SerializeReaderToJsonStreamAsync_SerializesAllSupportedTypesAndNullsCorrectly()
    {
        // Arrange
        using var table = new DataTable();
        table.Columns.Add("bool_val", typeof(bool));
        table.Columns.Add("byte_val", typeof(byte));
        table.Columns.Add("short_val", typeof(short));
        table.Columns.Add("int_val", typeof(int));
        table.Columns.Add("long_val", typeof(long));
        table.Columns.Add("float_val", typeof(float));
        table.Columns.Add("double_val", typeof(double));
        table.Columns.Add("decimal_val", typeof(decimal));
        table.Columns.Add("string_val", typeof(string));
        var dtCol = table.Columns.Add("datetime_val", typeof(DateTime));
        dtCol.DateTimeMode = DataSetDateTime.Utc;
        table.Columns.Add("guid_val", typeof(Guid));
        table.Columns.Add("bytes_val", typeof(byte[]));
        table.Columns.Add("null_val", typeof(string));

        var fixedGuid = Guid.Parse("a0000000-0000-0000-0000-000000000001");
        var fixedDate = new DateTime(2026, 11, 10, 8, 30, 0, DateTimeKind.Utc);
        byte[] rawBytes = [1, 2, 3, 4];

        table.Rows.Add(
            true,
            (byte)255,
            (short)1000,
            42,
            9876543210L,
            12.5f,
            34.75,
            199.99m,
            "SmkDoc Gateway",
            fixedDate,
            fixedGuid,
            rawBytes,
            DBNull.Value);

        using var reader = table.CreateDataReader();
        using var destination = new MemoryStream();

        // Act
        await PostgreSqlDriverStrategy.SerializeReaderToJsonStreamAsync(reader, destination, CancellationToken.None);

        // Assert
        var json = Encoding.UTF8.GetString(destination.ToArray());
        json.Should().StartWith("[{").And.EndWith("}]");

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        root.ValueKind.Should().Be(JsonValueKind.Array);
        root.GetArrayLength().Should().Be(1);

        var row = root[0];
        row.GetProperty("bool_val").GetBoolean().Should().BeTrue();
        row.GetProperty("byte_val").GetInt32().Should().Be(255);
        row.GetProperty("short_val").GetInt32().Should().Be(1000);
        row.GetProperty("int_val").GetInt32().Should().Be(42);
        row.GetProperty("long_val").GetInt64().Should().Be(9876543210L);
        row.GetProperty("float_val").GetDouble().Should().BeApproximately(12.5, 0.001);
        row.GetProperty("double_val").GetDouble().Should().Be(34.75);
        row.GetProperty("decimal_val").GetDecimal().Should().Be(199.99m);
        row.GetProperty("string_val").GetString().Should().Be("SmkDoc Gateway");
        row.GetProperty("datetime_val").GetString().Should().Be(fixedDate.ToString("O"));
        row.GetProperty("guid_val").GetGuid().Should().Be(fixedGuid);
        row.GetProperty("bytes_val").GetBytesFromBase64().Should().Equal(rawBytes);
        row.GetProperty("null_val").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task SerializeReaderToJsonStreamAsync_HandlesMultipleRowsDeterministically()
    {
        // Arrange
        using var table = new DataTable();
        table.Columns.Add("id", typeof(int));
        table.Columns.Add("label", typeof(string));

        table.Rows.Add(1, "Alpha");
        table.Rows.Add(2, "Beta");
        table.Rows.Add(3, "Gamma");

        using var reader = table.CreateDataReader();
        using var destination = new MemoryStream();

        // Act
        await PostgreSqlDriverStrategy.SerializeReaderToJsonStreamAsync(reader, destination, CancellationToken.None);

        // Assert
        var json = Encoding.UTF8.GetString(destination.ToArray());
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        root.GetArrayLength().Should().Be(3);
        root[0].GetProperty("label").GetString().Should().Be("Alpha");
        root[1].GetProperty("label").GetString().Should().Be("Beta");
        root[2].GetProperty("label").GetString().Should().Be("Gamma");
    }
}

