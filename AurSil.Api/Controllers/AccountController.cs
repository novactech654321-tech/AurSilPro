using System.Text.Json;
using System.Text.RegularExpressions;
using AurSil.Api.Models;
using AurSil.Api.Services;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace AurSil.Api.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public partial class AccountController(IConfiguration config, IHttpClientFactory httpFactory,
    ActivityService activity, TradeService trade, McpKeyService mcpKeys) : ControllerBase
{
    private readonly string _connectionString = config.GetConnectionString("Aursil")!;

    [GeneratedRegex(@"^[A-Z]{4}0[A-Z0-9]{6}$")]
    private static partial Regex IfscRegex();

    [GeneratedRegex(@"^\d{9,18}$")]
    private static partial Regex AccountRegex();

    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        await using var db = new SqlConnection(_connectionString);
        var user = await db.QuerySingleOrDefaultAsync<UserRow>("SELECT * FROM Users WHERE UserId = @id", new { id = User.UserId() });
        return user is null ? NotFound() : Ok(user.ToProfile());
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest req)
    {
        if (PasswordHasher.RuleError(req.NewPassword) is { } pwError)
            return BadRequest(new ApiMessage(pwError));

        await using var db = new SqlConnection(_connectionString);
        var user = await db.QuerySingleOrDefaultAsync<UserRow>("SELECT * FROM Users WHERE UserId = @id", new { id = User.UserId() });
        if (user is null)
            return NotFound(new ApiMessage("Account not found."));
        if (!PasswordHasher.Verify(req.CurrentPassword ?? "", user.PasswordHash))
            return BadRequest(new ApiMessage("Current password is incorrect."));
        if (req.CurrentPassword == req.NewPassword)
            return BadRequest(new ApiMessage("New password must be different from the current one."));

        await db.ExecuteAsync("UPDATE Users SET PasswordHash = @hash WHERE UserId = @UserId",
            new { hash = PasswordHasher.Hash(req.NewPassword), user.UserId });
        await activity.LogAsync(user.UserId, Activity.PasswordChanged, "Changed from the app");
        return Ok(new ApiMessage("Password changed successfully."));
    }

    // Tokens are stateless, so this only records the logout in the activity log
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await activity.LogAsync(User.UserId(), Activity.Logout);
        return Ok(new ApiMessage("Logged out."));
    }

    // AI assistant connector: the URL holds a personal key, so creating a new one disconnects any earlier assistant
    [HttpGet("assistant")]
    public async Task<IActionResult> Assistant()
    {
        var createdAt = await mcpKeys.CreatedAtAsync(User.UserId());
        return Ok(new { connected = createdAt is not null, createdAt });
    }

    [HttpPost("assistant/key")]
    public async Task<IActionResult> CreateAssistantKey()
    {
        var key = await mcpKeys.CreateAsync(User.UserId());
        await activity.LogAsync(User.UserId(), Activity.AssistantConnected, "Created an AI assistant connector link");
        return Ok(new { url = mcpKeys.ConnectorUrl(Request, key) });
    }

    [HttpDelete("assistant/key")]
    public async Task<IActionResult> RevokeAssistantKey()
    {
        await mcpKeys.RevokeAsync(User.UserId());
        await activity.LogAsync(User.UserId(), Activity.AssistantDisconnected, "Disconnected the AI assistant");
        return Ok(new ApiMessage("AI assistant disconnected."));
    }

    [HttpGet("portfolio")]
    public async Task<IActionResult> Portfolio() => Ok(await trade.PortfolioAsync(User.UserId()));

    [HttpGet("bank")]
    public async Task<IActionResult> GetBank()
    {
        await using var db = new SqlConnection(_connectionString);
        var bank = await db.QuerySingleOrDefaultAsync<BankAccountRow>("SELECT * FROM BankAccounts WHERE UserId = @id", new { id = User.UserId() });
        return bank is null ? NoContent() : Ok(bank.ToDto());
    }

    [HttpPut("bank")]
    public async Task<IActionResult> SaveBank(BankAccountRequest req)
    {
        var holder = req.HolderName?.Trim() ?? "";
        var account = req.AccountNumber?.Trim() ?? "";
        var ifsc = req.Ifsc?.Trim().ToUpperInvariant() ?? "";

        if (holder.Length < 3)
            return BadRequest(new ApiMessage("Enter the account holder name as per bank records."));
        if (!AccountRegex().IsMatch(account))
            return BadRequest(new ApiMessage("Account number must be 9 to 18 digits."));
        if (!IfscRegex().IsMatch(ifsc))
            return BadRequest(new ApiMessage("Enter a valid 11-character IFSC code (e.g. HDFC0001234)."));

        var info = await LookupIfscAsync(ifsc);
        if (info is null)
            return BadRequest(new ApiMessage("IFSC code not found. Please check it."));

        await using var db = new SqlConnection(_connectionString);
        await db.ExecuteAsync(
            """
            MERGE BankAccounts AS b USING (SELECT @userId AS UserId) AS s ON b.UserId = s.UserId
            WHEN MATCHED THEN UPDATE SET HolderName = @holder, AccountNumber = @account, Ifsc = @ifsc, BankName = @bankName, Branch = @branch, UpdatedAt = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN INSERT (UserId, HolderName, AccountNumber, Ifsc, BankName, Branch) VALUES (@userId, @holder, @account, @ifsc, @bankName, @branch);
            """,
            new { userId = User.UserId(), holder, account, ifsc, bankName = info.BankName, branch = $"{info.Branch}, {info.City}" });

        var saved = await db.QuerySingleAsync<BankAccountRow>("SELECT * FROM BankAccounts WHERE UserId = @id", new { id = User.UserId() });
        await activity.LogAsync(saved.UserId, Activity.BankAccountSaved, $"{saved.BankName} {BankMask.Mask(saved.AccountNumber)}, {saved.Ifsc}");
        return Ok(saved.ToDto());
    }

    [HttpGet("bank/ifsc/{ifsc}")]
    public async Task<IActionResult> Ifsc(string ifsc)
    {
        ifsc = ifsc.Trim().ToUpperInvariant();
        if (!IfscRegex().IsMatch(ifsc))
            return BadRequest(new ApiMessage("Invalid IFSC format."));
        var info = await LookupIfscAsync(ifsc);
        return info is null ? NotFound(new ApiMessage("IFSC code not found.")) : Ok(info);
    }

    private async Task<IfscInfo?> LookupIfscAsync(string ifsc)
    {
        try
        {
            using var response = await httpFactory.CreateClient("prices").GetAsync($"https://ifsc.razorpay.com/{ifsc}");
            if (!response.IsSuccessStatusCode)
                return null;
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var r = doc.RootElement;
            return new IfscInfo(ifsc, r.GetProperty("BANK").GetString() ?? "", r.GetProperty("BRANCH").GetString() ?? "",
                r.GetProperty("CITY").GetString() ?? "");
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }
}
