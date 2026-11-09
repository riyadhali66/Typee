using CommunityToolkit.Mvvm.ComponentModel;
using Typee.Core.Models;

namespace Typee.App.ViewModels;

public partial class DictionaryRowViewModel : ObservableObject
{
    public DictionaryRowViewModel(DictionaryWord word)
    {
        Word = word.Word;
        AddedUtc = word.AddedUtc;
    }

    [ObservableProperty]
    private string _word = string.Empty;

    public DateTimeOffset AddedUtc { get; }
}
