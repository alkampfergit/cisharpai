using Cisharpai.Models;

namespace Cisharpai.Tests.Models;

public sealed class MessageContentPartTests
{
    [Test]
    public void TextContentPart_Stores_Text()
    {
        var part = new TextContentPart("hello");
        Assert.That(part.Text, Is.EqualTo("hello"));
    }

    [Test]
    public void ImageFileContentPart_Stores_FilePath()
    {
        var part = new ImageFileContentPart("/path/to/image.png");
        Assert.That(part.FilePath, Is.EqualTo("/path/to/image.png"));
    }

    [Test]
    public void ImageBase64ContentPart_Stores_Data_And_MediaType()
    {
        var part = new ImageBase64ContentPart("base64data", "image/png");
        Assert.That(part.Base64Data, Is.EqualTo("base64data"));
        Assert.That(part.MediaType, Is.EqualTo("image/png"));
    }

    [Test]
    public void TextContentPart_Inherits_From_MessageContentPart()
    {
        MessageContentPart part = new TextContentPart("text");
        Assert.That(part, Is.InstanceOf<MessageContentPart>());
    }

    [Test]
    public void ImageFileContentPart_Inherits_From_MessageContentPart()
    {
        MessageContentPart part = new ImageFileContentPart("/path.png");
        Assert.That(part, Is.InstanceOf<MessageContentPart>());
    }

    [Test]
    public void ImageBase64ContentPart_Inherits_From_MessageContentPart()
    {
        MessageContentPart part = new ImageBase64ContentPart("data", "image/png");
        Assert.That(part, Is.InstanceOf<MessageContentPart>());
    }

    [Test]
    public void Records_Support_Equality_TextContentPart()
    {
        var a = new TextContentPart("hello");
        var b = new TextContentPart("hello");
        Assert.That(a, Is.EqualTo(b));
    }

    [Test]
    public void LlmMessage_Without_ContentParts_Has_Null()
    {
        var msg = new LlmMessage(LlmRole.User, "hello");
        Assert.That(msg.ContentParts, Is.Null);
    }

    [Test]
    public void LlmMessage_With_ToolCalls_Still_Works_And_ContentParts_Is_Null()
    {
        var toolCall = new ToolCall("id1", "myFunc", System.Text.Json.JsonDocument.Parse("{}").RootElement);
        var msg = new LlmMessage(LlmRole.Assistant, "content", ToolCalls: new[] { toolCall });
        Assert.That(msg.Content, Is.EqualTo("content"));
        Assert.That(msg.ToolCalls, Is.Not.Null);
        Assert.That(msg.ContentParts, Is.Null);
    }

    [Test]
    public void LlmMessage_With_ContentParts_Stores_Parts()
    {
        IReadOnlyList<MessageContentPart> parts = new MessageContentPart[]
        {
            new TextContentPart("describe this"),
            new ImageFileContentPart("/path/img.png")
        };

        var msg = new LlmMessage(LlmRole.User, string.Empty, ContentParts: parts);

        Assert.That(msg.ContentParts, Is.Not.Null);
        Assert.That(msg.ContentParts!.Count, Is.EqualTo(2));
        Assert.That(msg.ContentParts[0], Is.InstanceOf<TextContentPart>());
        Assert.That(msg.ContentParts[1], Is.InstanceOf<ImageFileContentPart>());
    }

    [Test]
    public void WithImage_Creates_Text_And_ImageFile_Parts()
    {
        var msg = LlmMessage.WithImage("describe", "/path.png");

        Assert.That(msg.Role, Is.EqualTo(LlmRole.User));
        Assert.That(msg.Content, Is.EqualTo(string.Empty));
        Assert.That(msg.ContentParts, Is.Not.Null);
        Assert.That(msg.ContentParts!.Count, Is.EqualTo(2));

        var textPart = msg.ContentParts[0] as TextContentPart;
        Assert.That(textPart, Is.Not.Null);
        Assert.That(textPart!.Text, Is.EqualTo("describe"));

        var imagePart = msg.ContentParts[1] as ImageFileContentPart;
        Assert.That(imagePart, Is.Not.Null);
        Assert.That(imagePart!.FilePath, Is.EqualTo("/path.png"));
    }

    [Test]
    public void WithBase64Image_Creates_Text_And_Base64_Parts()
    {
        var msg = LlmMessage.WithBase64Image("describe", "base64data", "image/png");

        Assert.That(msg.Role, Is.EqualTo(LlmRole.User));
        Assert.That(msg.Content, Is.EqualTo(string.Empty));
        Assert.That(msg.ContentParts, Is.Not.Null);
        Assert.That(msg.ContentParts!.Count, Is.EqualTo(2));

        var textPart = msg.ContentParts[0] as TextContentPart;
        Assert.That(textPart, Is.Not.Null);
        Assert.That(textPart!.Text, Is.EqualTo("describe"));

        var imagePart = msg.ContentParts[1] as ImageBase64ContentPart;
        Assert.That(imagePart, Is.Not.Null);
        Assert.That(imagePart!.Base64Data, Is.EqualTo("base64data"));
        Assert.That(imagePart.MediaType, Is.EqualTo("image/png"));
    }
}
