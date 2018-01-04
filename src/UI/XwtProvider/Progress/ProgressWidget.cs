//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Utilities;
using Xwt;
using Antmicro.Renode.UI;

namespace Antmicro.Renode.UI.Progress
{
    public class ProgressWidget : Widget
    {
        public ProgressWidget()
        {
            ProgressMonitor = new ProgressMonitor { Handler = new ProgressWidgetMonitor(this) };

            progressBar = new ProgressBar();
            text = new Label();
            var box = new HBox();
            box.PackStart(progressBar);
            box.PackStart(text, true);
            Content = box;
        }

        public ProgressMonitor ProgressMonitor { get; private set; }

        private readonly Label text;
        private readonly ProgressBar progressBar;

        private class ProgressWidgetMonitor : IProgressMonitorHandler
        {
            public ProgressWidgetMonitor(ProgressWidget widget)
            {
                this.widget = widget;
            }

            public void Finish(int id)
            {
                ApplicationExtensions.InvokeInUIThread(() =>
                {
                    widget.Visible = false;
                });
            }

            public void Update(int id, string description, int? progress)
            {
                ApplicationExtensions.InvokeInUIThread(() =>
                {
                    widget.Visible = true;
                    widget.text.Text = description;
                    widget.text.Visible = !string.IsNullOrEmpty(description);

                    if(progress.HasValue)
                    {
                        widget.progressBar.Indeterminate = false;
                        widget.progressBar.Fraction = progress.Value / 100.0;
                    }
                    else
                    {
                        widget.progressBar.Indeterminate = true;
                    }
                });
            }

            private readonly ProgressWidget widget;
        }
    }
}

