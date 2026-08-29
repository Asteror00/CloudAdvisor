using System;
using System.IO;
using System.IO.Compression;
using Microsoft.AspNetCore.Http;

namespace CloudAdvisor.Utilities
{
    public static class FileValidator
    {
        /// <summary>
        /// Validates file extension, size limit, ASP.NET Core project presence (.csproj), and blocks binary execution files.
        /// </summary>
        public static (bool IsValid, string ErrorMessage) ValidateZipFile(IFormFile file, long maxSizeBytes = 52428800)
        {
            if (file == null || file.Length == 0)
            {
                return (false, "Uploaded file is empty.");
            }

            if (file.Length > maxSizeBytes)
            {
                return (false, "File size exceeds the maximum upload limit of 50MB.");
            }

            var extension = Path.GetExtension(file.FileName);
            if (!extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                return (false, "Only ZIP (.zip) files are accepted.");
            }

            try
            {
                using (var stream = file.OpenReadStream())
                {
                    using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
                    {
                        bool hasCsproj = false;
                        foreach (var entry in archive.Entries)
                        {
                            var entryName = entry.Name.ToLowerInvariant();

                            // Rejection of executable/binary files for security
                            if (entryName.EndsWith(".exe") || entryName.EndsWith(".dll"))
                            {
                                return (false, $"Security violation: ZIP archive contains illegal binary file '{entry.FullName}'.");
                            }

                            if (entryName.EndsWith(".csproj"))
                            {
                                hasCsproj = true;
                            }
                        }

                        if (!hasCsproj)
                        {
                            return (false, "Invalid project: ZIP archive must contain at least one .csproj file to verify it is an ASP.NET Core project.");
                        }
                    }
                }
            }
            catch (InvalidDataException)
            {
                return (false, "The uploaded file is not a valid ZIP archive or is corrupted.");
            }
            catch (Exception ex)
            {
                return (false, $"An error occurred during file validation: {ex.Message}");
            }

            return (true, string.Empty);
        }
    }
}
