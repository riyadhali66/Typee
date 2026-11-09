using CommunityToolkit.Mvvm.ComponentModel;
using Typee.Core.Models;

namespace Typee.App.ViewModels;

public partial class PhraseRowViewModel : ObservableObject
{
    public PhraseRowViewModel(Phrase phrase)
    {
        Id = phrase.Id;
        Text = phrase.Text;
        TriggerPrefix = phrase.TriggerPrefix;
        UseCount = phrase.UseCount;
        Source = phrase.Source.ToString();
    }

    public long Id { get; }

    [ObservableProperty]
    private string _text = string.Empty;

    [ObservableProperty]
    private string _triggerPrefix = string.Empty;

    [ObservableProperty]
    private int _useCount;

    [ObservableProperty]
    private string _source = string.Empty;
}
