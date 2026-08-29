using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace CloudAdvisor.ViewModels
{
    /// <summary>
    /// View model representing the file upload details.
    /// </summary>
    public class UploadViewModel
    {
        /// <summary>
        /// Gets or sets the custom name of the project. If not provided, the ZIP file name will be used.
        /// </summary>
        [Display(Name = "Project Name (Optional)")]
        [StringLength(100, ErrorMessage = "Project name cannot exceed 100 characters.")]
        public string? ProjectName { get; set; }

        /// <summary>
        /// Gets or sets the uploaded ZIP archive file containing C# source code.
        /// </summary>
        [Required(ErrorMessage = "Please select an ASP.NET Core project ZIP file to upload.")]
        [DataType(DataType.Upload)]
        [Display(Name = "Project ZIP File")]
        public IFormFile ZipFile { get; set; } = null!;
    }
}
