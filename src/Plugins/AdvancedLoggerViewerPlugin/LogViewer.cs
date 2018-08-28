//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Xwt;
using System.Collections.Generic;
using Antmicro.Renode.Logging;
using System.Linq;
using System;
using Antmicro.Renode.Utilities;
using System.Threading.Tasks;
using System.Threading;
using Lucene.Net.QueryParsers;
using Antmicro.Renode.Logging.Backends;
using Antmicro.Renode.Logging.Lucene;
using Antmicro.Renode.UI;
using Antmicro.Renode.UI.Progress;

namespace Antmicro.Renode.Plugins.AdvancedLoggerViewer
{
    public class LogViewer : VBox
    {
        public LogViewer(LuceneLoggerBackend backend)
        {
            luceneStore = backend;
            levels = new HashSet<LogLevel>();

            Init();
        }

        async public Task RebuildIndex()
        {
            refreshButton.Sensitive = false;
            lastRefreshLabel.Text = CustomDateTime.Now.ToString();
            await luceneStore.RebuildIndexAsync(progressWidget.ProgressMonitor);
            await FilterEntriesAsync(progressWidget.ProgressMonitor, true);
            refreshButton.Sensitive = true;
        }

        private void Init()
        {
            progressWidget = new ProgressWidget();

            refreshButton = new Button("Refresh");
            refreshButton.Clicked += async (sender, e) => await RebuildIndex();

            pageStatusLabel = new Label();

            lastRefreshLabel = new Label("-");
            var autoRefreshBox = new HBox();
            autoRefreshBox.PackStart(pageStatusLabel);
            autoRefreshBox.PackStart(new Label(string.Empty), true);
            autoRefreshBox.PackStart(progressWidget);
            autoRefreshBox.PackStart(new Label(string.Empty), true);
            autoRefreshBox.PackStart(new Label("Log state from:"));
            autoRefreshBox.PackStart(lastRefreshLabel);
            autoRefreshBox.PackStart(refreshButton);
            PackStart(autoRefreshBox);

            var hbox = new HBox();

            var buttons = new LogLevelsSelectionWidget();

            hbox.PackStart(new Label("Filter:"));
            hbox.PackStart(buttons);

            hbox.PackStart(new HSeparator());
            filterTextEntry = new TextEntry();
            hbox.PackStart(filterTextEntry, true);
            filterTextEntry.KeyReleased += async (sender, e) => 
            {
                if (e.Key == Key.Return || e.Key == Key.NumPadEnter)
                {
                    await HandleFilterButtonClicked();
                }
            };
            var queryButton = new Button("Query");
            queryButton.Clicked += async (sender, e) => await HandleFilterButtonClicked();

            hbox.PackStart(queryButton);

            var helpButton = new Button("Help");
            helpButton.Clicked += (sender, e) => 
            {
                using (var dialog = new LogViewerHelpDialog())
                {
                    dialog.Run();
                }
            };
            hbox.PackStart(helpButton);

            listViewLog = new ListViewLog(false);
            listViewLog.ButtonPressed += (s, ea) =>
            {
                if (ea.Button != PointerButton.Right)
                {
                    return;
                }

                var menu = new Menu();
                var item = new MenuItem("Show surrounding log entries");

                item.Clicked += async (sender, e) =>
                {
                    var id = listViewLog.SelectedItemId;
                    if (!id.HasValue)
                    {
                        return;
                    }

                    var idFrom = Math.Max(0, id.Value - 20);
                    var idTo = id.Value + 20;
                    filterTextEntry.Text = string.Format("id:[{0} TO {1}]", idFrom, idTo);
                    await FilterEntriesAsync(progressWidget.ProgressMonitor);
                };

                menu.Items.Add(item);
                menu.Popup();
            };

            PackStart(listViewLog, true);
            PackStart(hbox);

            buttons.SelectionChanged += async (level, isSelected) => 
            {
                if (isSelected)
                {
                    levels.Add(level);
                }
                else
                {
                    levels.Remove(level);
                }
                await FilterEntriesAsync(progressWidget.ProgressMonitor, true);
            };
#if !PLATFORM_WINDOWS
            listViewLog.Scrolled += async state =>
            {
                if(Interlocked.Increment(ref eventDepth) > 1)
                {
                    Interlocked.Decrement(ref eventDepth);
                    return;
                }

                await LoadEntriesAsync(progressWidget.ProgressMonitor, 
                    state == ListViewLog.ScrolledState.ScrolledUp ? Direction.Backward : Direction.Forward,
                    () => Interlocked.Decrement(ref eventDepth));
            };
#endif
            var searchBox = new HBox();
            searchTextEntry = new TextEntry();
            searchTextEntry.KeyReleased += async (sender, e) => 
            {
                if (e.Key == Key.Return || e.Key == Key.NumPadEnter)
                {
                    await HandleSearchButtonClicked();
                }
            };
            searchBox.PackStart(new Label("Search:"));
            searchBox.PackStart(searchTextEntry, true);
            searchButton = new Button("Search");
            searchButton.Clicked += async (sender, e) => await HandleSearchButtonClicked();
            searchBox.PackStart(searchButton);
            searchLabel = new Label();
            nextSearchResultButton = new Button("Next");
            prevSearchResultButton = new Button("Previous");
            resetSearchButton = new Button("Reset");
            resetSearchButton.Clicked += (sender, e) => ResetSearch();
            nextSearchResultButton.Clicked += async (sender, e) => await SearchEntriesAsync(progressWidget.ProgressMonitor, Direction.Forward);
            prevSearchResultButton.Clicked += async (sender, e) => await SearchEntriesAsync(progressWidget.ProgressMonitor, Direction.Backward);
            searchBox.PackStart(searchLabel);
            searchBox.PackStart(nextSearchResultButton);
            searchBox.PackStart(prevSearchResultButton);
            searchBox.PackStart(resetSearchButton);

            PackStart(searchBox);
            ResetSearch();

            RefreshPaging(0);
        }

