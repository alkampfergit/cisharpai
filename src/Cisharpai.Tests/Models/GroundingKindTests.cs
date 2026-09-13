using Cisharpai.Models;

namespace Cisharpai.Tests.Models;

public sealed class GroundingKindTests
{
    [Test]
    public void GroundedChatCompletionResponse_DefaultsToNative()
    {
        var response = new GroundedChatCompletionResponse(
            ChatCompletion: new ChatCompletionResponse("content", "model", 10, 5),
            Citations: []);

        Assert.That(response.GroundingKind, Is.EqualTo(GroundingKind.Native));
    }

    [Test]
    public void GroundedChatCompletionResponse_CanBeSetToSynthesized()
    {
        var response = new GroundedChatCompletionResponse(
            ChatCompletion: new ChatCompletionResponse("content", "model", 10, 5),
            Citations: [],
            GroundingKind: GroundingKind.Synthesized);

        Assert.That(response.GroundingKind, Is.EqualTo(GroundingKind.Synthesized));
    }

    [Test]
    public void GroundedChatCompletionResponse_Error_DefaultsToNative()
    {
        var response = GroundedChatCompletionResponse.Error("some error");

        Assert.That(response.GroundingKind, Is.EqualTo(GroundingKind.Native));
    }
}
