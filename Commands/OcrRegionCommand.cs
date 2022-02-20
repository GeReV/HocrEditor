using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using HocrEditor.Commands.UndoRedo;
using HocrEditor.Core;
using HocrEditor.Helpers;
using HocrEditor.Services;
using HocrEditor.ViewModels;
using HtmlAgilityPack;

namespace HocrEditor.Commands;

public class OcrRegionCommand : UndoableCommandBase<Models.Rect>
{
    private readonly HocrPageViewModel hocrPageViewModel;

    public OcrRegionCommand(HocrPageViewModel hocrPageViewModel) : base(hocrPageViewModel)
    {
        this.hocrPageViewModel = hocrPageViewModel;
    }

    public override bool CanExecute(Models.Rect selectionBounds) => selectionBounds.Width > float.Epsilon && selectionBounds.Height > float.Epsilon;

    public override void Execute(Models.Rect selectionBounds)
    {
        var tesseractPath = Settings.TesseractPath;

        if (tesseractPath == null)
        {
            return;
        }

        var region = new Rectangle(
            selectionBounds.Left,
            selectionBounds.Top,
            selectionBounds.Width,
            selectionBounds.Height
        );

        Task.Run(
                async () =>
                {
                    var service = new TesseractService(tesseractPath);

                    var body = await service.PerformOcrRegion(
                        hocrPageViewModel.Image,
                        region,
                        Settings.TesseractSelectedLanguages
                    );

                    var doc = new HtmlDocument();
                    doc.LoadHtml(body);

                    return new HocrParser().Parse(doc);
                }
            )
            .ContinueWith(
                async hocrDocumentTask =>
                {
                    try
                    {
                        var hocrDocument = await hocrDocumentTask;

                        Debug.Assert(hocrDocument.Pages.Count == 1);

                        var tempPage = new HocrPageViewModel(hocrPageViewModel.Image);

                        tempPage.Build(hocrDocument.Pages.First());

                        var sourceRootNode = tempPage.Nodes.First(n => n.IsRoot);

                        var descendants = sourceRootNode.Descendants.ToList();

                        var commands = new List<UndoRedoCommand>();

                        foreach (var node in descendants)
                        {
                            var bbox = node.BBox;

                            bbox.Offset(selectionBounds.Location);

                            commands.Add(PropertyChangeCommand.FromProperty(node, n => n.BBox, bbox));
                        }

                        var pageRootNode = hocrPageViewModel.Nodes.First(n => n.IsRoot);

                        foreach (var node in sourceRootNode.Children)
                        {
                            // NOTE: Careful with putting this inside PropertyChangeCommand.FromProperty:
                            //  Passing it as a function and not as a value by mistake will probably break things.
                            //  (i.e. `PropertyChangeCommand.FromProperty(node, n => n.Id, hocrPageViewModel.NextId);`, without the execution parentheses).
                            var id = hocrPageViewModel.NextId();

                            commands.Add(
                                PropertyChangeCommand.FromProperty(node, n => n.Id, id)
                            );

                            commands.Add(PropertyChangeCommand.FromProperty(node, n => n.Parent, pageRootNode));

                            commands.Add(pageRootNode.Children.ToCollectionAddCommand(node));

                            foreach (var descendant in node.Descendants)
                            {
                                descendant.Id = hocrPageViewModel.NextId();
                            }
                        }

                        commands.Add(hocrPageViewModel.Nodes.ToCollectionAddCommand(descendants));

                        commands.Add(hocrPageViewModel.SelectedNodes.ToCollectionAddCommand(sourceRootNode.Children));

                        UndoRedoManager.ExecuteCommands(commands);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"{ex.Message}\n{ex.Source}\n\n{ex.StackTrace}");
                    }
                },
                TaskScheduler.FromCurrentSynchronizationContext()
            );
    }
}
