using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using SantaFeWaterSystem.Models.ViewModels;

namespace SantaFeWaterSystem.Models.ViewModels
{
    public class PrivacyPolicySectionCreateViewModel
    {
        [Required(ErrorMessage = "Section title is required.")]
        public string SectionTitle { get; set; } = string.Empty;

        [Required(ErrorMessage = "Section content is required.")]
        public string Content { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
    }

    public class CreatePrivacyPolicyViewModel
    {
        [Required(ErrorMessage = "Title is required.")]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Privacy Policy content is required.")]
        public string Content { get; set; } = string.Empty;

        [Required(ErrorMessage = "Version is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Version must be at least 1.")]
        public int Version { get; set; }

        // Allow adding multiple sections when creating
        public List<PrivacyPolicySectionCreateViewModel> Sections { get; set; } = new List<PrivacyPolicySectionCreateViewModel>();
    }
}
