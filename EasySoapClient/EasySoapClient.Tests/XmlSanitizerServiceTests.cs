using System.Diagnostics;

namespace EasySoapClient.Tests;

public class XmlSanitizerServiceTests
{
    private readonly Services.XmlSanitizerService _sanitizer = new();

    [Theory]
    [InlineData("<a>x&#x1F;y</a>", "<a>xy</a>")]
    [InlineData("<a>x&#31;y</a>", "<a>xy</a>")]
    [InlineData("<a>x&#0;y&#xFFFE;</a>", "<a>xy</a>")]
    [InlineData("<a>&#65;&#x42;&amp;&lt;&#x1F600;</a>", "<a>&#65;&#x42;&amp;&lt;&#x1F600;</a>")]
    [InlineData("<a>x\u0001y\u0008</a>", "<a>xy</a>")]
    [InlineData("<a>&#x;&#;&#xZZ;</a>", "<a>&#x;&#;&#xZZ;</a>")]
    [InlineData("<a>x&#x00000000000000000000000001;y</a>", "<a>xy</a>")]
    [InlineData("<a>x&#x0000000000000000000000000041;y</a>", "<a>x&#x0000000000000000000000000041;y</a>")]
    [InlineData("<a>x&#x100000000;y&#99999999999;</a>", "<a>xy</a>")]
    [InlineData("<a>&#", "<a>&#")]
    [InlineData("<a>&", "<a>&")]
    public void Removes_only_illegal_characters(string input, string expected)
    {
        Assert.Equal(expected, _sanitizer.RemoveIllegalCharacters(input));
    }

    [Fact]
    public void References_inside_cdata_and_comments_are_left_untouched_but_raw_chars_removed()
    {
        string input = "<a><![CDATA[&#x1F;\u0001]]><!-- &#x1F; -->&#x1F;</a>";

        Assert.Equal("<a><![CDATA[&#x1F;]]><!-- &#x1F; --></a>", _sanitizer.RemoveIllegalCharacters(input));
    }

    [Fact]
    public void Valid_surrogate_pairs_are_kept_and_lone_surrogates_removed()
    {
        Assert.Equal("<a>😀</a>", _sanitizer.RemoveIllegalCharacters("<a>😀</a>"));
        Assert.Equal("<a>x</a>", _sanitizer.RemoveIllegalCharacters("<a>x\uD800</a>"));
    }

    [Fact]
    public void Returns_same_instance_when_nothing_to_remove()
    {
        string input = "<a>clean &amp; tidy</a>";

        Assert.Same(input, _sanitizer.RemoveIllegalCharacters(input));
    }

    [Fact]
    public void Many_ampersands_without_semicolon_are_handled_in_linear_time()
    {
        // A quadratic scan (searching for ';' from every '&') takes many seconds on this input.
        string input = "<a>" + new string('&', 1_000_000) + "&#x1F;</a>";

        var stopwatch = Stopwatch.StartNew();
        string result = _sanitizer.RemoveIllegalCharacters(input);

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2), $"Took {stopwatch.Elapsed}");
        Assert.EndsWith("&</a>", result);
        Assert.DoesNotContain("&#x1F;", result);
    }

    [Fact]
    public void Many_unterminated_comments_are_handled_in_linear_time()
    {
        // A regex with a lazy "<!--.*?-->" rescans to the end from every "<!--" (quadratic).
        string input = String.Concat(Enumerable.Repeat("<!-- x", 200_000)) + "&#x1F;";

        var stopwatch = Stopwatch.StartNew();
        string result = _sanitizer.RemoveIllegalCharacters(input);

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2), $"Took {stopwatch.Elapsed}");
        Assert.EndsWith("&#x1F;", result); // inside an unterminated comment, so protected
    }
}
