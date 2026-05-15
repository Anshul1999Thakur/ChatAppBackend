using System.Security.Cryptography;
using System.Text;

namespace ChatApp.Helpers
{
    public class EncryptionHelper
    {
        private readonly IConfiguration _config;

        public EncryptionHelper(IConfiguration config)
        {
            _config = config;
        }

        public string Encrypt(string plainText)
        {
            var key = Encoding.UTF8.GetBytes(_config["Encryption:Key"]);
            var iv = Encoding.UTF8.GetBytes(_config["Encryption:IV"]);

            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;

            var encryptor = aes.CreateEncryptor();

            var bytes = Encoding.UTF8.GetBytes(plainText);

            var encrypted =
                encryptor.TransformFinalBlock(bytes, 0, bytes.Length);

            return Convert.ToBase64String(encrypted);
        }

        public string Decrypt(string cipherText)
        {
            var key = Encoding.UTF8.GetBytes(_config["Encryption:Key"]);
            var iv = Encoding.UTF8.GetBytes(_config["Encryption:IV"]);

            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;

            var decryptor = aes.CreateDecryptor();

            var bytes = Convert.FromBase64String(cipherText);

            var decrypted =
                decryptor.TransformFinalBlock(bytes, 0, bytes.Length);

            return Encoding.UTF8.GetString(decrypted);
        }
    }
}