using System;
using System.Collections.Generic;
using Halo.ClaudeCode;
using Xunit;

namespace Halo.Tests;

public class ProxyScoutTests
{
    [Fact]
    public void Nothing_listening_means_no_proxy_and_not_one_request()
    {
        int asked = 0;
        var hit = ProxyScout.Pick(ProxyScout.Ports, _ => false, _ => { asked++; return true; });
        Assert.Null(hit);
        // the whole point of the cheap port test: a machine with no proxy must not pay for a dozen
        // three-second HTTP timeouts every time the API blips
        Assert.Equal(0, asked);
    }

    [Fact]
    public void An_open_port_that_carries_nothing_is_not_adopted()
    {
        Assert.Null(ProxyScout.Pick(new[] { 10809 }, _ => true, _ => false));
    }

    [Fact]
    public void The_first_candidate_that_carries_a_request_wins()
    {
        var tried = new List<string>();
        var hit = ProxyScout.Pick(new[] { 10809, 2080 }, _ => true,
                                  u => { tried.Add(u); return u.Contains("2080"); });
        Assert.Equal("http://127.0.0.1:2080", hit);
        Assert.Equal(3, tried.Count);   // both schemes on 10809, then http on 2080
    }

    [Fact]
    public void Ports_are_tried_in_the_scheme_their_client_actually_uses()
    {
        // trying the wrong scheme first is not wrong, just slow - and this runs while the user is already
        // looking at a pill that says nothing is reachable
        Assert.StartsWith("http://", ProxyScout.UrlsFor(10809)[0]);
        Assert.StartsWith("socks5://", ProxyScout.UrlsFor(10808)[0]);
        Assert.StartsWith("socks5://", ProxyScout.UrlsFor(1080)[0]);
    }

    [Fact]
    public void Both_schemes_are_offered_for_every_port()
    {
        foreach (int p in ProxyScout.Ports)
        {
            var u = ProxyScout.UrlsFor(p);
            Assert.Equal(2, u.Length);
            Assert.Contains(u, s => s.StartsWith("http://", StringComparison.Ordinal));
            Assert.Contains(u, s => s.StartsWith("socks5://", StringComparison.Ordinal));
            Assert.All(u, s => Assert.Contains("127.0.0.1:" + p, s));
        }
    }

    [Fact]
    public void The_port_list_has_no_duplicates()
    {
        Assert.Equal(ProxyScout.Ports.Length, new HashSet<int>(ProxyScout.Ports).Count);
    }

    [Fact]
    public void Claude_s_own_settings_file_is_read_before_anything_is_guessed()
    {
        Assert.Equal("http://127.0.0.1:2080",
            ProxyScout.FromSettings("""{"env":{"HTTPS_PROXY":"http://127.0.0.1:2080"}}"""));
        Assert.Equal("http://127.0.0.1:1", ProxyScout.FromSettings("""{"env":{"http_proxy":"http://127.0.0.1:1"}}"""));
    }

    [Fact]
    public void Https_wins_over_http_when_the_file_carries_both()
    {
        Assert.Equal("https://a",
            ProxyScout.FromSettings("""{"env":{"HTTP_PROXY":"http://b","HTTPS_PROXY":"https://a"}}"""));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json at all")]
    [InlineData("{}")]
    [InlineData("""{"env":{}}""")]
    [InlineData("""{"env":"a string, not an object"}""")]
    [InlineData("""{"env":{"HTTPS_PROXY":""}}""")]
    [InlineData("""{"env":{"HTTPS_PROXY":"   "}}""")]
    [InlineData("""{"env":{"HTTPS_PROXY":123}}""")]
    public void A_file_with_nothing_useful_in_it_is_no_proxy_not_a_crash(string json)
    {
        Assert.Null(ProxyScout.FromSettings(json));
    }
}