        private async Task HandleFilterButtonClicked()
        {
            try 
            {
                await FilterEntriesAsync(progressWidget.ProgressMonitor, true);
            }
            catch (ParseException e)
            {
                MessageDialog.ShowError("Filter pattern syntax error", string.Format("{0}.\nClick Help button for more information on syntax.", e.Message));
            }
        }

        private async Task HandleSearchButtonClicked()
        {
            if(string.IsNullOrWhiteSpace(searchTextEntry.Text))
            {
                return;
            }

            searchTextEntry.Sensitive = false;
            searchButton.Sensitive = false;
            resetSearchButton.Sensitive = true;
            try 
            {
                await SearchEntriesAsync(progressWidget.ProgressMonitor, Direction.Backward); 
            }
            catch (ParseException e)
            {
                ResetSearch(false);
                MessageDialog.ShowError("Search pattern syntax error", string.Format("{0}.\nClick Help button for more information on syntax.", e.Message));
            }
        }

        private void ResetSearch(bool clearEnteredSearchPhrase = true)
        {
            searchContext = null;
            searchLabel.Text = string.Empty;

            resetSearchButton.Sensitive = false;
            nextSearchResultButton.Sensitive = false;
            prevSearchResultButton.Sensitive = false;

            searchButton.Sensitive = true;
            searchTextEntry.Sensitive = true;

            if(clearEnteredSearchPhrase)
            {
                searchTextEntry.Text = string.Empty;
            }
        }

