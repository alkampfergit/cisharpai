using System.Text.Json;
using Cisharpai.Models;

namespace Cisharpai.Tests.Models;

public sealed class ToolCallingResponseTests
{
    [Test]
    public void IsSuccess_DelegatesToChatCompletion()
    {
        var chatCompletion = new ChatCompletionResponse(
            Content: "text",
            Model: "gpt-4",
            PromptTokens: 10,
            CompletionTokens: 5);

        var response = new ToolCallingResponse(chatCompletion, null);

        Assert.That(response.IsSuccess, Is.True);
    }

    [Test]
    public void IsSuccess_FalseWhenError()
    {
        var response = ToolCallingResponse.Error("something failed");

        Assert.That(response.IsSuccess, Is.False);
    }

    [Test]
    public void ErrorMessage_DelegatesToChatCompletion()
    {
        var response = ToolCallingResponse.Error("something failed");

        Assert.That(response.ErrorMessage, Is.EqualTo("something failed"));
    }

    [Test]
    public void Content_DelegatesToChatCompletion()
    {
        var chatCompletion = new ChatCompletionResponse(
            Content: "Hello world",
            Model: "gpt-4",
            PromptTokens: 10,
            CompletionTokens: 5);

        var response = new ToolCallingResponse(chatCompletion, null);

        Assert.That(response.Content, Is.EqualTo("Hello world"));
    }

    [Test]
    public void ToolCalls_NullWhenTextResponse()
    {
        var chatCompletion = new ChatCompletionResponse(
            Content: "Hello",
            Model: "gpt-4",
            PromptTokens: 10,
            CompletionTokens: 5);

        var response = new ToolCallingResponse(chatCompletion, null);

        Assert.That(response.ToolCalls, Is.Null);
    }

    [Test]
    public void ToolCalls_PopulatedWhenToolCallsPresent()
    {
        var args = JsonDocument.Parse("""{"city":"Paris"}""").RootElement.Clone();
        var toolCalls = new List<ToolCall> { new("call-1", "get_weather", args) };

        var chatCompletion = new ChatCompletionResponse(
            Content: "",
            Model: "gpt-4",
            PromptTokens: 10,
            CompletionTokens: 5);

        var response = new ToolCallingResponse(chatCompletion, toolCalls);

        Assert.Multiple(() =>
        {
            Assert.That(response.ToolCalls, Is.Not.Null);
            Assert.That(response.ToolCalls!, Has.Count.EqualTo(1));
            Assert.That(response.ToolCalls![0].FunctionName, Is.EqualTo("get_weather"));
        });
    }

    [Test]
    public void Error_CreatesErrorResponse_WithRawJson()
    {
        var response = ToolCallingResponse.Error("fail", """{"error":"bad"}""");

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.False);
            Assert.That(response.ErrorMessage, Is.EqualTo("fail"));
            Assert.That(response.ToolCalls, Is.Null);
            Assert.That(response.ChatCompletion.RawResponseJson, Is.EqualTo("""{"error":"bad"}"""));
        });
    }

    [Test]
    public void Error_CreatesErrorResponse_WithEmptyContent()
    {
        var response = ToolCallingResponse.Error("fail");

        Assert.That(response.Content, Is.Empty);
    }
}
