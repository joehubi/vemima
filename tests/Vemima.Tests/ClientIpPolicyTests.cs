using System.Net;
using Vemima.Web;

namespace Vemima.Tests;

public class ClientIpPolicyTests
{
    private static readonly ClientIpPolicy Policy = new(["127.0.0.1", "192.168.178.31", "kein-ip", "::1"]);

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("192.168.178.31")]
    public void IsAllowed_AcceptsListedIPv4Addresses(string address)
    {
        Assert.True(Policy.IsAllowed(IPAddress.Parse(address)));
    }

    [Fact]
    public void IsAllowed_AcceptsIPv4MappedIPv6Addresses()
    {
        Assert.True(Policy.IsAllowed(IPAddress.Parse("::ffff:192.168.178.31")));
    }

    [Theory]
    [InlineData("192.168.178.99")]
    [InlineData("::1")]
    public void IsAllowed_RejectsUnlistedAndIPv6Addresses(string address)
    {
        Assert.False(Policy.IsAllowed(IPAddress.Parse(address)));
    }

    [Fact]
    public void IsAllowed_RejectsMissingAddress()
    {
        Assert.False(Policy.IsAllowed(null));
    }
}
