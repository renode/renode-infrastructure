//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
#if PLATFORM_LINUX
using System.Diagnostics;

namespace Antmicro.Renode.UI
{
    [ConsoleBackendAnalyzerProvider("XTerm")]
    public class XTermProvider : ProcessBasedProvider
    {
        protected override Process CreateProcess(string consoleName, string command)
        {
            var p = new Process();
            var position = WindowPositionProvider.Instance.GetNextPosition();
            var minFaceSize = @"XTerm.*.faceSize1: 6";
            var keys = @"XTerm.VT100.translations: #override \\n" +
                        // disable menu on CTRL click
                        @"!Ctrl <Btn1Down>: ignore()\\n" +
                        @"!Ctrl <Btn2Down>: ignore()\\n" +
                        @"!Ctrl <Btn3Down>: ignore()\\n" +
                        @"!Lock Ctrl <Btn1Down>: ignore()\\n" +
                        @"!Lock Ctrl <Btn2Down>: ignore()\\n" +
                        @"!Lock Ctrl <Btn3Down>: ignore()\\n" +
                        @"!@Num_Lock Ctrl <Btn1Down>: ignore()\\n" +
                        @"!@Num_Lock Ctrl <Btn2Down>: ignore()\\n" +
                        @"!@Num_Lock Ctrl <Btn3Down>: ignore()\\n" +
                        @"!Lock Ctrl @Num_Lock <Btn1Down>: ignore()\\n" +
                        @"!Lock Ctrl @Num_Lock <Btn2Down>: ignore()\\n" +
                        @"!Lock Ctrl @Num_Lock <Btn3Down>: ignore()\\n" +
                        // change default font size change keys into CTRL +/-
                        @"Shift~Ctrl <KeyPress> KP_Add:ignore()\\n" +
                        @"Shift Ctrl <KeyPress> KP_Add:ignore()\\n" +
                        @"Shift <KeyPress> KP_Subtract:ignore()\\n" +
                        @"Ctrl <KeyPress> KP_Subtract:smaller-vt-font()\\n" +
                        @"Ctrl <KeyPress> KP_Add:larger-vt-font() \\n";
            var scrollKeys = @"XTerm.VT100.scrollbar.translations: #override \\n"+
                                @"<Btn5Down>: StartScroll(Forward) \\n"+
                                @"<Btn1Down>: StartScroll(Continuous) MoveThumb() NotifyThumb() \\n"+
                                @"<Btn4Down>: StartScroll(Backward) \\n"+
                                @"<Btn3Down>: StartScroll(Continuous) MoveThumb() NotifyThumb() \\n"+
                                @"<Btn2Down>: ignore() \\n"+
                                @"<Btn1Motion>: MoveThumb() NotifyThumb() \\n"+
                                @"<BtnUp>: NotifyScroll(Proportional) EndScroll()";
            var fonts = "DejaVu Sans Mono, Ubuntu Sans Mono, Droid Sans Mono";

            var xtermCommand = string.Format(@"-T '{0}' -sb -rightbar -xrm '*Scrollbar.thickness: 10' -xrm '*Scrollbar.background: #CCCCCC' -geometry +{1}+{2}  -xrm '*Scrollbar.foreground: #444444' -xrm 'XTerm.vt100.background: black' -xrm 'XTerm.vt100.foreground: white' -fa '{3}' -fs 10 -xrm '{4}' -xrm '{5}' -xrm '{6}' -e {7}",
                consoleName, (int)position.X, (int)position.Y, fonts, keys, minFaceSize, scrollKeys, command);

            p.StartInfo = new ProcessStartInfo("xterm", xtermCommand)
            {
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
            };
            p.EnableRaisingEvents = true;
            p.Exited += (sender, e) =>
            {
                var proc = sender as Process;
                if(proc.ExitCode != 0 && proc.ExitCode != 15)
                {
                    LogError("Xterm", xtermCommand, proc.ExitCode);
                }
                InnerOnClose();
            };
            return p;
        }
    }
}
#endif
