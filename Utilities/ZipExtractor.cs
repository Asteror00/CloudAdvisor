using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace CloudAdvisor.Utilities
{
    public static class ZipExtractor
    {
        /// <summary>
        /// Safely extracts all source files from a ZIP archive to a destination folder.
        /// Prevents Zip Slip directory traversal vulnerability.
        /// </summary>
        public static List<string> ExtractSourceFiles(string zipFilePath, string destinationDirectory)
        {
            var csFilePaths = new List<string>();
            var destDirInfo = Directory.CreateDirectory(destinationDirectory);
            string destinationCanonicalPath = destDirInfo.FullName;

            using (var archive = ZipFile.OpenRead(zipFilePath))
            {
                foreach (var entry in archive.Entries)
                {
                    // Skip directories
                    if (entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\"))
                    {
                        continue;
                    }

                    // Path traversal protection (Zip Slip mitigation)
                    string entryDestinationPath = Path.GetFullPath(Path.Combine(destinationCanonicalPath, entry.FullName));
                    if (!entryDestinationPath.StartsWith(destinationCanonicalPath, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException("Security violation: ZIP archive entry attempted path traversal outside target directory.");
                    }

                    // Create parent directories if needed
                    string? parentDir = Path.GetDirectoryName(entryDestinationPath);
                    if (parentDir != null)
                    {
                        Directory.CreateDirectory(parentDir);
                    }

                    // Extract file with overwrite
                    entry.ExtractToFile(entryDestinationPath, overwrite: true);

                    // Track C# files for Roslyn static code analysis
                    if (Path.GetExtension(entryDestinationPath).Equals(".cs", StringComparison.OrdinalIgnoreCase))
                    {
                        csFilePaths.Add(entryDestinationPath);
                    }
                }
            }

            return csFilePaths;
        }
    }
}
