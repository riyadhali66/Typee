using Microsoft.Extensions.Logging;
using Typee.Core.Abstractions;
using Typee.Core.Models;

namespace Typee.Core.Phrases;

/// <summary>
/// In-memory phrase trie backed by <see cref="IPhraseRepository"/>.
/// Accept-count: existing phrases increment use_count; new text inserts as accepted after 3 accepts.
/// </summary>
public sealed class PhraseService : IPhraseService
{
    public const int AcceptsBeforeInsert = 3;

    private readonly IPhraseRepository _repository;
    private readonly PhraseTrie _trie = new();
    private readonly Dictionary<string, int> _pendingAccepts = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();
    private readonly TimeProvider _time;
    private readonly ILogger<PhraseService> _logger;
    private bool _initialized;

    public PhraseService(
        IPhraseRepository repository,
        ILogger<PhraseService> logger,
        TimeProvider? time = null)
    {
        _repository = repository;
        _logger = logger;
        _time = time ?? TimeProvider.System;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await ReloadAsync(cancellationToken).ConfigureAwait(false);
    }

    public IReadOnlyList<Phrase> Match(string textBeforeCaret, int maxResults = 5)
    {
        EnsureInitialized();
        return _trie.Match(textBeforeCaret, maxResults);
    }

    public async Task<IReadOnlyList<Phrase>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        return await _repository.GetAllAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<Phrase> AddManualAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        EnsureInitialized();

        var normalized = text.Trim();
        var existing = await _repository.FindByTextAsync(normalized, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing;
        }

        var now = _time.GetUtcNow();
        var phrase = new Phrase
        {
            Text = normalized,
            TriggerPrefix = PhraseTrie.DeriveTriggerPrefix(normalized),
            UseCount = 0,
            LastUsedUtc = now,
            Source = PhraseSource.Manual
        };

        var inserted = await _repository.InsertAsync(phrase, cancellationToken).ConfigureAwait(false);
        _trie.Upsert(inserted);
        _logger.LogInformation("Manual phrase added id={PhraseId}", inserted.Id);
        return inserted;
    }

    public async Task UpdateAsync(Phrase phrase, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(phrase);
        EnsureInitialized();

        var toSave = new Phrase
        {
            Id = phrase.Id,
            Text = phrase.Text.Trim(),
            TriggerPrefix = string.IsNullOrWhiteSpace(phrase.TriggerPrefix)
                ? PhraseTrie.DeriveTriggerPrefix(phrase.Text)
                : phrase.TriggerPrefix.Trim(),
            UseCount = phrase.UseCount,
            LastUsedUtc = phrase.LastUsedUtc ?? _time.GetUtcNow(),
            Source = phrase.Source
        };

        await _repository.UpdateAsync(toSave, cancellationToken).ConfigureAwait(false);
        _trie.Upsert(toSave);
        _logger.LogInformation("Phrase updated id={PhraseId}", toSave.Id);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        await _repository.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        _trie.Remove(id);
        _logger.LogInformation("Phrase deleted id={PhraseId}", id);
    }

    public async Task RecordAcceptAsync(string suggestionText, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(suggestionText);
        EnsureInitialized();

        var normalized = suggestionText.Trim();
        var existing = await _repository.FindByTextAsync(normalized, cancellationToken).ConfigureAwait(false);
        var now = _time.GetUtcNow();

        if (existing is not null)
        {
            var bumped = new Phrase
            {
                Id = existing.Id,
                Text = existing.Text,
                TriggerPrefix = existing.TriggerPrefix,
                UseCount = existing.UseCount + 1,
                LastUsedUtc = now,
                Source = existing.Source
            };
            await _repository.UpdateAsync(bumped, cancellationToken).ConfigureAwait(false);
            _trie.Upsert(bumped);
            _logger.LogInformation("Phrase accept incremented id={PhraseId} useCount={UseCount}", bumped.Id, bumped.UseCount);
            return;
        }

        int pending;
        lock (_gate)
        {
            _pendingAccepts.TryGetValue(normalized, out pending);
            pending++;
            _pendingAccepts[normalized] = pending;
        }

        if (pending < AcceptsBeforeInsert)
        {
            _logger.LogInformation("Pending phrase accept count={Count} (threshold={Threshold})", pending, AcceptsBeforeInsert);
            return;
        }

        var inserted = await _repository.InsertAsync(new Phrase
        {
            Text = normalized,
            TriggerPrefix = PhraseTrie.DeriveTriggerPrefix(normalized),
            UseCount = pending,
            LastUsedUtc = now,
            Source = PhraseSource.Accepted
        }, cancellationToken).ConfigureAwait(false);

        lock (_gate)
        {
            _pendingAccepts.Remove(normalized);
        }

        _trie.Upsert(inserted);
        _logger.LogInformation("Phrase inserted from accepts id={PhraseId} useCount={UseCount}", inserted.Id, inserted.UseCount);
    }

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        var all = await _repository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        _trie.Rebuild(all);
        _initialized = true;
        _logger.LogInformation("Phrase trie loaded count={Count}", all.Count);
    }

    public async Task ImportAsync(IEnumerable<Phrase> phrases, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(phrases);
        EnsureInitialized();

        var imported = 0;
        foreach (var phrase in phrases)
        {
            if (string.IsNullOrWhiteSpace(phrase.Text))
            {
                continue;
            }

            var text = phrase.Text.Trim();
            var existing = await _repository.FindByTextAsync(text, cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                continue;
            }

            var inserted = await _repository.InsertAsync(new Phrase
            {
                Text = text,
                TriggerPrefix = string.IsNullOrWhiteSpace(phrase.TriggerPrefix)
                    ? PhraseTrie.DeriveTriggerPrefix(text)
                    : phrase.TriggerPrefix.Trim(),
                UseCount = Math.Max(0, phrase.UseCount),
                LastUsedUtc = phrase.LastUsedUtc ?? _time.GetUtcNow(),
                Source = phrase.Source == PhraseSource.Accepted ? PhraseSource.Accepted : PhraseSource.Manual
            }, cancellationToken).ConfigureAwait(false);

            _trie.Upsert(inserted);
            imported++;
        }

        _logger.LogInformation("Phrase import finished imported={Count}", imported);
    }

    private void EnsureInitialized()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("PhraseService has not been initialized.");
        }
    }
}
