using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Timers;
using Microsoft.Extensions.Logging;

namespace WindowPos;

public class TrayApplicationContext : ApplicationContext
{
    [DllImport("user32.dll")]
    static extern bool GetWindowRect(IntPtr hwnd, ref Rect rectangle);

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    private ILogger<TrayApplicationContext> logger;

    private struct Rect
    {
        public int Left { get; set; }
        public int Top { get; set; }
        public int Right { get; set; }
        public int Bottom { get; set; }
    }

    private readonly NotifyIcon _trayIcon;
    private readonly List<(string, Dictionary<string, (Rect, string)>)> _desktopLayouts = new();
    private readonly System.Timers.Timer _saveTimer;

    public TrayApplicationContext(ILogger<TrayApplicationContext> logger)
    {
        this.logger = logger;
        _trayIcon = new NotifyIcon()
        {
            Icon = Resources.WindowPos, // Add your own icon here
            ContextMenuStrip = new ContextMenuStrip(),
            Visible = true
        };

        _trayIcon.ContextMenuStrip.Items.Add("Save Window Positions", null, SaveWindowPositions);
        _trayIcon.ContextMenuStrip.Items.Add("Exit", null, Exit);

        _saveTimer = new System.Timers.Timer(30000); // 30 seconds
        _saveTimer.Elapsed += OnTimedEvent;
        _saveTimer.AutoReset = true;
        _saveTimer.Enabled = true;
        SaveWindowPositions(null, null);
    }

    private void OnTimedEvent(object? source, ElapsedEventArgs e)
    {
        logger.LogInformation("Saving window positions due to timer event");
        SaveWindowPositions(null, null);
    }

    void SaveWindowPositions(object? sender, EventArgs? e)
    {
        Dictionary<string, (Rect, string)> currentLayout = new Dictionary<string, (Rect, string)>();

        foreach (Process p in Process.GetProcesses())
        {
            if (!string.IsNullOrEmpty(p.MainWindowTitle))
            {
                var rect = new Rect();
                GetWindowRect(p.MainWindowHandle, ref rect);

                // Get the screen that contains the largest area of the window
                var screen = Screen.FromHandle(p.MainWindowHandle);

                // Save the window position relative to the screen bounds
                rect.Left -= screen.Bounds.Left;
                rect.Top -= screen.Bounds.Top;
                rect.Right -= screen.Bounds.Left;
                rect.Bottom -= screen.Bounds.Top;

                currentLayout[p.MainWindowTitle] = (rect, screen.DeviceName);
            }
        }

        // Only add the current layout if it's different from the last saved layout
        if (_desktopLayouts.Count == 0 || !AreLayoutsEqual(currentLayout, _desktopLayouts.Last().Item2))
        {
            // Use a user-friendly timestamp as the layout name
            string layoutName = $"Layout created - {DateTime.Now:HH:mm:ss dd-MM-yyyy}";

            // Add the current layout to the list of layouts
            _desktopLayouts.Add((layoutName, currentLayout));

            // If there are more than 10 layouts, remove the oldest one
            if (_desktopLayouts.Count > 10)
            {
                _desktopLayouts.RemoveAt(0);
            }

            // Update the context menu
            UpdateContextMenu();
            logger.LogInformation($"Saved new desktop layout: {layoutName}");
        }
    }

    bool AreLayoutsEqual(Dictionary<string, (Rect, string)> layout1, Dictionary<string, (Rect, string)> layout2)
    {
        if (layout1.Count != layout2.Count)
        {
            return false;
        }

        foreach (var kvp in layout1)
        {
            if (!layout2.TryGetValue(kvp.Key, out var value) || !AreRectsEqual(value.Item1, kvp.Value.Item1) || value.Item2 != kvp.Value.Item2)
            {
                return false;
            }
        }

        return true;
    }

    bool AreRectsEqual(Rect rect1, Rect rect2)
    {
        return rect1.Left == rect2.Left && rect1.Top == rect2.Top && rect1.Right == rect2.Right && rect1.Bottom == rect2.Bottom;
    }

    void UpdateContextMenu()
    {
        // This code must be run on the UI thread
        if (_trayIcon.ContextMenuStrip is { InvokeRequired: true })
        {
            _trayIcon.ContextMenuStrip.Invoke(new MethodInvoker(UpdateContextMenu));
            return;
        }

        // Remove all items except for the "Save Window Positions" and "Exit" items
        while (_trayIcon.ContextMenuStrip is { Items.Count: > 2 })
        {
            _trayIcon.ContextMenuStrip.Items.RemoveAt(1);
        }

        // Create a new menu item for the "Restore Layout" submenu
        var restoreLayoutMenuItem = new ToolStripMenuItem("Restore Layout");

        // Add a menu item for each layout to the "Restore Layout" submenu
        foreach (var (layoutName, _) in _desktopLayouts)
        {
            restoreLayoutMenuItem.DropDownItems.Add(new ToolStripMenuItem(layoutName, null, RestoreWindowPositions));
        }

        // Add the "Restore Layout" submenu to the context menu
        _trayIcon.ContextMenuStrip?.Items.Insert(1, restoreLayoutMenuItem);
    }

    void RestoreWindowPositions(object? sender, EventArgs e)
    {
        // Get the layout associated with the clicked menu item
        if (sender == null) 
            return;

        var layoutName = ((ToolStripMenuItem)sender).Text;
        var layout = _desktopLayouts.First(t => t.Item1 == layoutName).Item2;

        foreach (Process p in Process.GetProcesses())
        {
            if (layout.TryGetValue(p.MainWindowTitle, out var value))
            {
                (Rect rect, string screenDeviceName) = value;

                // Get the screen to move the window to
                Screen? screen = Screen.AllScreens.FirstOrDefault(s => s.DeviceName == screenDeviceName) ?? Screen.PrimaryScreen;

                // Adjust the window position for the screen bounds
                if (screen != null)
                {
                    rect.Left += screen.Bounds.Left;
                    rect.Top += screen.Bounds.Top;
                    rect.Right += screen.Bounds.Left;
                    rect.Bottom += screen.Bounds.Top;
                    SetWindowPos(p.MainWindowHandle, IntPtr.Zero, rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top, 0);
                }
            }
        }
        logger.LogInformation($"Restored desktop layout: {layoutName}");
    }

    void Exit(object? sender, EventArgs e)
    {
        _trayIcon.Visible = false;
        Application.Exit();
    }
}