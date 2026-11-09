using Microsoft.Extensions.Logging.Abstractions;
using Typee.Core.Abstractions;
using Typee.Core.Models;
using Typee.Core.Phrases;

namespace Typee.Core.Tests;

public sealed class PhraseServiceAcceptTests
{
    [Fact]
    public async Task RecordAccept_IncrementsExistingPhrase()
    {
        var repo = new FakePhraseRepository();
        var inserted = await repo.InsertAsync(new Phrase
        {
            Text = "thanks in advance",
            TriggerPrefix = "thanks in advance",
            UseCount = 1,
            Source = PhraseSource.Manual
        });

        var service = new PhraseService(repo, NullLogger<PhraseService>.Instance);
        await service.InitializeAsync();
        await service.RecordAcceptAsync("thanks in advance");

        var updated = await repo.GetByIdAsync(inserted.Id);
        Assert.NotNull(updated);
        Assert.Equal(2, updated.UseCount);
    }

    [Fact]
    public async Task RecordAccept_InsertsAfterThreeAccepts()
    {
        var repo = new FakePhraseRepository();
        var service = new PhraseService(repo, NullLogger<PhraseService>.Instance);
        await service.InitializeAsync();

        await service.RecordAcceptAsync("kind regards");
        await service.RecordAcceptAsync("kind regards");
        Assert.Empty(await repo.GetAllAsync());

        await service.RecordAcceptAsync("kind regards");
        var all = await repo.GetAllAsync();
        Assert.Single(all);
        Assert.Equal(PhraseSource.Accepted, all[0].Source);
        Assert.Equal(3, all[0].UseCount);
        Assert.Equal("kind regards", all[0].Text);
    }

    private sealed class FakePhraseRepository : IPhraseRepository
    {
        private readonly List<Phrase> _items = [];
        private long _nextId = 1;

        public Task<IReadOnlyList<Phrase>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Phrase>>(_items.ToList());

        public Task<Phrase?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.FirstOrDefault(p => p.Id == id));

        public Task<Phrase?> FindByTextAsync(string text, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.FirstOrDefault(p =>
                string.Equals(p.Text, text, StringComparison.OrdinalIgnoreCase)));

        public Task<Phrase> InsertAsync(Phrase phrase, CancellationToken cancellationToken = default)
        {
            var created = new Phrase
            {
                Id = _nextId++,
                Text = phrase.Text,
                TriggerPrefix = phrase.TriggerPrefix,
                UseCount = phrase.UseCount,
                LastUsedUtc = phrase.LastUsedUtc,
                Source = phrase.Source
            };
            _items.Add(created);
            return Task.FromResult(created);
        }

        public Task UpdateAsync(Phrase phrase, CancellationToken cancellationToken = default)
        {
            var index = _items.FindIndex(p => p.Id == phrase.Id);
            if (index >= 0)
            {
                _items[index] = phrase;
            }

            return Task.CompletedTask;
        }

        public Task DeleteAsync(long id, CancellationToken cancellationToken = default)
        {
            _items.RemoveAll(p => p.Id == id);
            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            _items.Clear();
            return Task.CompletedTask;
        }
    }
}
