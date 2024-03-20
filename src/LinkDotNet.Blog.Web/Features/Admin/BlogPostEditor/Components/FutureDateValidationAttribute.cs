using System;
using System.ComponentModel.DataAnnotations;

namespace LinkDotNet.Blog.Web.Features.Admin.BlogPostEditor.Components;

[AttributeUsage(AttributeTargets.Property)]
public sealed class FutureDateValidationAttribute : ValidationAttribute
{
    protected override ValidationResult IsValid(object value, ValidationContext validationContext) => value is not null
            ? (DateTime)value <= DateTime.UtcNow
                ? new ValidationResult("The scheduled publish date must be in the future.")
                : ValidationResult.Success
            : ValidationResult.Success;
}
