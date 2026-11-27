using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.NUnit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Cisharpai.Tests;

/// <summary>The type rules A1 to A5, checked on the compiled assemblies.</summary>
[Category("Architecture")]
public class ArchitectureTests
{
    private static readonly Architecture Architecture =
        new ArchLoader()
            .LoadAssemblies(
                typeof(LlmHttpClient).Assembly,
                typeof(global::Cisharpai.OpenAi.OpenAiChatCompletionClient).Assembly,
                typeof(global::Cisharpai.Anthropic.AnthropicChatCompletionClient).Assembly,
                typeof(global::Cisharpai.Azure.AzureOpenAi.AzureOpenAiChatCompletionClient).Assembly,
                typeof(global::Cisharpai.Cohere.CohereChatCompletionClient).Assembly)
            .Build();

    [Test]
    public void A1_Core_does_not_depend_on_any_provider()
    {
        IArchRule rule = Types().That()
            .ResideInNamespaceMatching(@"^Cisharpai(\..*)?$")
            .And().DoNotResideInNamespaceMatching(@"^Cisharpai\.(OpenAi|Anthropic|Azure|Cohere|Rag|Testing|Tests)(\..*)?$")
            .Should().NotDependOnAny(
                Types().That().ResideInNamespaceMatching(@"^Cisharpai\.(OpenAi|Anthropic|Azure|Cohere)(\..*)?$"))
            .Because("A1: Core abstractions must not depend on any provider implementation");
        ArchRuleAssert.FulfilsRule(Architecture, rule);
    }

    [Test]
    public void A2_Providers_do_not_depend_on_each_other()
    {
        IArchRule rule = Types().That()
            .ResideInNamespaceMatching(@"^Cisharpai\.OpenAi(\..*)?$")
            .Should().NotDependOnAny(
                Types().That().ResideInNamespaceMatching(@"^Cisharpai\.(Anthropic|Azure|Cohere)(\..*)?$"))
            .AndShould().NotDependOnAny(
                Types().That().ResideInNamespaceMatching(@"^Cisharpai\.(Anthropic|Azure|Cohere)(\..*)?$"))
            .Because("A2: each provider is independent; switching providers is a configuration change");
        ArchRuleAssert.FulfilsRule(Architecture, rule);
    }

    [Test]
    public void A3_Anthropic_does_not_depend_on_other_providers()
    {
        IArchRule rule = Types().That()
            .ResideInNamespaceMatching(@"^Cisharpai\.Anthropic(\..*)?$")
            .Should().NotDependOnAny(
                Types().That().ResideInNamespaceMatching(@"^Cisharpai\.(OpenAi|Azure|Cohere)(\..*)?$"))
            .Because("A3: Anthropic provider is independent of other providers");
        ArchRuleAssert.FulfilsRule(Architecture, rule);
    }

    [Test]
    public void A4_Cohere_does_not_depend_on_other_providers()
    {
        IArchRule rule = Types().That()
            .ResideInNamespaceMatching(@"^Cisharpai\.Cohere(\..*)?$")
            .Should().NotDependOnAny(
                Types().That().ResideInNamespaceMatching(@"^Cisharpai\.(OpenAi|Anthropic|Azure)(\..*)?$"))
            .Because("A4: Cohere provider is independent of other providers");
        ArchRuleAssert.FulfilsRule(Architecture, rule);
    }

    [Test]
    public void A5_Azure_does_not_depend_on_non_Azure_providers()
    {
        IArchRule rule = Types().That()
            .ResideInNamespaceMatching(@"^Cisharpai\.Azure(\..*)?$")
            .Should().NotDependOnAny(
                Types().That().ResideInNamespaceMatching(@"^Cisharpai\.(OpenAi|Anthropic|Cohere)(\..*)?$"))
            .Because("A5: Azure provider is independent of non-Azure providers");
        ArchRuleAssert.FulfilsRule(Architecture, rule);
    }
}
