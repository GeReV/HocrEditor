using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using HocrEditor.Commands.UndoRedo;
using HocrEditor.Core;
using HocrEditor.Helpers;
using HocrEditor.Services;
using HocrEditor.Tesseract;
using HocrEditor.ViewModels;
using HtmlAgilityPack;
using Rect = HocrEditor.Models.Rect;

namespace HocrEditor.Commands;

public class OcrRegionCommand : UndoableAsyncCommandBase<Rect>
{
    private readonly HocrPageViewModel hocrPageViewModel;

    public OcrRegionCommand(HocrPageViewModel hocrPageViewModel) : base(hocrPageViewModel)
    {
        this.hocrPageViewModel = hocrPageViewModel;
    }

    protected override bool IsCancelable => true;

    protected override bool CanExecuteImpl(Rect selectionBounds) => selectionBounds.Width > 0 && selectionBounds.Height > 0;

    protected override Task ExecuteAsyncImpl(Rect selectionBounds, CancellationToken? cancellationToken)
    {
        var tesseractPath = Settings.TesseractPath;

        if (tesseractPath == null)
        {
            return Task.CompletedTask;
        }

        var region = new Rectangle(
            selectionBounds.Left,
            selectionBounds.Top,
            selectionBounds.Width,
            selectionBounds.Height
        );

        return Task.Run(
                async () =>
                {
                    using var service = new TesseractService(tesseractPath, Settings.TesseractSelectedLanguages);

                    ArgumentNullException.ThrowIfNull(hocrPageViewModel.Image.Value);

                    var body = await service.Recognize(
                        hocrPageViewModel.Image.Value,
                        hocrPageViewModel.ImageFilename,
                        region
                    );

                    var doc = new HtmlDocument();
                    doc.LoadHtml(body);

                    var hocrDocument = new HocrParser().Parse(doc);

                    hocrDocument.OcrSystem = $"Tesseract {service.GetVersion()}";

                    return hocrDocument;
                }
            )
            .ContinueWith(
                async hocrDocumentTask =>
                {
                    try
                    {
                        if (cancellationToken is { IsCancellationRequested: true })
                        {
                            return;
                        }

                        var hocrDocument = await hocrDocumentTask;

                        Debug.Assert(hocrDocument.Pages.Count == 1);

                        var tempPage = new HocrPageViewModel(hocrPageViewModel.ImageFilename);

                        tempPage.Build(hocrDocument.Pages.First());

                        var sourceRootNode = tempPage.Nodes.First(n => n.IsRoot);

                        var descendants = sourceRootNode.Descendants.ToList();

                        // Tesseract might not get any results.
                        if (descendants.Count == 0)
                        {
                            return;
                        }

                        var commands = new List<UndoRedoCommand>();

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

                        if (cancellationToken is { IsCancellationRequested: true })
                        {
                            return;
                        }

                        UndoRedoManager.ExecuteCommands(commands);
                    }
                    catch (Exception ex)
                    {
                        // TODO: Improve error handling.
                        MessageBox.Show($"{ex.Message}\n{ex.Source}\n\n{ex.StackTrace}");
                    }
                },
                TaskScheduler.FromCurrentSynchronizationContext()
            );
    }
}
