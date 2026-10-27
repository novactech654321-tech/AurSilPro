using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AurSil.Api.Services;

public class RazorpayOptions
{
    public string KeyId { get; set; } = "";
    public string KeySecret { get; set; } = "";
}

// Razorpay Orders API + checkout signature verification. Use rzp_test_ keys for test mode.
public class RazorpayService(RazorpayOptions options, IHttpClientFactory httpFactory)
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.KeyId) && !string.IsNullOrWhiteSpace(options.KeySecret);

    public string KeyId => options.KeyId;

    public async Task<string> CreateOrderAsync(decimal amountInr, string receipt)
    {
        var http = httpFactory.CreateClient("razorpay");
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.razorpay.com/v1/orders")
        {
            Content = JsonContent.Create(new { amount = (long)Math.Round(amountInr * 100), currency = "INR", receipt }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes($"{options.KeyId}:{options.KeySecret}")));

        using var response = await http.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Razorpay order failed: {body}");

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("id").GetString()!;
    }

    public bool VerifySignature(string razorpayOrderId, string paymentId, string signature)
    {
        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(options.KeySecret),
            Encoding.UTF8.GetBytes($"{razorpayOrderId}|{paymentId}"));
        var actual = Convert.FromHexString(signature.Length == 64 ? signature : new string('0', 64));
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}
