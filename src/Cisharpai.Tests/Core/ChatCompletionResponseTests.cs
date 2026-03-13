using Cisharpai.Models;

namespace Cisharpai.Tests.Core;

public sealed class ChatCompletionResponseTests
{
    [Test]
    public void DefaultResponse_HasIsSuccessTrue()
    {
        var response = new ChatCompletionResponse("hi", "model", 1, 1);

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.True);
            Assert.That(response.ErrorMessage, Is.Null);
        });
    }

    [Test]
    public void Error_ReturnsResponseWithIsSuccessFalse()
    {
        var response = ChatCompletionResponse.Error("Something went wrong");

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.False);
            Assert.That(response.ErrorMessage, Is.EqualTo("Something went wrong"));
            Assert.That(response.Content, Is.EqualTo(string.Empty));
            Assert.That(response.Model, Is.EqualTo(string.Empty));
            Assert.That(response.PromptTokens, Is.EqualTo(0));
            Assert.That(response.CompletionTokens, Is.EqualTo(0));
            Assert.That(response.RawResponseJson, Is.Null);
        });
    }

    [Test]
    public void Error_WithRawJson_IncludesRawJson()
    {
        var response = ChatCompletionResponse.Error("error", """{"error":"bad"}""");

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.False);
            Assert.That(response.RawResponseJson, Is.EqualTo("""{"error":"bad"}"""));
        });
    }
}
