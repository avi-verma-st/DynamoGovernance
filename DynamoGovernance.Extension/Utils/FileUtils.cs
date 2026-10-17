using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DynamoGovernance.Extension.Utils
{
    internal class FileUtils
    {
        internal static long? GetFileSizeBytes(
            string? filePath)
        {
            if (filePath is null)
            {
                return null;
            }

            try
            {
                FileInfo file = new(filePath);
                return file.Exists ? file.Length : null;
            }
            catch
            {
                return null;

            }
        }
    }
}
