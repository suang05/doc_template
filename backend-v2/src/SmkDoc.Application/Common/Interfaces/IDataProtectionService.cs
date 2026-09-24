namespace SmkDoc.Application.Common.Interfaces;

public interface IDataProtectionService
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
}
