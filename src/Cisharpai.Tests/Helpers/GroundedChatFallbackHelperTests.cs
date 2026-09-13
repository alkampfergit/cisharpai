using Cisharpai.Helpers;
using Cisharpai.Models;

namespace Cisharpai.Tests.Helpers;

public sealed class GroundedChatFallbackHelperTests
{
    private static IReadOnlyList<DocumentChunk> CreateTextDocs() =>
    [
        new DocumentChunk(Id: "doc-1", Text: "Paris is the capital of France."),
        new DocumentChunk(Id: "doc-2", Text: "Berlin is the capital of Germany.")
    ];

    private static IReadOnlyList<DocumentChunk> CreateDataDocs() =>
    [
        new DocumentChunk(
            Id: "doc-1",
            Data: new Dictionary<string, string>
            {
                ["title"] = "France",
                ["snippet"] = "Paris is the capital of France."
            }),
        new DocumentChunk(
            Id: "doc-2",
            Data: new Dictionary<string, string>
            {
                ["title"] = "Germany",
                ["snippet"] = "Berlin is the capital of Germany."
            })
    ];

    #region BuildGroundingMessages

    [Test]
    public void BuildGroundingMessages_AppendsGroundingToExistingSystemMessage()
    {
        var messages = new List<LlmMessage>
        {
            new(LlmRole.System, "You are a helpful assistant."),
            new(LlmRole.User, "What is the capital of France?")
        };

        var result = GroundedChatFallbackHelper.BuildGroundingMessages(messages, CreateTextDocs());

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result[0].Role, Is.EqualTo(LlmRole.System));
            Assert.That(result[0].Content, Does.StartWith("You are a helpful assistant."));
            Assert.That(result[0].Content, Does.Contain("REFERENCE DOCUMENTS"));
            Assert.That(result[0].Content, Does.Contain("doc-1"));
            Assert.That(result[0].Content, Does.Contain("doc-2"));
            Assert.That(result[0].Content, Does.Contain("«cite:N»"));
        });
    }

    [Test]
    public void BuildGroundingMessages_CreatesSystemMessage_WhenNoneExists()
    {
        var messages = new List<LlmMessage>
        {
            new(LlmRole.User, "What is the capital of France?")
        };

        var result = GroundedChatFallbackHelper.BuildGroundingMessages(messages, CreateTextDocs());

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result[0].Role, Is.EqualTo(LlmRole.System));
            Assert.That(result[0].Content, Does.Contain("REFERENCE DOCUMENTS"));
            Assert.That(result[1].Role, Is.EqualTo(LlmRole.User));
            Assert.That(result[1].Content, Is.EqualTo("What is the capital of France?"));
        });
    }

    [Test]
    public void BuildGroundingMessages_IncludesDocumentContent_ForTextDocs()
    {
        var messages = new List<LlmMessage> { new(LlmRole.User, "Question") };

        var result = GroundedChatFallbackHelper.BuildGroundingMessages(messages, CreateTextDocs());

        Assert.Multiple(() =>
        {
            Assert.That(result[0].Content, Does.Contain("Paris is the capital of France."));
            Assert.That(result[0].Content, Does.Contain("Berlin is the capital of Germany."));
        });
    }

    [Test]
    public void BuildGroundingMessages_SerializesDataDocs_AsJson()
    {
        var messages = new List<LlmMessage> { new(LlmRole.User, "Question") };

        var result = GroundedChatFallbackHelper.BuildGroundingMessages(messages, CreateDataDocs());

        Assert.Multiple(() =>
        {
            Assert.That(result[0].Content, Does.Contain("\"title\""));
            Assert.That(result[0].Content, Does.Contain("France"));
        });
    }

    [Test]
    public void BuildGroundingMessages_GeneratesDocId_WhenNull()
    {
        var docs = new List<DocumentChunk> { new(Text: "Some content") };
        var messages = new List<LlmMessage> { new(LlmRole.User, "Question") };

        var result = GroundedChatFallbackHelper.BuildGroundingMessages(messages, docs);

        Assert.That(result[0].Content, Does.Contain("document_0"));
    }

    [Test]
    public void BuildGroundingMessages_PreservesUserMessages()
    {
        var messages = new List<LlmMessage>
        {
            new(LlmRole.System, "Be concise."),
            new(LlmRole.User, "Tell me about France."),
            new(LlmRole.Assistant, "France is great."),
            new(LlmRole.User, "What about Germany?")
        };

        var result = GroundedChatFallbackHelper.BuildGroundingMessages(messages, CreateTextDocs());

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(4));
            Assert.That(result[1].Content, Is.EqualTo("Tell me about France."));
            Assert.That(result[2].Content, Is.EqualTo("France is great."));
            Assert.That(result[3].Content, Is.EqualTo("What about Germany?"));
        });
    }

    #endregion

    #region ParseAndStripMarkers — basic round-trip

    [Test]
    public void ParseAndStripMarkers_SingleCitation_RoundTrips()
    {
        var raw = "The capital is «cite:0»Paris«/cite».";
        var docs = CreateTextDocs();

        var (clean, citations) = GroundedChatFallbackHelper.ParseAndStripMarkers(raw, docs);

        Assert.Multiple(() =>
        {
            Assert.That(clean, Is.EqualTo("The capital is Paris."));
            Assert.That(citations, Has.Count.EqualTo(1));
            Assert.That(citations[0].Text, Is.EqualTo("Paris"));
            Assert.That(citations[0].Start, Is.EqualTo(15));
            Assert.That(citations[0].End, Is.EqualTo(20));
            Assert.That(citations[0].Sources[0].Id, Is.EqualTo("doc-1"));
        });
    }

    [Test]
    public void ParseAndStripMarkers_MultipleCitations_CorrectOffsets()
    {
        var raw = "«cite:0»Paris«/cite» is great and «cite:1»Berlin«/cite» too.";
        var docs = CreateTextDocs();

        var (clean, citations) = GroundedChatFallbackHelper.ParseAndStripMarkers(raw, docs);

        Assert.Multiple(() =>
        {
            Assert.That(clean, Is.EqualTo("Paris is great and Berlin too."));
            Assert.That(citations, Has.Count.EqualTo(2));

            Assert.That(citations[0].Text, Is.EqualTo("Paris"));
            Assert.That(citations[0].Start, Is.EqualTo(0));
            Assert.That(citations[0].End, Is.EqualTo(5));
            Assert.That(citations[0].Sources[0].Id, Is.EqualTo("doc-1"));

            Assert.That(citations[1].Text, Is.EqualTo("Berlin"));
            Assert.That(citations[1].Start, Is.EqualTo(19));
            Assert.That(citations[1].End, Is.EqualTo(25));
            Assert.That(citations[1].Sources[0].Id, Is.EqualTo("doc-2"));
        });
    }

    [Test]
    public void ParseAndStripMarkers_SameDocCitedMultipleTimes_CorrectOffsets()
    {
        var raw = "«cite:0»Paris«/cite» and also «cite:0»the Eiffel Tower«/cite».";
        var docs = CreateTextDocs();

        var (clean, citations) = GroundedChatFallbackHelper.ParseAndStripMarkers(raw, docs);

        Assert.Multiple(() =>
        {
            Assert.That(clean, Is.EqualTo("Paris and also the Eiffel Tower."));
            Assert.That(citations, Has.Count.EqualTo(2));
            Assert.That(citations[0].Sources[0].Id, Is.EqualTo("doc-1"));
            Assert.That(citations[1].Sources[0].Id, Is.EqualTo("doc-1"));
            Assert.That(citations[1].Start, Is.EqualTo(15));
            Assert.That(citations[1].End, Is.EqualTo(31));
        });
    }

    #endregion

    #region ParseAndStripMarkers — offset correctness after stripping

    [Test]
    public void ParseAndStripMarkers_OffsetsCorrectAfterStripping_WithLeadingText()
    {
        var raw = "Here is the answer: «cite:0»The capital of France is Paris«/cite». Done.";
        var docs = CreateTextDocs();

        var (clean, citations) = GroundedChatFallbackHelper.ParseAndStripMarkers(raw, docs);

        Assert.Multiple(() =>
        {
            Assert.That(clean, Is.EqualTo("Here is the answer: The capital of France is Paris. Done."));
            var cited = clean[citations[0].Start..citations[0].End];
            Assert.That(cited, Is.EqualTo("The capital of France is Paris"));
        });
    }

    [Test]
    public void ParseAndStripMarkers_ConsecutiveCitations_OffsetsCorrect()
    {
        var raw = "«cite:0»Paris«/cite»«cite:1»Berlin«/cite»";
        var docs = CreateTextDocs();

        var (clean, citations) = GroundedChatFallbackHelper.ParseAndStripMarkers(raw, docs);

        Assert.Multiple(() =>
        {
            Assert.That(clean, Is.EqualTo("ParisBerlin"));
            Assert.That(citations[0].Start, Is.EqualTo(0));
            Assert.That(citations[0].End, Is.EqualTo(5));
            Assert.That(citations[1].Start, Is.EqualTo(5));
            Assert.That(citations[1].End, Is.EqualTo(11));
        });
    }

    #endregion

    #region ParseAndStripMarkers — malformed output

    [Test]
    public void ParseAndStripMarkers_NoMarkers_ReturnsContentUnchanged()
    {
        var raw = "The capital of France is Paris.";
        var docs = CreateTextDocs();

        var (clean, citations) = GroundedChatFallbackHelper.ParseAndStripMarkers(raw, docs);

        Assert.Multiple(() =>
        {
            Assert.That(clean, Is.EqualTo(raw));
            Assert.That(citations, Is.Empty);
        });
    }

    [Test]
    public void ParseAndStripMarkers_EmptyContent_ReturnsEmpty()
    {
        var (clean, citations) = GroundedChatFallbackHelper.ParseAndStripMarkers(string.Empty, CreateTextDocs());

        Assert.Multiple(() =>
        {
            Assert.That(clean, Is.Empty);
            Assert.That(citations, Is.Empty);
        });
    }

    [Test]
    public void ParseAndStripMarkers_NullContent_ReturnsEmpty()
    {
        var (clean, citations) = GroundedChatFallbackHelper.ParseAndStripMarkers(null!, CreateTextDocs());

        Assert.Multiple(() =>
        {
            Assert.That(clean, Is.Empty);
            Assert.That(citations, Is.Empty);
        });
    }

    [Test]
    public void ParseAndStripMarkers_UnmatchedOpenMarker_TreatedAsPlainText()
    {
        var raw = "The capital is «cite:0»Paris.";
        var docs = CreateTextDocs();

        var (clean, citations) = GroundedChatFallbackHelper.ParseAndStripMarkers(raw, docs);

        Assert.Multiple(() =>
        {
            Assert.That(clean, Is.EqualTo(raw));
            Assert.That(citations, Is.Empty);
        });
    }

    [Test]
    public void ParseAndStripMarkers_UnmatchedCloseMarker_TreatedAsPlainText()
    {
        var raw = "The capital is Paris«/cite».";
        var docs = CreateTextDocs();

        var (clean, citations) = GroundedChatFallbackHelper.ParseAndStripMarkers(raw, docs);

        Assert.Multiple(() =>
        {
            Assert.That(clean, Is.EqualTo(raw));
            Assert.That(citations, Is.Empty);
        });
    }

    [Test]
    public void ParseAndStripMarkers_InvalidDocIndex_StillExtractsCitation()
    {
        var raw = "«cite:99»Something«/cite» is here.";
        var docs = CreateTextDocs();

        var (clean, citations) = GroundedChatFallbackHelper.ParseAndStripMarkers(raw, docs);

        Assert.Multiple(() =>
        {
            Assert.That(clean, Is.EqualTo("Something is here."));
            Assert.That(citations, Has.Count.EqualTo(1));
            Assert.That(citations[0].Sources[0].Id, Is.EqualTo("document_99"));
            Assert.That(citations[0].Sources[0].Data, Is.Null);
        });
    }

    [Test]
    public void ParseAndStripMarkers_NegativeDocIndex_Ignored()
    {
        var raw = "«cite:-1»Something«/cite» is here.";
        var docs = CreateTextDocs();

        var (clean, citations) = GroundedChatFallbackHelper.ParseAndStripMarkers(raw, docs);

        // -1 doesn't match \d+ so this doesn't parse as a citation
        Assert.Multiple(() =>
        {
            Assert.That(clean, Is.EqualTo(raw));
            Assert.That(citations, Is.Empty);
        });
    }

    #endregion

    #region ParseAndStripMarkers — multi-document attribution

    [Test]
    public void ParseAndStripMarkers_MultiDocAttribution_SourcesResolvedCorrectly()
    {
        var raw = "«cite:0»Paris is in France«/cite», and «cite:1»Berlin is in Germany«/cite».";
        var docs = CreateDataDocs();

        var (clean, citations) = GroundedChatFallbackHelper.ParseAndStripMarkers(raw, docs);

        Assert.Multiple(() =>
        {
            Assert.That(clean, Is.EqualTo("Paris is in France, and Berlin is in Germany."));
            Assert.That(citations, Has.Count.EqualTo(2));

            Assert.That(citations[0].Sources[0].Id, Is.EqualTo("doc-1"));
            Assert.That(citations[0].Sources[0].Data, Is.Not.Null);
            Assert.That(citations[0].Sources[0].Data!["title"], Is.EqualTo("France"));

            Assert.That(citations[1].Sources[0].Id, Is.EqualTo("doc-2"));
            Assert.That(citations[1].Sources[0].Data, Is.Not.Null);
            Assert.That(citations[1].Sources[0].Data!["title"], Is.EqualTo("Germany"));
        });
    }

    [Test]
    public void ParseAndStripMarkers_SourceData_IsDefensiveCopy()
    {
        var mutableData = new Dictionary<string, string> { ["key"] = "original" };
        var docs = new List<DocumentChunk> { new(Id: "doc-0", Data: mutableData) };

        var raw = "«cite:0»text«/cite»";
        var (_, citations) = GroundedChatFallbackHelper.ParseAndStripMarkers(raw, docs);

        mutableData["key"] = "mutated";

        Assert.That(citations[0].Sources[0].Data!["key"], Is.EqualTo("original"));
    }

    [Test]
    public void ParseAndStripMarkers_IdLessDocuments_UseFallbackIds()
    {
        var docs = new List<DocumentChunk>
        {
            new(Text: "First"),
            new(Text: "Second")
        };

        var raw = "«cite:0»A«/cite» and «cite:1»B«/cite».";
        var (_, citations) = GroundedChatFallbackHelper.ParseAndStripMarkers(raw, docs);

        Assert.Multiple(() =>
        {
            Assert.That(citations[0].Sources[0].Id, Is.EqualTo("document_0"));
            Assert.That(citations[1].Sources[0].Id, Is.EqualTo("document_1"));
        });
    }

    #endregion

    #region ParseAndStripMarkers — citation type

    [Test]
    public void ParseAndStripMarkers_Citations_HaveSynthesizedType()
    {
        var raw = "«cite:0»Paris«/cite»";
        var (_, citations) = GroundedChatFallbackHelper.ParseAndStripMarkers(raw, CreateTextDocs());

        Assert.That(citations[0].Type, Is.EqualTo("synthesized_citation"));
    }

    #endregion

    #region ParseAndStripMarkers — multiline cited text

    [Test]
    public void ParseAndStripMarkers_MultilineCitedText_ParsedCorrectly()
    {
        var raw = "Result: «cite:0»Line one\nLine two«/cite».";
        var docs = CreateTextDocs();

        var (clean, citations) = GroundedChatFallbackHelper.ParseAndStripMarkers(raw, docs);

        Assert.Multiple(() =>
        {
            Assert.That(clean, Is.EqualTo("Result: Line one\nLine two."));
            Assert.That(citations[0].Text, Is.EqualTo("Line one\nLine two"));
        });
    }

    #endregion
}
