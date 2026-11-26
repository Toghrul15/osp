namespace MyApp.Core.Constants;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Vendor = "Vendor";
    public const string Customer = "Customer";

    public static readonly string[] All = [Admin, Vendor, Customer];
}
