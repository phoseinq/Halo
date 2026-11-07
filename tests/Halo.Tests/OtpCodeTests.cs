using Halo.Notifications;

namespace Halo.Tests;

// The code the banner offers to copy: which shapes are read, and which are left alone.
public class OtpCodeTests
{
    [Theory]
    [InlineData("Sign-in code", "Your verification code is 482913. It expires in 10 minutes.", "482913")]
    [InlineData("G-482913 is your Google verification code", "", "482913")]
    [InlineData("Your code", "123 456", "123456")]
    [InlineData("Login", "Use 4821-9930 to sign in", "48219930")]
    [InlineData("Bank", "Your OTP is 5832", "5832")]
    [InlineData("Steam Guard", "Your login code: F7K2Q", "F7K2Q")]
    [InlineData("\u06A9\u062F \u062A\u0627\u06CC\u06CC\u062F \u0634\u0645\u0627", "\u06F4\u06F8\u06F2\u06F9\u06F1\u06F3", "482913")]
    [InlineData("SMS", "\u0631\u0645\u0632 \u06CC\u06A9\u0628\u0627\u0631 \u0645\u0635\u0631\u0641: \u06F5\u06F8\u06F3\u06F2", "5832")]
    [InlineData("Order", "Your order 12345678 has shipped", "12345678")]
    public void Codes_are_found(string title, string body, string expected)
        => Assert.Equal(expected, OtpCode.Find(title, body));

    [Theory]
    [InlineData("Meeting", "See you at 2026 offices, room 4512")]          // no code word: short runs stay
    [InlineData("Price", "Now 1999 instead of 2499")]
    [InlineData("Your code", "valid until 2027")]                           // a year, even next to "code"
    [InlineData("Hello", "HELLO WORLD")]
    public void Ordinary_numbers_are_left_alone(string title, string body)
        => Assert.Equal("", OtpCode.Find(title, body));
}