        private async Task SearchEntriesAsync(ProgressMonitor progressMonitor, Direction direction)
        {
            using(var progress = progressMonitor.Start("Searching in log..."))
            {
                var view = new ViewFilter { LogLevels = levels.ToList(), CustomFilter = filterTextEntry.Text };

                var entries = await luceneStore.FindAsync(searchTextEntry.Text, view,  2 * pageSize, direction, searchContext != null ? searchContext.PreviousResultId : null);
                if(entries.Entries == null)
                {
                    ApplicationExtensions.InvokeInUIThread(() => MessageDialog.ShowWarning("No results found!"));
                    ResetSearch(false);
                    return;
                }

                if(searchContext == null)
                {
                    searchContext = new SearchContext(entries.TotalHits);
                }
                else
                {
                    searchContext.Advance(direction);
                }
                searchContext.PreviousResultId = entries.FoundId;

                ApplicationExtensions.InvokeInUIThread(() =>
                {
                    listViewLog.ClearItems();
                    foreach(var entry in entries.Entries)
                    {
                        listViewLog.AddItem(entry, entries.FoundId == entry.Id);
                    }

                    searchLabel.Text = string.Format("Showing result: {0} / {1}", searchContext.CurrentResult, searchContext.ResultsCount);
                    nextSearchResultButton.Sensitive = (searchContext.CurrentResult > 1);  
                    prevSearchResultButton.Sensitive = (searchContext.CurrentResult < searchContext.ResultsCount);
                });
            }
        }

        private async Task LoadEntriesAsync(ProgressMonitor progressMonitor, Direction direction, Action afterCallback = null)
        {
            var view = new ViewFilter { LogLevels = levels.ToList(), CustomFilter = filterTextEntry.Text };
            using(var progress = progressMonitor.Start("Filtering log..."))
            {
                var referenceId = (direction == Direction.Backward) ? listViewLog.FirstItemId : listViewLog.LastItemId;
                var entries = await luceneStore.FilterHistoryViewAsync(filterTextEntry.Text, view, pageSize, direction, referenceId);
                if(entries.Entries.Any())
                {
                    if(direction == Direction.Backward)
                    {
                        ApplicationExtensions.InvokeInUIThread(() =>
                        {
                            foreach(var entry in entries.Entries.Where(e => e.Id < referenceId).Reverse())
                            {
                                listViewLog.InsertItem(entry, false, maxPageCount * pageSize);
                            }
                        });
                    }
                    else
                    {
                        ApplicationExtensions.InvokeInUIThread(() =>
                        {
                            foreach(var entry in entries.Entries.Where(e => e.Id > referenceId))
                            {
                                listViewLog.AddItem(entry, false, maxPageCount * pageSize);
                            }
                        });
                    }
                }
            }

            if(afterCallback != null)
            {
                afterCallback();
            }
        }

        private async Task FilterEntriesAsync(ProgressMonitor progressMonitor, bool selectLastAddedItem = false)
        {
            using(var progress = progressMonitor.Start("Filtering log..."))
            {
                var view = new ViewFilter { LogLevels = levels.ToList(), CustomFilter = filterTextEntry.Text };

                var entries = await luceneStore.FilterHistoryViewAsync(filterTextEntry.Text, view, maxPageCount * pageSize, Direction.Backward);
                ApplicationExtensions.InvokeInUIThread(() =>
                {
                    listViewLog.ClearItems();
                    foreach(var entry in entries.Entries)
                    {
                        listViewLog.AddItem(entry, selectLastAddedItem, maxPageCount * pageSize);
                    }

                    RefreshPaging(entries.TotalHits);
                });
            }
        }

        private void RefreshPaging(int totalCount)
        {
            pageStatusLabel.Text = string.Format("Total results: {0}", totalCount);
        }
#if !PLATFORM_WINDOWS
        private int eventDepth;
#endif
        private ListViewLog listViewLog;
        private TextEntry searchTextEntry;
        private TextEntry filterTextEntry;
        private Label lastRefreshLabel;
        private Label pageStatusLabel;
        private Button resetSearchButton;
        private Button searchButton;
        private Button nextSearchResultButton;
        private Button prevSearchResultButton;
        private SearchContext searchContext;
        private Label searchLabel;
        private ProgressWidget progressWidget;
        private Button refreshButton;

        private readonly HashSet<LogLevel> levels;
        private readonly LuceneLoggerBackend luceneStore;

        private const int maxPageCount = 3;
        private const int pageSize = 25;
    }
}
