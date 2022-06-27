//
// Copyright (c) 2010-2022 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Xwt;
using System.IO;

namespace Antmicro.Renode.Plugins.AdvancedLoggerViewer
{
    public class LogViewerHelpDialog : Dialog
    {
        public LogViewerHelpDialog()
        {
            var label = new Label("Log viewer help");
            label.Font = label.Font.WithSize(15).WithWeight(Xwt.Drawing.FontWeight.Bold);

            var markdown = new MarkdownView();
            using(var stream = typeof(LogViewerHelpDialog).Assembly.GetManifestResourceStream("Antmicro.Renode.Plugins.AdvancedLoggerViewer.LogViewerHelpFile.txt"))
            {
                using(var reader = new StreamReader(stream))
                {
                    markdown.Markdown = reader.ReadToEnd();
                }
            }

            var box = new VBox();
            box.PackStart(label);
            box.PackStart(markdown, true);

            Content = box;
            Buttons.Add(new DialogButton(Command.Ok));

            Width = 1000;
            Height = 300;
        }
    }
}

