using System;
using Xunit;

namespace HTTPServer.Test.ReaderTests;

public class ReadRequestLineTests
{
    private static readonly byte Space = (byte)' ';

    [Fact]
    public void Parses_Valid_RequestLine()
    {
        // Arrange
        var line = "GET /hello HTTP/1.1"u8.ToArray();

        // Act
        Http1Parser.ReadRequestLine(line, out var method, out var target, out var version);

        // Assert
        Assert.Equal("GET", method.GetString());
        Assert.Equal("/hello", target.GetString());
        Assert.Equal("HTTP/1.1", version.GetString());
    }

    [Fact]
    public void Parses_Different_Method_Target_And_Version()
    {
        // Arrange
        var line = "POST /api/v2/items HTTP69.0"u8.ToArray();

        // Act
        Http1Parser.ReadRequestLine(line, out var method, out var target, out var version);

        // Assert
        Assert.Equal("POST", method.GetString());
        Assert.Equal("/api/v2/items", target.GetString());
        Assert.Equal("HTTP69.0", version.GetString());
    }

    [Fact]
    public void Throws_When_Method_Is_Missing()
    {
        // Arrange
        var line = " /target HTTP/1.1"u8.ToArray(); // starts with space → no method

        // Act & Assert
        Assert.Throws<FormatException>(() =>
            Http1Parser.ReadRequestLine(line, out _, out _, out _));
    }

    [Fact]
    public void Throws_When_Target_Is_Missing()
    {
        // Arrange
        var line = "GET  HTTP/1.1"u8.ToArray(); // double space → empty target

        // Act & Assert
        Assert.Throws<FormatException>(() =>
            Http1Parser.ReadRequestLine(line, out _, out _, out _));
    }

    [Fact]
    public void Throws_When_Version_Is_Missing()
    {
        // Arrange
        var line = "GET /hello "u8.ToArray(); // ends with space → no version

        // Act & Assert
        Assert.Throws<FormatException>(() =>
            Http1Parser.ReadRequestLine(line, out var method, out var target, out var version));
    }
}
