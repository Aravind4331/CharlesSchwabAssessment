using System.Security.Cryptography;

namespace CharlesSchwabAssessment.Services
{
    public sealed class ShortCodeGenerator
    {
        private const string Alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        public string Generate(int length = 8)
        {
            if (length < 4)
            {
                throw new ArgumentOutOfRangeException(nameof(length), "Length must be at least 4.");
            }

            Span<byte> buffer = stackalloc byte[length];
            RandomNumberGenerator.Fill(buffer);

            var chars = new char[length];
            for (var i = 0; i < length; i++)
            {
                chars[i] = Alphabet[buffer[i] % Alphabet.Length];
            }

            return new string(chars);
        }
    }
}
