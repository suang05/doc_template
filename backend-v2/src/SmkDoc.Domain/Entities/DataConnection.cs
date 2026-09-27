namespace SmkDoc.Domain.Entities;

public class DataConnection : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string Provider { get; private set; } = "PostgreSQL"; // e.g. PostgreSQL, SqlServer, MySQL
    public string EncryptedConnectionString { get; private set; } = string.Empty;

    private DataConnection() { }

    public DataConnection(string name, string provider, string encryptedConnectionString)
    {
        Name = name;
        Provider = provider;
        EncryptedConnectionString = encryptedConnectionString;
    }

    public void UpdateConnection(string name, string provider, string encryptedConnectionString)
    {
        Name = name;
        Provider = provider;
        EncryptedConnectionString = encryptedConnectionString;
        SetUpdated();
    }
}
