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

    public override bool CanExecute(Models.Rect selectionBounds) => !selectionBounds.IsEmpty;

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
                        new[] { "script/Hebrew", "eng" }
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

                        var page = new HocrPageViewModel(hocrPageViewModel.Image);

                        page.Build(hocrDocument.Pages.First());

                        var pRootNode = page.Nodes.First(n => n.IsRoot);

                        var descendants = pRootNode.Descendents.ToList();

                        var commands = new List<UndoRedoCommand>();

                        foreach (var node in descendants)
                        {
                            var bbox = node.BBox;

                            bbox.Offset(selectionBounds.Location);

                            commands.Add(PropertyChangeCommand.FromProperty(node, n => n.BBox, bbox));
                        }

                        var pageRootNode = hocrPageViewModel.Nodes.First(n => n.IsRoot);


                        foreach (var node in pRootNode.Children)
                        {
                            commands.Add(
                                PropertyChangeCommand.FromProperty(node, n => n.Id, ++hocrPageViewModel.LastId)
                            );

                            commands.Add(PropertyChangeCommand.FromProperty(node, n => n.Parent, pageRootNode));

                            commands.Add(pageRootNode.Children.ToCollectionAddCommand(node));

                            foreach (var descendant in node.Descendents)
                            {
                                descendant.Id = ++hocrPageViewModel.LastId;
                            }
                        }

                        commands.Add(hocrPageViewModel.Nodes.ToCollectionAddCommand(descendants));

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
