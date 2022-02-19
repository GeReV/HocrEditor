using System;
using HocrEditor.Helpers;
using HocrEditor.Models;

namespace HocrEditor.ViewModels;

    public class NodeVisibility : ViewModelBase
    {
        public NodeVisibility(HocrNodeTypeViewModel nodeTypeViewModel)
        {
            NodeTypeViewModel = nodeTypeViewModel;
        }

        public HocrNodeTypeViewModel NodeTypeViewModel { get; }

        public bool Visible { get; set; } = true;

        public override void Dispose()
        {
            // No-op.
        }
    }
