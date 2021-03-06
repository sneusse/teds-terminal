﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using TTerm.Extensions;
using TTerm.Terminal;

namespace TTerm.Ui
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : EnhancedWindow
    {
        private const int MinColumns = 52;
        private const int MinRows = 4;
        private const int ReadyDelay = 1000;

        private int _tickInitialised;
        private bool _ready;
        private Size _consoleSizeDelta;


        private TerminalSessionManager _sessionMgr = new TerminalSessionManager();
        private TerminalSession _currentSession;
        private TerminalSize _terminalSize;
        private ExecutionProfile _defaultExecutionProfile;

        public bool Ready
        {
            get
            {
                // HACK Try and find a more reliable way to check if we are ready.
                //      This is to prevent the resize hint from showing at startup.
                if (!_ready)
                {
                    _ready = Environment.TickCount > _tickInitialised + ReadyDelay;
                }
                return _ready;
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            // AllowsTransparency = true;
            resizeHint.Visibility = Visibility.Hidden;


        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            _tickInitialised = Environment.TickCount;
        }

        private void NewSessionTab_Click(object sender, EventArgs e)
        {
            CreateSession(_defaultExecutionProfile);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // var config = _configService.Config;
            //
            // int columns = Math.Max(config.Columns, MinColumns);
            // int rows = Math.Max(config.Rows, MinRows);
            // _terminalSize = new TerminalSize(columns, rows);
            // FixWindowSize();
            //
            // ExecutionProfile executionProfile = config.ExecutionProfile;
            // if (executionProfile == null)
            // {
            //     executionProfile = DefaultProfile.Get();
            // }
            // _defaultExecutionProfile = executionProfile.ExpandVariables();
            // CreateSession(_defaultExecutionProfile);
        }

        protected override void OnForked(ForkData data)
        {
            ExecutionProfile executionProfile = _defaultExecutionProfile;
            executionProfile.CurrentWorkingDirectory = data.CurrentWorkingDirectory;
            executionProfile.EnvironmentVariables = data.Environment;
            CreateSession(executionProfile);
        }

        private void CreateSession(ExecutionProfile executionProfile)
        {
            var session = _sessionMgr.CreateSession(_terminalSize, executionProfile);
            // session.TitleChanged += OnSessionTitleChanged;
            session.Finished += OnSessionFinished;


            ChangeSession(session);
        }


        private void ChangeSession(TerminalSession session)
        {
            if (session != _currentSession)
            {
                if (_currentSession != null)
                {
                    _currentSession.Active = false;
                }

                _currentSession = session;

                if (session != null)
                {
                    session.Active = true;
                    session.Buffer.Size = _terminalSize;
                }

                terminalControl.Session = session;
                terminalControl.Focus();
            }
        }

        private void CloseSession(TerminalSession session)
        {
            session.Dispose();
        }


        private void OnSessionFinished(object sender, EventArgs e)
        {
            var session = sender as TerminalSession;

            var sessions = _sessionMgr.Sessions;
            if (sessions.Count > 0)
            {
                ChangeSession(sessions[0]);
            }
            else
            {
                ChangeSession(null);
            }
        }

        private void terminalControl_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                {
                    if (AllowsTransparency)
                    {
                        var terminal = terminalControl;
                        const double OpacityDelta = 1 / 32.0;
                        if (e.Delta > 0)
                        {
                            Opacity = Math.Min(Opacity + OpacityDelta, 1);
                        }
                        else
                        {
                            Opacity = Math.Max(Opacity - OpacityDelta, 0.25);
                        }
                        e.Handled = true;
                    }
                }
                else
                {
                    var terminal = terminalControl;
                    const double FontSizeDelta = 2;
                    double fontSize = terminal.FontSize;
                    if (e.Delta > 0)
                    {
                        if (fontSize < 54)
                        {
                            fontSize += FontSizeDelta;
                        }
                    }
                    else
                    {
                        if (fontSize > 8)
                        {
                            fontSize -= FontSizeDelta;
                        }
                    }
                    if (terminal.FontSize != fontSize)
                    {
                        terminal.FontSize = fontSize;
                        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
                        {
                            FixWindowSize();
                        }
                        else
                        {
                            FixTerminalSize();
                        }
                    }
                    e.Handled = true;
                }
            }
        }

        private void FixTerminalSize()
        {
            var size = GetBufferSizeForWindowSize(Size);
            SetTermialSize(size);
        }

        private void FixWindowSize()
        {
            Size = GetWindowSizeForBufferSize(_terminalSize);
        }

        private TerminalSize GetBufferSizeForWindowSize(Size size)
        {
            Size charSize = terminalControl.CharSize;
            Size newConsoleSize = new Size(Math.Max(size.Width - _consoleSizeDelta.Width, 0),
                                           Math.Max(size.Height - _consoleSizeDelta.Height, 0));

            int columns = (int)Math.Floor(newConsoleSize.Width / charSize.Width);
            int rows = (int)Math.Floor(newConsoleSize.Height / charSize.Height);

            columns = Math.Max(columns, MinColumns);
            rows = Math.Max(rows, MinRows);

            return new TerminalSize(columns, rows);
        }

        private Size GetWindowSizeForBufferSize(TerminalSize size)
        {
            Size charSize = terminalControl.CharSize;
            Size snappedConsoleSize = new Size(size.Columns * charSize.Width,
                                               size.Rows * charSize.Height);

            Size result = new Size(Math.Ceiling(snappedConsoleSize.Width + _consoleSizeDelta.Width) + 2,
                                   Math.Ceiling(snappedConsoleSize.Height + _consoleSizeDelta.Height));
            return result;
        }

        protected override Size GetPreferedSize(Size size)
        {
            var tsize = GetBufferSizeForWindowSize(size);
            return GetWindowSizeForBufferSize(tsize);
        }

        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            Size result = base.ArrangeOverride(arrangeBounds);
            _consoleSizeDelta = new Size(Math.Max(arrangeBounds.Width - terminalControl.ActualWidth, 0),
                                         Math.Max(arrangeBounds.Height - terminalControl.ActualHeight, 0));
            return result;
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            FixTerminalSize();
        }

        protected override void OnResizeEnded()
        {
            resizeHint.IsShowing = false;
        }

        private void SetTermialSize(TerminalSize size)
        {
            if (_terminalSize != size)
            {
                _terminalSize = size;
                if (_currentSession != null)
                {
                    _currentSession.Buffer.Size = size;
                }

                if (Ready)
                {
                    // Save configuration
                    // _configService.Config.Columns = size.Columns;
                    // _configService.Config.Rows = size.Rows;
                    // _configService.Save();

                    // Update hint overlay
                    resizeHint.Hint = size;
                    resizeHint.IsShowing = true;
                    resizeHint.IsShowing = IsResizing;
                }
            }
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            var modifiers = Keyboard.Modifiers;
            if (modifiers.HasFlag(ModifierKeys.Control))
            {
                switch (e.Key)
                {
                    case Key.Tab:
                        {
                            var direction = modifiers.HasFlag(ModifierKeys.Shift) ?
                                CycleDirection.Previous : CycleDirection.Next;
                            CycleSession(direction);
                            e.Handled = true;
                            break;
                        }
                    case Key.PageUp:
                        {
                            int direction = modifiers.HasFlag(ModifierKeys.Shift) ? -1 : 1;
                            CycleSession(CycleDirection.Previous);
                            e.Handled = true;
                            break;
                        }
                    case Key.PageDown:
                        {
                            CycleSession(CycleDirection.Next);
                            e.Handled = true;
                            break;
                        }
                    case Key.N:
                        {
                            // var app = Application.Current as App;
                            // app.StartNewInstance();
                            // e.Handled = true;
                            break;
                        }
                    case Key.T:
                        {
                            CreateSession(_defaultExecutionProfile);
                            e.Handled = true;
                            break;
                        }
                    case Key.W:
                        {
                            if (_currentSession != null)
                            {
                                CloseSession(_currentSession);
                            }
                            e.Handled = true;
                            break;
                        }
                }
            }
            base.OnPreviewKeyDown(e);
        }

        private enum CycleDirection { Previous, Next}

        private void CycleSession(CycleDirection direction)
        {
            int numSessions = _sessionMgr.Sessions.Count;
            if (numSessions > 1)
            {
                int sessionIndex = _sessionMgr.Sessions.IndexOf(_currentSession);
                if (sessionIndex != -1)
                {
                    if (direction == CycleDirection.Previous)
                    {
                        sessionIndex--;
                        if (sessionIndex < 0)
                        {
                            sessionIndex = numSessions - 1;
                        }
                    }
                    else
                    {
                        sessionIndex++;
                        if (sessionIndex >= numSessions)
                        {
                            sessionIndex = 0;
                        }
                    }

                    var newSession = _sessionMgr.Sessions[sessionIndex];
                    ChangeSession(newSession);
                }
            }
        }
    }
}
