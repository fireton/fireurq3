using Urql.Core.Runtime;

namespace Urql.Runner.MonoGame.Runner;

internal sealed class ChoicePresenter
{
    private List<ButtonAction> _choices = [];

    public IReadOnlyList<ButtonAction> ActiveChoices => _choices;

    public void SetChoices(IReadOnlyList<ButtonAction> choices)
    {
        _choices = choices.ToList();
    }

    public void Clear()
    {
        _choices = [];
    }

    public ButtonAction? FindById(int buttonId)
    {
        return _choices.FirstOrDefault(c => c.Id == buttonId);
    }
}
