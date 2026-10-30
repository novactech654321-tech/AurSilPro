namespace AurSil.Api.Models;

public record RegisterRequest(string UserId, string Name, string Email, string Phone, string Password);

public record LoginRequest(string Login, string Password);

public record VerifyOtpRequest(string Email, string Code);

public record EmailRequest(string Email);

public record ResetPasswordRequest(string Email, string Code, string NewPassword);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record ApiMessage(string Message);

public record UserProfile(string UserId, string Name, string Email, string Phone, string Status, string Role);

public static class OtpPurpose
{
    public const string Register = "Register";
    public const string ResetPassword = "ResetPassword";
}

public static class UserStatus
{
    public const string Pending = "Pending";
    public const string Active = "Active";
    public const string Blocked = "Blocked";
}

public static class UserRole
{
    public const string User = "User";
    public const string Admin = "Admin";
}

public class UserRow
{
    public string UserId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Status { get; set; } = "";
    public string Role { get; set; } = UserRole.User;

    public UserProfile ToProfile() => new(UserId, Name, Email, Phone, Status, Role);
}
