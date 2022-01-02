using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;


namespace HocrEditor.ViewModels
{
    public class HocrTreeNodeViewModel : ViewModelBase
    {
        private ObservableCollection<HocrTreeNodeViewModel> children = new();
        private readonly Lazy<List<HocrTreeNodeViewModel>> childrenLoader;

        public HocrTreeNodeViewModel(HocrNodeViewModel node, HocrTreeNodeViewModel? parent = null)
        {
            HocrNode = node;

            Id = node.Id;
            Parent = parent;

            //Wrap loader for the nested view model inside a lazy so we can control when it is invoked
            childrenLoader = new Lazy<List<HocrTreeNodeViewModel>>(
                () => node.Children
                    .Select(e => new HocrTreeNodeViewModel(e, this))
                    .ToList()
            );

            //return true when the children should be loaded
            //(i.e. if current node is a root, otherwise when the parent expands)
            if (Parent != null)
            {
                Parent.PropertyChanged += (_, args) =>
                {
                    if (args is not { PropertyName: nameof(HocrNodeViewModel.IsExpanded) })
                    {
                        return;
                    }

                    if (Parent.IsExpanded)
                    {
                        LoadChildren();
                    }
                };
            }
            else
            {
                LoadChildren();
            }
        }

        private void LoadChildren()
        {
            foreach (var child in childrenLoader.Value)
            {
                children.Add(child);
            }
        }

        public bool IsExpanded { get; private set; } = false;

        public HocrNodeViewModel HocrNode { get; }

        public string Id { get; }
        public HocrTreeNodeViewModel? Parent { get; }

        public ObservableCollection<HocrTreeNodeViewModel> Children => children;
    }
}
