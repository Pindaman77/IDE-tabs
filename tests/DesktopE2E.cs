// Independent black-box driver: no reference to IDE-tabs or service injection.
// Real entry point, foreign HWNDs, SendInput, foreground/owner checks, normal Quit.
// The runner stages this driver as Code.exe, so IDE-tabs takes its windows for VS Code's.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

static partial class DesktopE2E
{
    delegate bool EnumProc(IntPtr hwnd, IntPtr data);
    [StructLayout(LayoutKind.Sequential)] struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct GuiThread { public int Size,Flags; public IntPtr Active,Focus,Capture,MenuOwner,MoveSize,Caret; public Rect CaretRect; }
    [StructLayout(LayoutKind.Sequential)] struct Mouse { public int X,Y; public uint Data,Flags,Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] struct Key { public ushort Code,Scan; public uint Flags,Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Explicit)] struct Data { [FieldOffset(0)] public Mouse Mouse; [FieldOffset(0)] public Key Key; }
    [StructLayout(LayoutKind.Sequential)] struct Input { public uint Type; public Data Data; }
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback, IntPtr data);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] static extern bool GetGUIThreadInfo(uint thread, ref GuiThread info);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr hwnd);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hwnd, int command);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr hwnd, uint command);
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
    [DllImport("dwmapi.dll", EntryPoint="DwmGetWindowAttribute")] static extern int DwmGetFrame(IntPtr hwnd, int attribute, out Rect value, int size);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool AllowSetForegroundWindow(int pid);
    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr hwnd, StringBuilder text, int size);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wparam, IntPtr lparam);
    [DllImport("oleacc.dll")] static extern int AccessibleObjectFromWindow(IntPtr window, uint objectId, ref Guid iid, [MarshalAs(UnmanagedType.IDispatch)] out object accessible);
    [DllImport("oleacc.dll")] static extern int AccessibleChildren(Accessibility.IAccessible container, int start, int count, [Out] object[] children, out int obtained);
    [DllImport("user32.dll", SetLastError=true)] static extern uint SendInput(uint count, Input[] inputs, int size);
    [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll")] static extern uint GetGuiResources(IntPtr process, uint kind);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] static extern IntPtr OpenInputDesktop(uint flags, bool inherit, uint access);
    [DllImport("user32.dll")] static extern bool CloseDesktop(IntPtr desktop);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern bool GetUserObjectInformation(IntPtr handle, int index, StringBuilder text, int size, out uint needed);
    static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
    static readonly string Root = AppDomain.CurrentDomain.BaseDirectory;
    static readonly List<Form> Forms = new List<Form>();
    static readonly List<object> Measurements = new List<object>();
    static readonly List<object> ResourceSamples = new List<object>();
    static readonly Queue<object> RecentActions = new Queue<object>();
    static Process app;
    static string phase = "preflight";
    static string step = "";
    static int completed, totalSwitches;
    static double switchingSeconds;
    static int clickOrdinal, helperCount;
    static Point savedCursor;
    static bool inputOwned;
    static bool randomize;
    static bool cleaningUp;
    static int seed, randomActionCount, randomBatchesPerCycle;
    // Hiding, narrowing, maximizing or minimizing moves every editor and the column; a busy
    // desktop can take seconds to settle.
    const int Settle=20000;
    sealed class RandomAction
    {
        public int Kind, Target, Burst;
        public int[] Digits;
    }
    static RandomAction Plan(Random random)
    {
        var action=new RandomAction { Kind=random.Next(8),Target=random.Next(3),Burst=random.Next(2,13) };
        action.Digits=Enumerable.Range(0,action.Burst).Select(i=>random.Next(3)).ToArray();
        return action;
    }
    static void CheckRandomPlan()
    {
        foreach(int testSeed in new[]{0,1,-1,int.MinValue,int.MaxValue})
        {
            var first=new Random(testSeed); var replay=new Random(testSeed);
            var kinds=new HashSet<int>();
            for(int i=0;i<1000;i++)
            {
                var a=Plan(first); var b=Plan(replay); kinds.Add(a.Kind);
                Check(Json.Serialize(a)==Json.Serialize(b),"Seed replay changed the random plan");
                Check(a.Kind>=0 && a.Kind<8 && a.Target>=0 && a.Target<3 && a.Burst>=2 && a.Burst<=12 &&
                    a.Digits.Length==a.Burst && a.Digits.All(n=>n>=0 && n<3),"Random plan contains an invalid action");
            }
            Check(kinds.Count==8,"Random plan did not exercise every action kind");
        }
        Log("PASS: random planner bounds, action coverage and deterministic seed replay");
    }
    static void Check(bool ok, string text) { if (!ok) throw new Exception(phase + " / " + step + ": " + text); }
    static void Log(string text) { Console.WriteLine(DateTime.UtcNow.ToString("o") + " " + text); }
    static void Remember(string action, object details)
    {
        var record=new { utc=DateTime.UtcNow.ToString("o"),phase,step,action,details,foreground=GetForegroundWindow().ToInt64() };
        RecentActions.Enqueue(record);
        if(randomize) File.AppendAllText(Path.Combine(Root,"actions.jsonl"),Json.Serialize(record)+Environment.NewLine);
        while(RecentActions.Count>40) RecentActions.Dequeue();
    }
    static bool HasCrash() { var path=Path.Combine(Root,"IDE-tabs-crash.log"); return File.Exists(path) && new FileInfo(path).Length>0; }
    static void Diagnose()
    {
        if(app==null || app.HasExited) return;
        var all=Windows(app.Id).Concat(Helpers()).Select(h=> new { hwnd=h.ToInt64(),owner=GetWindow(h,4).ToInt64(),visible=IsWindowVisible(h),style=GetWindowLong(h,-16),extendedStyle=GetWindowLong(h,-20),windowClass=Class(h),bounds=Bounds(h) });
        File.WriteAllText(Path.Combine(Root,"windows.json"),Json.Serialize(all.ToArray()));
    }
    static string Class(IntPtr h) { var s=new StringBuilder(256); GetClassName(h,s,s.Capacity); return s.ToString(); }
    static List<IntPtr> Windows(int pid)
    {
        var result=new List<IntPtr>();
        EnumWindows((h,d) => { uint p; GetWindowThreadProcessId(h,out p); if(p==pid) result.Add(h); return true; },IntPtr.Zero);
        return result;
    }
    static List<IntPtr> Helpers() { return Forms.Where(f=>!f.IsDisposed).Select(f=>f.Handle).ToList(); }
    static Form HelperForm(IntPtr h) { return Forms.Single(f=>!f.IsDisposed && f.Handle==h); }
    static List<IntPtr> Strips()
    {
        var helpers=Helpers();
        return Windows(app.Id).Where(h=>IsWindowVisible(h) && helpers.Contains(GetWindow(h,4)) &&
            (GetWindowLong(h,-20)&0x80000)!=0 && (GetWindowLong(h,-20)&0x20)==0).ToList();
    }
    static Rect Bounds(IntPtr h) { Rect r; Check(GetWindowRect(h,out r),"Cannot read HWND bounds"); return r; }
    /// What is seen of a window, without the invisible resize borders.
    static Rect Frame(IntPtr h) { Rect r; Check(DwmGetFrame(h,9,out r,Marshal.SizeOf(typeof(Rect)))==0,"Cannot read the visible frame"); return r; }
    static int Scaled(IntPtr h, int value) { return (int)Math.Round(value*(double)GetDpiForWindow(h)/96); }
    static bool Near(int a, int b) { return Math.Abs(a-b)<=2; }
    // The column's layout (TabStripSprite): a header row with the chevron, then one row per tab.
    static int RowHeight(IntPtr strip) { return Scaled(strip,32); }
    static Point HeaderPoint(IntPtr strip) { var r=Bounds(strip); return new Point((r.Left+r.Right)/2,r.Top+RowHeight(strip)/2); }
    static Point RowPoint(IntPtr strip, int index)
    {
        var r=Bounds(strip); int row=RowHeight(strip), gap=Scaled(strip,4);
        return new Point((r.Left+r.Right)/2,r.Top+row+gap+index*(row+gap)+row/2);
    }
    static bool ColumnWidth(IntPtr strip, int logical) { var r=Bounds(strip); return Near(r.Right-r.Left,Scaled(strip,logical)); }
    /// The front editor fills its monitor's work area beside the full-height column, or all of it
    /// while the column is hidden.
    static bool FillsWorkspace(IntPtr front, IntPtr strip)
    {
        if(!IsWindow(front) || IsZoomed(front) || IsIconic(front)) return false;
        var area=Screen.FromHandle(front).WorkingArea; var f=Frame(front);
        int left=area.Left;
        if(IsWindowVisible(strip))
        {
            var s=Bounds(strip);
            if(!Near(s.Left,area.Left) || !Near(s.Top,area.Top) || !Near(s.Bottom,area.Bottom)) return false;
            left=s.Right;
        }
        return Near(f.Left,left) && Near(f.Top,area.Top) && Near(f.Right,area.Right) && Near(f.Bottom,area.Bottom);
    }
    static IntPtr RootOwner(IntPtr window)
    {
        var seen=new HashSet<IntPtr>();
        while(GetWindow(window,4)!=IntPtr.Zero) {
            Check(seen.Add(window),"Cyclic owner chain"); window=GetWindow(window,4);
        }
        return window;
    }
    /// One taskbar button, and so one native Alt+Tab entry: every editor is owned, through a
    /// preview of its own, by one window of IDE-tabs.
    static bool OneTaskbarButton(List<IntPtr> members)
    {
        if(members.Any(h=>GetWindow(h,4)==IntPtr.Zero)) return false;
        var roots=members.Select(RootOwner).Distinct().ToList();
        return roots.Count==1 && !members.Contains(roots[0]) && Windows(app.Id).Contains(roots[0]) &&
            members.Select(h=>GetWindow(h,4)).Distinct().Count()==members.Count;
    }
    static void Inject(params Input[] inputs)
    {
        Check(SendInput((uint)inputs.Length,inputs,Marshal.SizeOf(typeof(Input)))==inputs.Length,"SendInput rejected input (locked desktop/UIPI?)");
    }
    static Input MouseInput(uint flags, int x=0, int y=0) { return new Input { Data=new Data { Mouse=new Mouse { X=x,Y=y,Flags=flags } } }; }
    static Input KeyInput(int code, bool up) { return new Input { Type=1,Data=new Data { Key=new Key { Code=(ushort)code,Flags=(up?2u:0u) | (code>=0x21 && code<=0x28 ? 1u:0u) } } }; }
    static void Move(Point p)
    {
        var v=SystemInformation.VirtualScreen;
        Inject(MouseInput(0xC001,(int)Math.Round((p.X-v.Left)*65535.0/(v.Width-1)),(int)Math.Round((p.Y-v.Top)*65535.0/(v.Height-1))));
    }
    static async Task ClickAt(IntPtr strip, Point p, string what)
    {
        // Repeated clicks at the same position within the system double-click
        // interval intentionally open Rename. Keep this a single-click scenario.
        p.X += (++clickOrdinal%2==0 ? 1 : -1) * (SystemInformation.DoubleClickSize.Width+2);
        Remember("click",new { strip=strip.ToInt64(),what,x=p.X,y=p.Y });
        CheckForeground();
        Check(WindowFromPoint(p)==strip,what+" is occluded; refusing to click another window");
        Move(p); await Task.Delay(20);
        Inject(MouseInput(2)); await Task.Delay(20); Inject(MouseInput(4));
        uint pid; uint thread=GetWindowThreadProcessId(strip,out pid);
        await Until("Click did not release mouse capture",()=> { var info=new GuiThread { Size=Marshal.SizeOf(typeof(GuiThread)) }; return GetGUIThreadInfo(thread,ref info) && info.Capture==IntPtr.Zero; });
    }
    static Task Click(IntPtr strip, int index) { return ClickAt(strip,RowPoint(strip,index),"Row "+index); }
    /// As Windows does when the editor itself, Alt+Tab or the taskbar brings a window forward.
    static void Activate(IntPtr window)
    {
        Remember("activate",new { window=window.ToInt64() });
        HelperForm(window).Activate();
    }
    /// Another application in front, such as an elevated Task Manager, would take or block the input.
    static void CheckForeground()
    {
        uint pid; GetWindowThreadProcessId(GetForegroundWindow(),out pid);
        Check(pid==Process.GetCurrentProcess().Id || (app!=null && pid==app.Id),"Foreground belongs to another application ("+GetForegroundWindow()+"); refusing input");
    }
    static void Chord(params int[] keys)
    {
        Remember("chord",new { keys });
        CheckForeground();
        var input=keys.Select(k=>KeyInput(k,false)).Concat(keys.Reverse().Select(k=>KeyInput(k,true))).ToArray();
        Inject(input);
    }
    static async Task Until(string description, Func<bool> condition, int timeout=5000)
    {
        var watch=Stopwatch.StartNew();
        Check(cleaningUp || !File.Exists(Path.Combine(Root,"stop-requested")),"Runner requested cancellation");
        while(!condition())
        {
            Check(cleaningUp || !File.Exists(Path.Combine(Root,"stop-requested")),"Runner requested cancellation");
            Check(app==null || !app.HasExited,"Application exited unexpectedly");
            Check(watch.ElapsedMilliseconds<timeout,description);
            await Task.Delay(10);
        }
    }
    static async Task Activated(IntPtr expected, IntPtr strip)
    {
        Remember("verify activation",new { expected=expected.ToInt64(),strip=strip.ToInt64(),owner=GetWindow(strip,4).ToInt64() });
        try { await Until("Wrong foreground, column owner or workspace frame after switch",()=>GetForegroundWindow()==expected && GetWindow(strip,4)==expected && IsWindowVisible(strip) && FillsWorkspace(expected,strip),5000); }
        catch { Log("expected="+expected+" foreground="+GetForegroundWindow()+" owner="+GetWindow(strip,4)+" frame="+Json.Serialize(Frame(expected))+" column="+Json.Serialize(Bounds(strip))); throw; }
        // Restoring from maximized can put the editor over its column for a moment.
        await Until("Active window covers its column",()=>WindowFromPoint(RowPoint(strip,0))==strip,2000);
    }
    static async Task<List<IntPtr>> ReadOrder(IntPtr strip, int count, IEnumerable<IntPtr> expected)
    {
        var order=new List<IntPtr>();
        for(int i=0;i<count;i++)
        {
            await Click(strip,i);
            // Each row brings forward an editor not read yet; the previous one may still be in front.
            await Until("Clicked row did not bring a new editor forward",()=>GetWindow(strip,4)==GetForegroundWindow() && expected.Contains(GetForegroundWindow()) && !order.Contains(GetForegroundWindow()));
            // Wait for event-driven ownership and placement to settle, then verify uniqueness.
            await Activated(GetForegroundWindow(),strip);
            order.Add(GetForegroundWindow());
        }
        Check(order.Distinct().Count()==count && new HashSet<IntPtr>(order).SetEquals(expected),"Missing, duplicated, or stale row in the column");
        return order;
    }
    static int[] Resources()
    {
        app.Refresh();
        var counts=new[]{(int)GetGuiResources(app.Handle,0),(int)GetGuiResources(app.Handle,1),app.HandleCount};
        Check(counts.All(n=>n>0),"Native resource counters are unavailable");
        return counts;
    }
    static void CheckGrowth(int[] before, int[] after)
    {
        Check(after[0]<=before[0]+256,"GDI growth exceeded the 256-object budget: "+before[0]+" -> "+after[0]);
        Check(after[1]<=before[1]+128,"USER growth exceeded the 128-object budget: "+before[1]+" -> "+after[1]);
        Check(after[2]<=before[2]+64,"Handle growth exceeded the 64-handle budget: "+before[2]+" -> "+after[2]);
    }
    static async Task<int[]> SwitchMany(IntPtr strip, List<IntPtr> order, int count)
    {
        var timings=new List<double>();
        // Warm caches before measuring growth; do not force GC inside the application.
        for(int i=0;i<30;i++) { step="warmup "+i; int n=i%order.Count; await Click(strip,n); await Activated(order[n],strip); }
        await Task.Delay(250);
        int[] before=Resources();
        for(int i=0;i<count;i++)
        {
            step="switch "+i;
            int current=order.IndexOf(GetForegroundWindow());
            Check(current>=0,"Foreground left the workspace (desktop input interference)");
            int target=(current+(i%4>=2?order.Count-1:1))%order.Count;
            var timer=Stopwatch.StartNew();
            await Click(strip,target);
            await Activated(order[target],strip);
            timings.Add(timer.Elapsed.TotalMilliseconds); totalSwitches++;
            Check(Strips().Count==1,"Switch created an extra column");
            if(i%50==49) { ResourceSamples.Add(new { phase,verifiedSwitches=totalSwitches,resources=Resources() }); Log("Verified switches: "+totalSwitches); }
        }
        // A burst clicks rows without awaiting each switch; compare final focus to a model.
        for(int i=0;i<6;i++) { await Click(strip,i%order.Count); await Task.Delay(15); }
        await Until("A burst of clicks did not end on its last row",()=>GetForegroundWindow()==order[5%order.Count] && GetWindow(strip,4)==order[5%order.Count] && FillsWorkspace(order[5%order.Count],strip),Settle);
        await Task.Delay(1000);
        await Activated(order[5%order.Count],strip);
        await Task.Delay(500);
        int[] after=Resources();
        timings.Sort();
        Measurements.Add(new { phase, count, p50Ms=timings[timings.Count/2],p95Ms=timings[(int)(timings.Count*.95)],maxMs=timings.Last(),before,after });
        // This external process cannot force GC in the application. Icon finalizers
        // make short-window counts fluctuate; use a bounded headroom budget and
        // retain raw measurements, not an assertion that every resource is a leak.
        CheckGrowth(before,after);
        return before;
    }
    // Browsers, Terminal and Explorer rely on an unowned top-level window, rather than APPWINDOW.
    sealed class NativeTaskbarForm : Form
    {
        public bool ForceTaskbar;
        protected override CreateParams CreateParams {
            get { var parameters=base.CreateParams; if(!ForceTaskbar) parameters.ExStyle &= ~0x40000; return parameters; }
        }
    }
    /// An editor window as VS Code titles it: "file - folder - Visual Studio Code".
    static string EditorTitle() { return "notes.md - E2E Project "+(++helperCount)+" - Visual Studio Code"; }
    static Form NewHelper(Point position, bool appWindow=false)
    {
        var helper=new NativeTaskbarForm { ForceTaskbar=appWindow,Text=EditorTitle(),StartPosition=FormStartPosition.Manual,
            Location=position,Size=new Size(560,150),BackColor=Color.LightBlue };
        helper.Menu=new MainMenu(new[]{new MenuItem("&File",new[]{new MenuItem("&Test")})});
        Forms.Add(helper); helper.Show(); ShowWindow(helper.Handle,4);
        return helper;
    }
    /// Hides the column with Ctrl+Alt+H, the front editor taking its place, then shows it again.
    static async Task ToggleSidebar(IntPtr strip, IntPtr front, Func<Task> whileHidden=null)
    {
        Chord(0x11,0x12,0x48);
        await Until("Ctrl+Alt+H did not hide the column and widen the editor",()=>!IsWindowVisible(strip) && FillsWorkspace(GetForegroundWindow(),strip),Settle);
        Check(GetForegroundWindow()==front,"Hiding the column moved the focus");
        if(whileHidden!=null) { await whileHidden(); front=GetForegroundWindow(); }
        Chord(0x11,0x12,0x48);
        await Until("Ctrl+Alt+H did not show the column again",()=>IsWindowVisible(strip) && FillsWorkspace(front,strip),Settle);
        await Activated(front,strip);
        // A click straight after the column comes back can be lost on a busy desktop.
        await Task.Delay(1000);
    }
    /// The chevron narrows the column to icons, the editor widening beside it, and expands it again.
    static async Task ToggleIcons(IntPtr strip, IntPtr front, int expanded, Func<Task> whileNarrow=null)
    {
        await ClickAt(strip,HeaderPoint(strip),"Chevron");
        await Until("The chevron did not narrow the column to icons",()=>ColumnWidth(strip,48) && FillsWorkspace(front,strip),Settle);
        if(whileNarrow!=null) { await whileNarrow(); front=GetForegroundWindow(); }
        await ClickAt(strip,HeaderPoint(strip),"Chevron");
        await Until("The chevron did not expand the column again",()=>ColumnWidth(strip,expanded) && FillsWorkspace(front,strip),Settle);
        await Activated(front,strip);
    }
    /// Maximizing the front editor gives it back its workspace frame beside the column.
    static async Task MaximizeFront(IntPtr strip)
    {
        var front=GetForegroundWindow();
        Remember("maximize",new { front=front.ToInt64() });
        HelperForm(front).WindowState=FormWindowState.Maximized;
        await Until("A maximized editor did not return to its workspace frame",()=>FillsWorkspace(front,strip),Settle);
        await Activated(front,strip);
    }
    /// Minimizing the front editor minimizes the workspace and hides its column; restoring it brings both back.
    static async Task MinimizeFront(IntPtr strip)
    {
        var front=GetForegroundWindow();
        Remember("minimize and restore",new { front=front.ToInt64() });
        HelperForm(front).WindowState=FormWindowState.Minimized;
        await Until("Minimizing did not minimize every editor and hide the column",()=>Helpers().All(IsIconic) && !IsWindowVisible(strip),Settle);
        ShowWindow(front,9);
        await Until("Restoring did not restore the editor in front",()=>!IsIconic(front) && IsWindowVisible(strip),Settle);
        await Activated(front,strip);
    }
    /// Closes an editor; the workspace keeps the others and a new editor window joins it on its own.
    static async Task<List<IntPtr>> CloseAndReplace(IntPtr strip, List<IntPtr> order, IntPtr closing)
    {
        var area=Screen.PrimaryScreen.WorkingArea;
        Remember("close and replace",new { closing=closing.ToInt64() });
        HelperForm(closing).Close(); order.Remove(closing);
        Forms.RemoveAll(f=>f.IsDisposed);
        await Until("Closed editor kept its row or the column",()=>!IsWindow(closing) && order.Contains(GetWindow(strip,4)),Settle);
        // The column drops the closed editor's row just after the editor in front changes.
        await Activated(GetWindow(strip,4),strip);
        await Task.Delay(1000);
        step="after close";
        await ReadOrder(strip,order.Count,order);
        var replacement=NewHelper(new Point(area.Left+60,area.Top+530)).Handle;
        var expected=order.Concat(new[]{replacement}).ToList();
        await Until("A new editor window did not join the workspace",()=>Strips().Count==1 && OneTaskbarButton(expected),15000);
        await Task.Delay(1000);
        return await ReadOrder(strip,expected.Count,expected);
    }
    static async Task RandomMany(IntPtr strip, List<IntPtr> order, Random random, int expanded)
    {
        for(int i=0;i<30;i++)
        {
            var action=Plan(random);
            step="random action "+(++randomActionCount)+" kind "+action.Kind;
            // Record logical targets before input; HWNDs are diagnostic, not replay identifiers.
            File.AppendAllText(Path.Combine(Root,"random-plan.jsonl"),Json.Serialize(new { seed,phase,index=randomActionCount,action })+Environment.NewLine);
            Check(order.Count==3 && new HashSet<IntPtr>(order).SetEquals(Helpers()),"Random model lost a fixture window");
            int current=order.IndexOf(GetForegroundWindow());
            Check(current>=0,"Foreground left the fixture before a random action");
            int selected=action.Target;
            switch(action.Kind)
            {
                case 0: await Click(strip,selected); await Activated(order[selected],strip); totalSwitches++; break;
                case 1: await Click(strip,(selected+1)%3); await Activated(order[(selected+1)%3],strip); totalSwitches++; break;
                case 2:
                    await ToggleSidebar(strip,order[current]); break;
                case 3:
                    await ToggleIcons(strip,order[current],expanded,async ()=> { await Click(strip,selected); await Activated(order[selected],strip); });
                    totalSwitches++; break;
                case 4:
                    foreach(int digit in action.Digits) { await Click(strip,digit); await Task.Delay(15); }
                    await Activated(order[action.Digits.Last()],strip); totalSwitches++; break;
                case 5: await MaximizeFront(strip); break;
                case 6: await MinimizeFront(strip); break;
                default:
                    var updated=await CloseAndReplace(strip,order,order[selected]); order.Clear(); order.AddRange(updated);
                    break;
            }
            Check(order.Contains(GetForegroundWindow()),"Random action left foreground outside the fixture model");
            await Activated(GetForegroundWindow(),strip);
            Check(Strips().Count==1 && OneTaskbarButton(order) && order.All(IsWindow) && new HashSet<IntPtr>(order).SetEquals(Helpers()),"Random action left missing, stale or extra groups");
            Check(!HasCrash(),"Application logged an exception during a random action");
            if(i%10==9) { ResourceSamples.Add(new { phase,randomActions=randomActionCount,resources=Resources() }); Log("Verified random actions: "+randomActionCount+" seed="+seed); }
        }
        await ReadOrder(strip,3,order);
    }
    // A menu item's bounds on screen from the window's MSAA tree, or null when it has none by that name.
    static Rect? MenuItemRect(IntPtr window, string name)
    {
        var iid=new Guid("618736E0-3C3D-11CF-810C-00AA00389B71"); // IAccessible
        object found;
        if(AccessibleObjectFromWindow(window,0xFFFFFFFC,ref iid,out found)!=0) return null; // OBJID_CLIENT
        var menu=found as Accessibility.IAccessible;
        if(menu==null) return null;
        int count; try { count=menu.accChildCount; } catch(COMException) { return null; }
        var children=new object[count]; int obtained;
        if(count==0 || AccessibleChildren(menu,0,count,children,out obtained)!=0) return null;
        foreach(var child in children.Take(obtained))
        {
            var entry=child as Accessibility.IAccessible;
            if(entry==null) continue;
            try
            {
                var text=entry.get_accName(0);
                if(text==null || text.Replace("&","")!=name) continue;
                int left,top,width,height; entry.accLocation(out left,out top,out width,out height,0);
                return new Rect{ Left=left, Top=top, Right=left+width, Bottom=top+height };
            }
            catch(COMException) { }
        }
        return null;
    }
    static async Task ExitNormally()
    {
        // Exercise the shipped NotifyIcon context-menu Quit handler. This bypasses
        // Explorer icon discovery only, using Framework NotifyIcon's tray callback.
        Check(AllowSetForegroundWindow(app.Id),"Cannot grant the tray-menu foreground permission");
        var candidates=Windows(app.Id).Where(h=>!IsWindowVisible(h) && Class(h).StartsWith("WindowsForms10.")).ToList();
        foreach(var h in candidates) PostMessage(h,0x800,IntPtr.Zero,new IntPtr(0x205));
        // The menu is a WinForms strip in the app's theme, not a system menu: its items are
        // found by name through MSAA, which reaches across processes.
        var popup=IntPtr.Zero; var item=new Rect();
        await Until("Tray context menu did not open",()=>{
            foreach(var h in Windows(app.Id).Where(IsWindowVisible))
            {
                var found=MenuItemRect(h,"Quit IDE-tabs");
                if(found.HasValue) { popup=h; item=found.Value; return true; }
            }
            return false;
        });
        await Until("Tray menu did not acquire keyboard focus",()=>Windows(app.Id).Contains(GetForegroundWindow()));
        Log("Tray Quit item at "+item.Left+","+item.Top+" "+(item.Right-item.Left)+"x"+(item.Bottom-item.Top));
        var point=new Point((item.Left+item.Right)/2,(item.Top+item.Bottom)/2);
        Check(WindowFromPoint(point)==popup,"Quit menu item is occluded");
        Move(point); Inject(MouseInput(2)); await Task.Delay(30); Inject(MouseInput(4));
        var timer=Stopwatch.StartNew();
        while(!app.HasExited) { Check(timer.ElapsedMilliseconds<10000,"Quit did not terminate the process"); await Task.Delay(20); }
        Check(app.ExitCode==0,"Quit returned "+app.ExitCode);
        int exitedPid=app.Id;
        Check(Helpers().All(IsWindow),"Quitting IDE-tabs closed an editor window");
        app.Dispose(); app=null;
        // USER window teardown can finish just after the process handle signals.
        await Until("Native windows survived process exit",()=>Windows(exitedPid).Count==0,2000);
    }
    static void Fixture()
    {
        File.WriteAllText(Path.Combine(Root,"IDE-tabs-settings.json"),Json.Serialize(new {
            runAtStartup=false,language="en",editorCursor=true,editorVSCode=true,tabColumnWidth=180
        }));
    }
    static void Preflight()
    {
        Check(Environment.UserInteractive && Process.GetCurrentProcess().SessionId!=0,"Run the runner in an interactive user session, not as a Windows service");
        Check(Process.GetProcessesByName("IDE-tabs").Length==0,"IDE-tabs is running; quit it first");
        // IDE-tabs would take real editor windows into the fixture's workspace.
        int own=Process.GetCurrentProcess().Id;
        Check(Process.GetProcessesByName("Code").All(p=>p.Id==own) && Process.GetProcessesByName("Cursor").Length==0,"Close Cursor and VS Code first");
        var desk=OpenInputDesktop(0,false,1);
        Check(desk!=IntPtr.Zero,"Desktop is locked or inaccessible");
        try { var name=new StringBuilder(256); uint needed; Check(GetUserObjectInformation(desk,2,name,512,out needed) && name.ToString()=="Default","Use the unlocked Default desktop"); }
        finally { CloseDesktop(desk); }
        foreach(int key in new[]{1,2,0x10,0x11,0x12}) Check(GetAsyncKeyState(key)>=0,"Release mouse and modifier keys before testing");
        Check(Screen.PrimaryScreen.WorkingArea.Width>=1000 && Screen.PrimaryScreen.WorkingArea.Height>=700,"Desktop must provide at least 1000x700 physical pixels");
        Check(Marshal.SizeOf(typeof(Input))==28,"Incorrect x86 INPUT layout");
    }
    static async Task Run(int switches, int cycles, int durationMinutes)
    {
        await Until("Release mouse and modifier keys before testing",()=>new[]{1,2,0x10,0x11,0x12}.All(key=>GetAsyncKeyState(key)>=0),10000);
        Preflight(); GetCursorPos(out savedCursor); inputOwned=true; Fixture();
        var area=Screen.PrimaryScreen.WorkingArea;
        const int expanded=180;
        for(int cycle=0;cycle<cycles;cycle++)
        {
            phase="cycle "+(cycle+1)+" startup"; Log(phase);
            var positions=new[]{new Point(area.Left+40,area.Top+90),new Point(area.Left+400,area.Top+310),new Point(area.Left+60,area.Top+530)};
            // Retain surviving foreign HWNDs across app restarts, then replenish
            // the editor closed by the previous cycle.
            Forms.RemoveAll(f=>f.IsDisposed);
            while(Forms.Count<3) {
                var helper=new Form { Text=EditorTitle() };
                helper.Menu=new MainMenu(new[]{new MenuItem("&File",new[]{new MenuItem("&Test")})});
                Forms.Add(helper);
            }
            for(int i=0;i<3;i++)
            {
                var f=Forms[i];
                f.StartPosition=FormStartPosition.Manual; f.WindowState=FormWindowState.Normal;
                f.Location=positions[i]; f.Size=new Size(560,150);
                f.BackColor=new[]{Color.LightBlue,Color.LightGreen,Color.LightPink}[i];
                f.Show(); ShowWindow(f.Handle,4); // Override the launcher's initial SW_HIDE.
            }
            var handles=Helpers();
            var before=handles.ToDictionary(h=>h,Bounds);
            app=Process.Start(new ProcessStartInfo(Path.Combine(Root,"IDE-tabs.exe")) { WorkingDirectory=Root,UseShellExecute=false });
            // A busy desktop can take IDE-tabs over ten seconds to start.
            await Until("Startup did not gather the three editor windows into one fitted workspace with one taskbar button",()=>
                Strips().Count==1 && OneTaskbarButton(handles) && FillsWorkspace(GetWindow(Strips().Single(),4),Strips().Single()),30000);
            Check(!HasCrash(),"App logged an exception during startup");
            IntPtr strip=Strips().Single();
            await Until("The column did not open at its expanded width",()=>ColumnWidth(strip,expanded),Settle);
            var order=await ReadOrder(strip,3,handles);
            phase="cycle "+(cycle+1)+" editor shortcuts";
            // Ctrl+1-9 and Ctrl+Alt+arrows belong to the editor; IDE-tabs must not switch projects on them.
            var front=GetForegroundWindow();
            Chord(0x11,0x31+(order.IndexOf(front)+1)%3); await Task.Delay(300);
            Chord(0x11,0x12,0x27); await Task.Delay(300);
            Check(GetForegroundWindow()==front,"An editor shortcut switched projects");
            phase="cycle "+(cycle+1)+" sidebar";
            await ToggleSidebar(strip,front);
            await ToggleIcons(strip,front,expanded);
            phase="cycle "+(cycle+1)+" frequent switching";
            var switching=Stopwatch.StartNew();
            int[] switchingBaseline=null;
            var random=new Random(unchecked(seed+cycle));
            int batches=0;
            // Keep one application/workspace alive for the requested duration in
            // each cycle, then exercise normal quit and restart as usual.
            do
            {
                var baseline=await SwitchMany(strip,order,switches);
                if(switchingBaseline==null) switchingBaseline=baseline;
                if(randomize) await RandomMany(strip,order,random,expanded);
                // A slow leak must not disappear by resetting the baseline on
                // every chunk in a prolonged run.
                CheckGrowth(switchingBaseline,Resources());
                batches++;
            }
            while(randomBatchesPerCycle>0 ? batches<randomBatchesPerCycle : switching.Elapsed.TotalSeconds < durationMinutes*60.0/cycles);
            switchingSeconds+=switching.Elapsed.TotalSeconds;
            phase="cycle "+(cycle+1)+" icons-only switching";
            await ToggleIcons(strip,GetForegroundWindow(),expanded,async ()=> { await SwitchMany(strip,order,30); });
            phase="cycle "+(cycle+1)+" maximize and minimize";
            await MaximizeFront(strip);
            await MinimizeFront(strip);
            phase="cycle "+(cycle+1)+" close and join";
            order=await CloseAndReplace(strip,order,GetForegroundWindow());
            await SwitchMany(strip,order,30);
            phase="cycle "+(cycle+1)+" normal quit";
            var last=GetForegroundWindow();
            await ExitNormally();
            Check(Helpers().All(h=>GetWindow(h,4)==IntPtr.Zero && IsWindowVisible(h)),"Quit left an editor owned by IDE-tabs or hidden");
            if(before.ContainsKey(last))
            {
                var r=Bounds(last); var b=before[last];
                await Until("Quit did not give the editor in front its own placement back",()=> { r=Bounds(last); return Near(r.Left,b.Left) && Near(r.Top,b.Top) && Near(r.Right,b.Right) && Near(r.Bottom,b.Bottom); });
            }
            var saved=Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(Root,"IDE-tabs-settings.json")));
            Check(saved.ContainsKey("version") && !string.IsNullOrEmpty((string)saved["version"]),"Startup did not persist its settings version");
            Check(saved.ContainsKey("ideMode") && (bool)saved["ideMode"] && (bool)saved["editorVSCode"],"IDE Mode or the VS Code setting was lost");
            Check(!HasCrash(),"Application logged an exception during the cycle");
            completed++; Log("PASS: cycle "+completed+"; original entry point, automatic workspace, sidebar, switching, close, join and normal quit");
        }
    }
    [STAThread] static int Main(string[] args)
    {
        if(args.Length==1 && args[0]=="--desktop-anchor") { RunDesktopAnchor(); return 0; }
        if(args.Length==1 && args[0]=="--check-random-plan") { CheckRandomPlan(); return 0; }
        bool virtualDesktops=args.Length==1 && args[0]=="--virtual-desktops";
        if(!virtualDesktops && ((args.Length!=3 && args.Length!=4 && args.Length!=7) || args[0]!="--run")) { Console.Error.WriteLine("Use Run-DesktopE2E.ps1 -Interactive"); return 2; }
        if(args.Length==7) { seed=int.Parse(args[4]); randomize=bool.Parse(args[5]); randomBatchesPerCycle=int.Parse(args[6]); }
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        Application.EnableVisualStyles();
        int code=1;
        using(var context=new ApplicationContext())
        using(var start=new Timer { Interval=50 })
        {
            start.Tick+=async (s,e)=> {
                start.Stop();
                try { if(virtualDesktops) await RunVirtualDesktops(); else await Run(int.Parse(args[1]),int.Parse(args[2]),args.Length>=4?int.Parse(args[3]):0); code=0; }
                catch(Exception error) { Console.Error.WriteLine(error); File.WriteAllText(Path.Combine(Root,"failure.txt"),error.ToString()); Diagnose(); }
                finally
                {
                    if(app!=null) { if(!app.HasExited) app.Kill(); app.WaitForExit(); app.Dispose(); }
                    app=null;
                    foreach(var f in Forms) f.Dispose();
                }
                // The Framework compiler supports C# 5, before await-in-finally.
                if(virtualDesktops) {
                    cleaningUp=true;
                    try { await CleanupVirtualDesktops(); }
                    catch(Exception error) { code=1; Console.Error.WriteLine(error); File.AppendAllText(Path.Combine(Root,"failure.txt"),Environment.NewLine+"Cleanup: "+error); }
                }
                if(inputOwned) { Inject(MouseInput(4),KeyInput(0x11,true),KeyInput(0x12,true),KeyInput(0x5B,true)); Move(savedCursor); }
                File.WriteAllText(Path.Combine(Root,"result.json"),Json.Serialize(new { status=code==0?"passed":"failed",phase,step,seed,randomize,randomActionCount,completedCycles=completed,verifiedSwitches=totalSwitches,switchingSeconds,virtualDesktops,virtualDesktopScenarios=DesktopScenarios,temporaryDesktopsRemoved,originalDesktopRestored,measurements=Measurements,resourceSamples=ResourceSamples,recentActions=RecentActions.ToArray() }));
                context.ExitThread();
            };
            start.Start(); Application.Run(context);
        }
        return code;
    }
}
