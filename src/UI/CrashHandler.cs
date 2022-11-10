//
// Copyright (c) 2010-2022 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.IO;
using Antmicro.Renode.Utilities;
using Xwt;
using System.Text;
using System.Linq;
using System.Threading;

namespace Antmicro.Renode.UI 
{
    public class CrashHandler
    {
        public static void HandleCrash(Exception e)
        {
            var message = GetFullStackTrace(e);
            SaveErrorToFile(TemporaryFilesManager.Instance.EmulatorTemporaryPath + TemporaryFilesManager.CrashSuffix, message);
            ShowErrorInConsole(message);
            try 
            {
                ApplicationExtensions.InvokeInUIThreadAndWait(() => ShowErrorWindow(message));
            }
            catch(Exception)
            {
                // there is nothing to do here    
            }
        }

        private static void ShowErrorWindow(string message)
        {
            var dialog = new Dialog();
            dialog.Title = "Fatal error";
            var markdown = new MarkdownView();
            markdown.Markdown = message.Split(new [] { '\n' }).Select(x => "\t" + x).Aggregate((x, y) => x + "\n" + y);

            var copyButton = new Button("Copy to clipboard");
            copyButton.Clicked += (sender, ev) => Clipboard.SetText(message);

            var box = new VBox();

            box.PackStart(new Label("Got unhandled exception") { Font = global::Xwt.Drawing.Font.SystemFont.WithSize(15).WithWeight(Xwt.Drawing.FontWeight.Bold) });
            box.PackStart(new ScrollView(markdown), true, true);
            box.PackStart(copyButton);

            dialog.Content = box;

            dialog.Buttons.Add(new DialogButton(Command.Ok));
            dialog.Width = 350;
            dialog.Height = 300;

            dialog.Run();
            dialog.Dispose();
        }

        private static void SaveErrorToFile(string location, string message)
        {
            Directory.CreateDirectory(location);
            var filename = CustomDateTime.Now.ToString("yyyyMMddHHmmssfff");
            File.AppendAllText(Path.Combine(location, filename), message);
        }

        private static void ShowErrorInConsole(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine("Fatal error:");
            Console.Error.WriteLine(message);
            Console.ResetColor();
        }

        private static string GetFullStackTrace(Exception e)
        {
            var result = new StringBuilder();
            var current = e;
            while(current != null)
            {
                result.AppendLine(current.Message);
                result.AppendLine(current.StackTrace);
                current = current.InnerException;
                if(current != null)
                {
                    result.AppendLine("Inner exception:");
                }
            }
            return result.ToString();
        }
    }
}

