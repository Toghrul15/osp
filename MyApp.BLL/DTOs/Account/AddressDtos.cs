using System.ComponentModel.DataAnnotations;

namespace MyApp.BLL.DTOs.Account;

public record AddressDto(
    int Id,
    string RecipientName,
    string PhoneNumber,
    string Line1,
    string? Line2,
    string City,
    string? State,
    string PostalCode,
    string Country,
    bool IsDefault);

public class AddressRequest
{
    public const int MaxAddressesPerUser = 10;

    /// <summary>Name of the person receiving the delivery.</summary>
    [Required, StringLength(100, MinimumLength = 2)]
    public string RecipientName { get; set; } = string.Empty;

    /// <summary>Contact phone, digits with an optional leading + (spaces, dashes and brackets allowed).</summary>
    [Required, StringLength(30, MinimumLength = 5)]
    [RegularExpression(@"^\+?[0-9 ()\-]{5,30}$", ErrorMessage = "Enter a valid phone number.")]
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>Street and house number.</summary>
    [Required, StringLength(200, MinimumLength = 3)]
    public string Line1 { get; set; } = string.Empty;

    /// <summary>Apartment, floor, etc. (optional).</summary>
    [StringLength(200)]
    public string? Line2 { get; set; }

    [Required, StringLength(100, MinimumLength = 2)]
    public string City { get; set; } = string.Empty;

    /// <summary>State or region (optional).</summary>
    [StringLength(100)]
    public string? State { get; set; }

    [Required, StringLength(20, MinimumLength = 2)]
    public string PostalCode { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 2)]
    public string Country { get; set; } = string.Empty;

    /// <summary>
    /// true makes this the default address (the previous default is unset). The first address is always the
    /// default. On update, false is ignored: to change the default, make another address the default instead.
    /// </summary>
    public bool IsDefault { get; set; }
}
