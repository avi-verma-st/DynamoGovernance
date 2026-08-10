using System;
using System.Security.Cryptography;
using System.Text;

namespace DynamoGovernance.Core.Services
{
    public static class IdentityService
    {
        private static bool _useHashing = false;
        /// <summary>
        /// Enable or disable hashing of identifiers
        /// </summary>
        public static void SetHashingEnabled(bool enabled)
        {
            _useHashing = enabled;
        }

        /// <summary>
        /// Gets the user identifier (hashed or plain)
        /// </summary>
        public static string GetUserId()
        {
            string userId = $"{Environment.UserDomainName}\\{Environment.UserName}";
            return _useHashing ? HashString(userId) : userId;
        }

        /// <summary>
        /// Gets the machine identifier (hashed or plain)
        /// </summary>
        public static string GetMachineId()
        {
            string machineId = Environment.MachineName;
            return _useHashing ? HashString(machineId) : machineId;
        }

        private static string HashString(string input)
        {
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }
}
