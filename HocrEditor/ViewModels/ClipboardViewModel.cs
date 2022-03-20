using System.Collections.Generic;
using System.Linq;
using HocrEditor.Helpers;

namespace HocrEditor.ViewModels;

public class ClipboardViewModel : ViewModelBase
{
    private IList<HocrNodeViewModel> data = new List<HocrNodeViewModel>();

    public bool HasData => data.Count > 0;

    public void Clear()
    {
        data = new List<HocrNodeViewModel>();

        OnPropertyChanged(nameof(HasData));
    }

    public void SetData(IEnumerable<HocrNodeViewModel> nodes)
    {
        var list = nodes.ToList();

        if (list.Count == 0)
        {
            Clear();
            return;
        }

        data = NodeHelpers.CloneNodeCollection(list);

        OnPropertyChanged(nameof(HasData));
    }

    public IList<HocrNodeViewModel> GetData() => data;
}
