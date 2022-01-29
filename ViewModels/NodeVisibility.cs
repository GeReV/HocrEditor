using System;
using HocrEditor.Helpers;
using HocrEditor.Models;

namespace HocrEditor.ViewModels;

    public class NodeVisibility : ViewModelBase
    {
        public NodeVisibility(HocrNodeType nodeType)
        {
            NodeType = nodeType;
        }

        public HocrNodeType NodeType { get; }

        public string? IconPath => HocrNodeTypeHelper.GetIcon(NodeType);

        public string? ToolTip => Enum.GetName(NodeType);

        public bool Visible { get; set; } = true;
    }
