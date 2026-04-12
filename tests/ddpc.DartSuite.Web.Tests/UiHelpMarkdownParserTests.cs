using ddpc.DartSuite.Web.Services;
using FluentAssertions;

namespace ddpc.DartSuite.Web.Tests;

public sealed class UiHelpMarkdownParserTests
{
    [Fact]
    public void Parse_ShouldReadHelpSections_WithBothSupportedHeaderFormats()
    {
        const string markdown = """
# Demo

### [help:page.demo]
Demo page help text.

### help:field.input
Input tooltip text.
Second line for expanded panel.

### Unrelated Header
Ignored
""";

        var result = UiHelpMarkdownParser.Parse(markdown);

        result.Should().ContainKey("page.demo");
        result["page.demo"].Should().Be("Demo page help text.");

        result.Should().ContainKey("field.input");
        result["field.input"].Should().Contain("Input tooltip text.");
        result["field.input"].Should().Contain("Second line for expanded panel.");
    }

    [Fact]
    public void Parse_ShouldKeepFirstOccurrence_WhenKeyIsDuplicated()
    {
        const string markdown = """
### [help:dup.key]
First value

### [help:dup.key]
Second value
""";

        var result = UiHelpMarkdownParser.Parse(markdown);

        result["dup.key"].Should().Be("First value");
    }
}
