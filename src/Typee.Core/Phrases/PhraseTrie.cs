using Typee.Core.Models;

namespace Typee.Core.Phrases;

/// <summary>
/// In-memory prefix trie over phrase trigger prefixes / texts.
/// Matching uses the last 1–5 whitespace-separated words before the caret.
/// </summary>
public sealed class PhraseTrie
{
    private readonly TrieNode _root = new();
    private readonly Dictionary<long, Phrase> _byId = new();
    private readonly object _gate = new();

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _byId.Count;
            }
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _root.Children.Clear();
            _root.PhraseIds.Clear();
            _byId.Clear();
        }
    }

    public void Rebuild(IEnumerable<Phrase> phrases)
    {
        ArgumentNullException.ThrowIfNull(phrases);
        lock (_gate)
        {
            _root.Children.Clear();
            _root.PhraseIds.Clear();
            _byId.Clear();
            foreach (var phrase in phrases)
            {
                InsertUnlocked(phrase);
            }
        }
    }

    public void Upsert(Phrase phrase)
    {
        ArgumentNullException.ThrowIfNull(phrase);
        lock (_gate)
        {
            if (_byId.TryGetValue(phrase.Id, out var existing))
            {
                RemoveKeysUnlocked(existing);
            }

            InsertUnlocked(phrase);
        }
    }

    public void Remove(long id)
    {
        lock (_gate)
        {
            if (!_byId.Remove(id, out var existing))
            {
                return;
            }

            RemoveKeysUnlocked(existing);
        }
    }

    /// <summary>
    /// Finds phrases whose trigger prefix or text starts with any of the last 1–5 words
    /// before the caret. Ranked by use_count desc, then last_used desc.
    /// </summary>
    public IReadOnlyList<Phrase> Match(string textBeforeCaret, int maxResults = 5)
    {
        if (string.IsNullOrWhiteSpace(textBeforeCaret) || maxResults <= 0)
        {
            return [];
        }

        var queries = BuildTrailingQueries(textBeforeCaret);
        if (queries.Count == 0)
        {
            return [];
        }

        lock (_gate)
        {
            var found = new Dictionary<long, Phrase>();
            foreach (var query in queries)
            {
                foreach (var phrase in CollectPrefixMatchesUnlocked(query))
                {
                    found[phrase.Id] = phrase;
                }
            }

            return found.Values
                .OrderByDescending(static p => p.UseCount)
                .ThenByDescending(static p => p.LastUsedUtc ?? DateTimeOffset.MinValue)
                .ThenBy(static p => p.Id)
                .Take(maxResults)
                .ToList();
        }
    }

    /// <summary>Builds longest-first trailing word queries (1–5 words).</summary>
    public static IReadOnlyList<string> BuildTrailingQueries(string textBeforeCaret)
    {
        if (string.IsNullOrWhiteSpace(textBeforeCaret))
        {
            return [];
        }

        var words = SplitWords(textBeforeCaret);
        if (words.Count == 0)
        {
            return [];
        }

        var take = Math.Min(5, words.Count);
        var queries = new List<string>(take);
        for (var n = take; n >= 1; n--)
        {
            var slice = words.Skip(words.Count - n);
            queries.Add(string.Join(' ', slice));
        }

        return queries;
    }

    public static string DeriveTriggerPrefix(string phraseText)
    {
        if (string.IsNullOrWhiteSpace(phraseText))
        {
            return string.Empty;
        }

        var words = SplitWords(phraseText);
        if (words.Count == 0)
        {
            return string.Empty;
        }

        var take = Math.Min(3, words.Count);
        return string.Join(' ', words.Take(take)).ToLowerInvariant();
    }

    private void InsertUnlocked(Phrase phrase)
    {
        _byId[phrase.Id] = phrase;
        foreach (var key in KeysFor(phrase))
        {
            InsertKeyUnlocked(key, phrase.Id);
        }
    }

    private void RemoveKeysUnlocked(Phrase phrase)
    {
        foreach (var key in KeysFor(phrase))
        {
            RemoveKeyUnlocked(key, phrase.Id);
        }
    }

    private static IEnumerable<string> KeysFor(Phrase phrase)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(phrase.TriggerPrefix))
        {
            keys.Add(Normalize(phrase.TriggerPrefix));
        }

        if (!string.IsNullOrWhiteSpace(phrase.Text))
        {
            keys.Add(Normalize(phrase.Text));
            var derived = DeriveTriggerPrefix(phrase.Text);
            if (!string.IsNullOrEmpty(derived))
            {
                keys.Add(derived);
            }
        }

        return keys;
    }

    private void InsertKeyUnlocked(string key, long phraseId)
    {
        var node = _root;
        foreach (var ch in key)
        {
            if (!node.Children.TryGetValue(ch, out var child))
            {
                child = new TrieNode();
                node.Children[ch] = child;
            }

            node = child;
        }

        node.PhraseIds.Add(phraseId);
    }

    private void RemoveKeyUnlocked(string key, long phraseId)
    {
        var node = _root;
        var stack = new Stack<(TrieNode Parent, char Key, TrieNode Node)>();
        foreach (var ch in key)
        {
            if (!node.Children.TryGetValue(ch, out var child))
            {
                return;
            }

            stack.Push((node, ch, child));
            node = child;
        }

        node.PhraseIds.Remove(phraseId);
        while (stack.Count > 0)
        {
            var (parent, ch, current) = stack.Pop();
            if (current.PhraseIds.Count > 0 || current.Children.Count > 0)
            {
                break;
            }

            parent.Children.Remove(ch);
        }
    }

    private IEnumerable<Phrase> CollectPrefixMatchesUnlocked(string query)
    {
        var node = _root;
        foreach (var ch in Normalize(query))
        {
            if (!node.Children.TryGetValue(ch, out var child))
            {
                yield break;
            }

            node = child;
        }

        var stack = new Stack<TrieNode>();
        stack.Push(node);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            foreach (var id in current.PhraseIds)
            {
                if (_byId.TryGetValue(id, out var phrase))
                {
                    yield return phrase;
                }
            }

            foreach (var child in current.Children.Values)
            {
                stack.Push(child);
            }
        }
    }

    private static List<string> SplitWords(string text)
    {
        return text
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static w => w.Length > 0)
            .ToList();
    }

    private static string Normalize(string value) => value.Trim().ToLowerInvariant();

    private sealed class TrieNode
    {
        public Dictionary<char, TrieNode> Children { get; } = new();
        public HashSet<long> PhraseIds { get; } = [];
    }
}
