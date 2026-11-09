using Typee.Core.Models;
using Typee.Core.Phrases;

namespace Typee.Core.Tests;

public sealed class PhraseTrieTests
{
    [Fact]
    public void Match_UsesLastOneToFiveWords_AndRanksByUseThenRecency()
    {
        var trie = new PhraseTrie();
        var older = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var newer = DateTimeOffset.Parse("2026-02-01T00:00:00Z");

        trie.Rebuild(
        [
            new Phrase
            {
                Id = 1,
                Text = "looking forward to hearing from you",
                TriggerPrefix = "looking forward to",
                UseCount = 2,
                LastUsedUtc = older,
                Source = PhraseSource.Manual
            },
            new Phrase
            {
                Id = 2,
                Text = "looking forward to your reply",
                TriggerPrefix = "looking forward to",
                UseCount = 5,
                LastUsedUtc = older,
                Source = PhraseSource.Accepted
            },
            new Phrase
            {
                Id = 3,
                Text = "looking forward to catching up",
                TriggerPrefix = "looking forward to",
                UseCount = 5,
                LastUsedUtc = newer,
                Source = PhraseSource.Manual
            }
        ]);

        var matches = trie.Match("I am looking forward to");

        Assert.Equal(3, matches.Count);
        Assert.Equal(3, matches[0].Id); // same use_count, more recent
        Assert.Equal(2, matches[1].Id);
        Assert.Equal(1, matches[2].Id);
    }

    [Fact]
    public void BuildTrailingQueries_LongestFirst_UpToFiveWords()
    {
        var queries = PhraseTrie.BuildTrailingQueries("one two three four five six");
        Assert.Equal(
        [
            "two three four five six",
            "three four five six",
            "four five six",
            "five six",
            "six"
        ],
        queries);
    }

    [Fact]
    public void UpsertAndRemove_UpdateMatchResults()
    {
        var trie = new PhraseTrie();
        var phrase = new Phrase
        {
            Id = 10,
            Text = "best regards",
            TriggerPrefix = "best regards",
            UseCount = 1,
            Source = PhraseSource.Manual
        };
        trie.Upsert(phrase);
        Assert.Single(trie.Match("best"));

        trie.Remove(10);
        Assert.Empty(trie.Match("best"));
    }
}
