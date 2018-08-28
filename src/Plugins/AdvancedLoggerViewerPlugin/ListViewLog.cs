//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Xwt;
using Xwt.Drawing;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Plugins.AdvancedLoggerViewer
{
    public class ListViewLog : ListView
    {
        public ListViewLog(bool squeezeEntries)
        {
            SqueezeEntries = squeezeEntries;

            infoIcon = StockIcons.Information.WithSize(IconSize.Small);
            warningIcon = StockIcons.Warning.WithSize(IconSize.Small);
            errorIcon = StockIcons.Error.WithSize(IconSize.Small);

            imgField = new DataField<Image>();
            typeField = new DataField<string>();
            timestampField = new DataField<string>();
            textField = new DataField<string>();
            objectField = new DataField<string>();
            machineField = new DataField<string>();
            idField = new DataField<ulong>();

            listStore = new ListStore(imgField, objectField, timestampField, typeField, textField, machineField, idField);

            DataSource = listStore;
            Columns.Add("Id", idField);
            Columns.Add("Type", imgField, typeField);
            Columns.Add("Timestamp", timestampField);
            Columns.Add("Source", objectField);
            Columns.Add("Machine", machineField);
            Columns.Add("Text", textField);

#if !PLATFORM_WINDOWS
            var gtkScrolledWindow = (Gtk.ScrolledWindow)Toolkit.CurrentEngine.GetNativeWidget(this);
            gtkScrolledWindow.Child.CanFocus = false; //can only set this inside GTK since there is a ScrolledWindow wrapper... - should be fixed in XWT

            gtkScrolledWindow.Vadjustment.ValueChanged += (sender, e) => 
            {
                var s = Scrolled;
                if (s == null)
                {
                    return;
                }

                if(gtkScrolledWindow.Vadjustment.Value < Epsilon)
                {
                    s(ScrolledState.ScrolledUp);
                }
                else if ((gtkScrolledWindow.Vadjustment.Upper - gtkScrolledWindow.Vadjustment.PageSize) - gtkScrolledWindow.Vadjustment.Value < Epsilon)
                {
                    s(ScrolledState.ScrolledDown);
                }
            };
#endif
            BorderVisible = false;
        }

        public int AddItem(LogEntry entry, bool scrollOnRefresh, int? limit = null)
        {
            int rowId;

            if(SqueezeEntries && entry.EqualsWithoutIdAndTime(lastLogEntry))
            {
                rowId = listStore.RowCount - 1;

                listStore.SetValue(rowId, idField, entry.Id);
                listStore.SetValue(rowId, textField, string.Format("{0} ({1})", entry.Message, ++lastLogEntryCounter));
                listStore.SetValue(rowId, timestampField, string.Format("{0:HH:mm:ss.ffff}", entry.Time));

                return rowId;
            }

            rowId = listStore.AddRow();

            listStore.SetValues(rowId, 
                imgField,       LogLevelToIcon(entry.Type),
                typeField,      entry.Type.ToString(),
                machineField,   entry.MachineName ?? string.Empty,
                objectField,    entry.ObjectName ?? string.Empty,
                textField,      entry.Message,
                timestampField, string.Format("{0:HH:mm:ss.ffff}", entry.Time),
                idField,        entry.Id);

            if(scrollOnRefresh || entry.Id == SelectedItemId)
            {
                SelectRow(rowId);
            }
            while (limit.HasValue && limit != 0 && listStore.RowCount > limit)
            {
                listStore.RemoveRow(0);
            }

            lastLogEntry = entry;
            lastLogEntryCounter = 1;
            return rowId;
        }

        public int InsertItem(LogEntry entry, bool scrollOnRefresh, int? limit = null)
        {
            var rowId = listStore.InsertRowBefore(0);

            listStore.SetValues(rowId, 
                imgField,       LogLevelToIcon(entry.Type),
                typeField,      entry.Type.ToString(),
                machineField,   entry.MachineName ?? string.Empty,
                objectField,    entry.ObjectName ?? string.Empty,
                textField,      entry.Message,
                timestampField, string.Format("{0:HH:mm:ss.ffff}", entry.Time),
                idField,        entry.Id);

            if(scrollOnRefresh || entry.Id == SelectedItemId)
            {
                SelectRow(rowId);
            }
            while (limit.HasValue && limit != 0 && listStore.RowCount > limit)
            {
                listStore.RemoveRow(listStore.RowCount - 1);
            }

            return rowId;
        }

        public void ClearItems()
        {
            lastLogEntry = null;
            lastLogEntryCounter = 1;
            listStore.Clear();
        }

        public bool SqueezeEntries { get; private set; }

        public ulong? SelectedItemId { get; private set; }

        public ulong? FirstItemId { get { return listStore.RowCount == 0 ? (ulong?)null : listStore.GetValue(0, idField); } }
        public ulong? LastItemId  { get { return listStore.RowCount == 0 ? (ulong?)null : listStore.GetValue(listStore.RowCount - 1, idField); } }

        public int ItemsCount { get { return listStore.RowCount; } }
        #pragma warning disable 67
        // on windows we currently don't support this event
        public event Action<ScrolledState> Scrolled;
        #pragma warning restore 67
        protected override void OnSelectionChanged(EventArgs a)
        {
            base.OnSelectionChanged(a);
            if(SelectedRow != -1)
            {
                SelectedItemId = listStore.GetValue(SelectedRow, idField);
            }
        }

        private Image LogLevelToIcon(LogLevel level)
        {
            if (level == LogLevel.Error)
            {
                return errorIcon;
            }
            else if (level == LogLevel.Warning)
            {
                return warningIcon;
            }
            else
            {
                return infoIcon;
            }
        }

        protected readonly DataField<Image>  imgField;
        protected readonly DataField<string> timestampField;
        protected readonly DataField<string> typeField;
        protected readonly DataField<string> textField;
        protected readonly DataField<string> objectField;
        protected readonly DataField<string> machineField;
        protected readonly DataField<ulong>  idField;
        protected readonly ListStore listStore;

        private LogEntry lastLogEntry;
        private int lastLogEntryCounter = 1;

        private readonly Image errorIcon;
        private readonly Image warningIcon;
        private readonly Image infoIcon;

        private const double Epsilon = 15.0;

        public enum ScrolledState
        {
            ScrolledUp,
            ScrolledDown
        }
    }
}

