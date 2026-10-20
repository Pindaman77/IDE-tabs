#r "System.Drawing"
#r "System.Windows.Forms"
#r "Debug/Win32.dll"
#r "Debug/IDE-tabs.exe"
open System
open System.IO
open System.Threading
open System.Windows.Forms
open Bemo

let check condition message = if not condition then failwith message
let pumpUntil description predicate =
    let clock = Diagnostics.Stopwatch.StartNew()
    let mutable satisfied = predicate()
    while not satisfied && clock.ElapsedMilliseconds<10000L do
        Application.DoEvents()
        Thread.Sleep(5)
        satisfied <- predicate()
    check satisfied description
let pump milliseconds =
    let clock = Diagnostics.Stopwatch.StartNew()
    while clock.ElapsedMilliseconds<int64 milliseconds do
        Application.DoEvents()
        Thread.Sleep(5)

let projectLabels() =
    let project title = IdeMode.projectName title
    check (project "page.tsx - alpha - Cursor" = Some "alpha") "A Cursor title did not give its project folder"
    check (project "index.ts - project-bravo - Visual Studio Code" = Some "project-bravo") "A VS Code title did not give its project folder"
    check (project "● main.fs - IDE-tabs - Visual Studio Code" = Some "IDE-tabs") "An unsaved file's title did not give its project folder"
    check (project "Notes - Release plan - project-bravo - Cursor" = Some "project-bravo") "A title whose first part holds a separator gave the wrong project"
    // "name - App" may be a file opened without a folder: no project is guessed from it.
    for title in ["alpha - Cursor";"Cursor";"Cursor Agents";"notes.txt - projA - Notepad";"";null] do
        check (project title = None) (sprintf "A title naming no project gave one: %A" title)
    check (IdeMode.label "a.ts - crm - Cursor" ["x.ts - website - Cursor"] = "crm") "A unique project was not used as the tab's label"
    check (IdeMode.label "a.ts - crm - Cursor" ["b.ts - crm - Cursor"] = "a.ts - crm - Cursor") "Two windows of one project were both labelled by the project"
    check (IdeMode.label "alpha - Cursor" [] = "alpha - Cursor") "A title naming no project did not keep the whole title"
    let editor path = IdeMode.editorOf path |> Option.map(fun e -> e.id)
    check (editor @"C:\Users\me\AppData\Local\Programs\cursor\Cursor.exe" = Some "cursor" && editor @"D:\VS Code\code.exe" = Some "vscode")
          "Cursor or VS Code was not a supported editor"
    check (editor @"C:\Program Files\Google\Chrome\Application\chrome.exe" = None && editor @"C:\t\CursorTest.exe" = None)
          "An app other than the supported editors was supported"

let workspaceHelpers() =
    let target = IdeMode.groupingTargetWith (fun _ -> true)
    let cursor,code = @"C:\cursor\Cursor.exe",@"C:\vscode\Code.exe"
    check ((target cursor).IsSome && (target code).IsSome && target cursor<>target code && IdeMode.isWorkspaceId (target cursor))
          "Cursor and VS Code did not each get a workspace of their own"
    check (target @"C:\Program Files\Google\Chrome\Application\chrome.exe" = None) "An app other than the editors belonged in a workspace"
    check (IdeMode.groupingTargetWith (fun e -> e.id<>"vscode") code = None) "An editor turned off still joined a workspace"
    check (not (IdeMode.isWorkspaceId (Some "ide-group")) && not (IdeMode.isWorkspaceId None)) "An app rule's group counted as a workspace"
    check (IdeMode.mayJoin cursor [cursor;cursor] && not (IdeMode.mayJoin code [cursor]) && not (IdeMode.mayJoin cursor [cursor;code]))
          "Windows of different editors could share a group"
    let work = Rect(Pt(0,40),Sz(1920,1000))
    check (IdeMode.workspaceFrame work TabDockLeft 270 = Rect(Pt(270,40),Sz(1650,1000))) "The workspace did not leave the column's width on the left"
    check (IdeMode.workspaceFrame work TabDockRight 72 = Rect(Pt(0,40),Sz(1848,1000))) "The workspace did not leave the column's width on the right"
    check ((IdeMode.workspaceFrame work TabDockLeft 5000).width=1) "A column wider than the screen left the editor no width"
    check (List.contains ("tabColumnWidth",box 180) IdeMode.preset) "Turning IDE Mode on did not set the 180-pixel column"
    for label,expected in ["project-bravo","PB";"ProjectAlpha","PA";"charlie2","C2";"alpha","AL";"IDE-tabs","IT";"Cursor Agents","CA";"项目","项目"] do
        check (IdeMode.initials label = Some expected) (sprintf "%s did not stand for %s in the narrow column" expected label)
    check (IdeMode.initials "  " = None) "A blank label had initials"
    let placement showCmd = { flags=0;showCmd=showCmd;ptMinPosition=Pt();ptMaxPosition=Pt();rcNormalPosition=Rect(Pt(10,10),Sz(800,600)) }
    let hwnd = IntPtr(0x7e57)
    IdeMode.Restore.remember hwnd (placement ShowWindowCommands.SW_SHOWMAXIMIZED)
    IdeMode.Restore.remember hwnd (placement ShowWindowCommands.SW_SHOWNORMAL)
    check ((IdeMode.Restore.take hwnd |> Option.map(fun saved -> saved.showCmd)) = Some ShowWindowCommands.SW_SHOWMAXIMIZED)
          "A later placement replaced the one from before the workspace"
    check (IdeMode.Restore.take hwnd).IsNone "A placement was given back twice"
    check (IdeMode.Restore.showCmd (placement ShowWindowCommands.SW_SHOWMINIMIZED) = ShowWindowCommands.SW_SHOWNOACTIVATE)
          "Leaving the workspace would minimize an editor or take the focus"

let settingDefinition() =
    check (SettingsCatalog.toggleDefault "ideMode" false=false && SettingsCatalog.toggleDefault "ideMode" true=false) "IDE Mode was on by default"
    check ((SettingsCatalog.find "ide-mode").page=GeneralSettings) "IDE Mode is not on the General page"
    for text in [Strings.Settings.ideMode;Strings.Settings.hideSidebar] do
        for part in [text.caption;text.description;text.keywords] do
            check ([part.en;part.zh;part.ja] |> List.forall(String.IsNullOrWhiteSpace >> not)) "IDE Mode text is missing a translation"
    // Ctrl+Alt+H: H (0x48) in the low byte, Ctrl and Alt in the high one.
    check (SettingsCatalog.shortcutDefault "toggleSidebar" = 0x648) "Hiding the sidebar is not Ctrl+Alt+H by default"
    check ((SettingsCatalog.find "hide-sidebar").page=HotKeySettings) "Hiding the sidebar is not on the Shortcuts page"

let main() =
    projectLabels()
    workspaceHelpers()
    settingDefinition()
    let runtimeDirectory = Path.Combine(__SOURCE_DIRECTORY__,"Debug","runtime-"+Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory(runtimeDirectory) |> ignore
    let previousDirectory = Environment.CurrentDirectory
    Environment.CurrentDirectory <- runtimeDirectory
    try
        let runtimeSettings = new Settings(true,saveDelay=0)
        let api = runtimeSettings :> ISettings
        let path = Diagnostics.Process.GetCurrentProcess().MainModule.FileName
        let chrome = @"C:\Program Files\Google\Chrome\Application\chrome.exe"
        api.setValue("runAtStartup",box false)
        api.setValue("replaceAltTab",box true)
        api.setValue("combineIconsInTaskbar",box false)
        api.setValue("tabPosition",box "Horizontal")
        api.setValue("autoHideMode",box "Maximized")
        api.setValue("enableTabbingByDefault",box true)
        api.setValue("includedPaths",box(Set2(List2([path;chrome]))))
        // This app's windows group by app, and a group remembers one of their titles.
        api.setValue("autoGroupingPaths",box(Set2(List2([path]))))
        runtimeSettings.rememberedGroups <-
            [{ id=6161; seen=DateTime.UtcNow; members=["Remembered A";"Remembered B"] |> List.map(fun title -> { app=AppIdentity.key path; title=title }); paths=Map.empty }]
        use lifetime = new LifetimeScope(fun error -> raise error)
        let program = Program.Program(lifetime,settings=runtimeSettings)
        program.initializePlugins(List2())
        let filter = FilterService()
        check (filter.getIsTabbingEnabledForProcess chrome && (program :> IProgram).getAutoGroupingEnabled path) "The app's own choices did not apply with IDE Mode off"

        api.setValue("ideMode",box true)
        pumpUntil "Turning IDE Mode on did not set its preset" (fun () ->
            api.getValue("tabPosition")=box "Left" && api.getValue("combineIconsInTaskbar")=box true &&
            api.getValue("replaceAltTab")=box false && api.getValue("autoHideMode")=box "Never")
        check (filter.getIsTabbingEnabledForProcess @"C:\cursor\Cursor.exe" && filter.getIsTabbingEnabledForProcess @"C:\vscode\Code.exe")
              "IDE Mode did not give the supported editors tabs"
        // Every supported editor starts on, in new and existing settings; one turned off stays off.
        check (SettingsCatalog.toggleDefault "editorVSCode" false && SettingsCatalog.toggleDefault "editorVSCode" true) "VS Code was not on by default"
        api.setValue("editorVSCode",box false)
        check (not (filter.getIsTabbingEnabledForProcess @"C:\vscode\Code.exe") && filter.getIsTabbingEnabledForProcess @"C:\cursor\Cursor.exe")
              "Turning VS Code off did not leave only Cursor with tabs"
        api.setValue("editorVSCode",box true)
        check (not (filter.getIsTabbingEnabledForProcess chrome) && not (filter.getIsTabbingEnabledForProcess path))
              "IDE Mode gave tabs to an app other than the supported editors"
        check (not ((program :> IProgram).getAutoGroupingEnabled path)) "IDE Mode left automatic grouping on"
        // The editors use Ctrl+1-9 and Ctrl+Alt+arrows themselves; IDE Mode holds only its own shortcut.
        check (not (NumberShortcutRules.enabled @"C:\cursor\Cursor.exe")) "IDE Mode left Ctrl+1-9 switching tabs in an editor"
        check (program.isHotKeyActive "toggleSidebar" && ["nextTab";"prevTab";"searchTabs";"newTab";"closeTab";"numberLeader"] |> List.forall(program.isHotKeyActive >> not))
              "IDE Mode held a global shortcut other than its own"
        // An app found while IDE Mode is on keeps its own choice for when it is off.
        filter.rememberApplications [@"C:\tools\Tool.exe"]
        check (AppIdentity.contains filter.includedPaths @"C:\tools\Tool.exe" && not (AppIdentity.contains filter.excludedPaths @"C:\tools\Tool.exe"))
              "IDE Mode recorded its own answer as a new app's choice"
        check (filter.includedPaths.contains chrome && filter.includedPaths.contains path) "IDE Mode removed an app's own choice"

        // Fixture windows stand in for editor windows: the real filter would not tab this test host.
        use first = new Form(ShowInTaskbar=false,Text="first")
        use second = new Form(ShowInTaskbar=false,Text="second")
        use keptA = new Form(ShowInTaskbar=false,Text="kept A")
        use keptB = new Form(ShowInTaskbar=false,Text="kept B")
        use reopened = new Form(ShowInTaskbar=false,Text="Remembered A")
        use reopenedToo = new Form(ShowInTaskbar=false,Text="Remembered B")
        let fixtures = [first;second;keptA;keptB;reopened;reopenedToo] |> List.map(fun form -> form.Handle)
        let known = ref fixtures
        let mutable discovering = true
        Services.registerLocal<IFilterService>({new IFilterService with
            member _.isAppWindow hwnd = List.contains hwnd known.Value
            member _.isAppWindowStyle hwnd = List.contains hwnd known.Value
            member _.isTabbableWindow hwnd = discovering && List.contains hwnd known.Value
            member _.isTabbingEnabledForAllProcessesByDefault with get()=false and set _=()
            member _.setIsTabbingEnabledForProcess value enabled = ()
            member _.setIsTabbingEnabledForProcesses values enabled = ()
            member _.getIsTabbingEnabledForProcess value = true})
        let groupOf hwnd = Services.desktop.groups.list |> List.tryFind(fun group -> group.windows.contains((=)hwnd))
        let together a b =
            match groupOf a,groupOf b with
            | Some x,Some y -> obj.ReferenceEquals(x,y)
            | _ -> false
        // Windows WindowTabs grouped before it restarted carry their group and go back into it.
        WindowGroupToken.save keptA.Handle 4646
        WindowGroupToken.save keptB.Handle 4646
        for form in [first;second;keptA;keptB;reopened;reopenedToo] do form.Show()
        program.refresh()
        pumpUntil "IDE Mode did not tab its windows" (fun () -> fixtures |> List.forall(fun hwnd -> (groupOf hwnd).IsSome))
        pumpUntil "IDE Mode did not put windows grouped before a restart back together" (fun () -> together keptA.Handle keptB.Handle)
        // Remembered titles would join these (see StartupGrouping); in IDE Mode only a drag does.
        pump 1500
        check (not (together reopened.Handle reopenedToo.Handle)) "IDE Mode grouped windows by a remembered title"
        check ([first;second;reopened;reopenedToo] |> List.forall(fun form -> not (together form.Handle keptA.Handle)))
              "IDE Mode added a window to a restored group automatically"

        // Off again, the app's own choices come back.
        api.setValue("ideMode",box false)
        check (filter.getIsTabbingEnabledForProcess chrome && (program :> IProgram).getAutoGroupingEnabled path) "Turning IDE Mode off did not restore the app's own choices"
        check (NumberShortcutRules.enabled chrome && program.isHotKeyActive "nextTab" && not (program.isHotKeyActive "toggleSidebar"))
              "Turning IDE Mode off did not give the app's shortcuts back"
        check (api.getValue("tabPosition")=box "Left") "Turning IDE Mode off undid settings it had set"
        // Grouping by app and app rules each find a group for a window unless IDE Mode is on. The
        // hidden test desktop never places a window on a virtual desktop, so the choice is checked
        // rather than the windows joining.
        let window = OS().windowFromHwnd(first.Handle)
        check (program.tryAutoGroup(window)).IsSome "Grouping by app did not choose for a window with IDE Mode off"
        api.setValue("ideMode",box true)
        check (program.tryAutoGroup(window)).IsNone "IDE Mode grouped a window by app"
        let rule = { id="ide-rule";enabled=true;processPath=path;title="";matchType= -1;groupId="ide-group";groupName="IDE group" }
        api.setValue("appGroupingRules",box [rule])
        check (program.tryGroupingRule(window)).IsNone "IDE Mode grouped a window by an app rule"
        api.setValue("ideMode",box false)
        check (program.tryGroupingRule(window)).IsSome "An app rule did not choose for a window with IDE Mode off"
        api.setValue("appGroupingRules",box ([]:AppGroupingRule list))

        // The workspace sizes a maximized window beside a full-height column, and gives the
        // maximized state back when the window leaves it or IDE Mode is turned off.
        api.setValue("ideMode",box true)
        use editor = new Form(ShowInTaskbar=false,Text="main.fs - project - Cursor")
        editor.Show()
        editor.WindowState <- FormWindowState.Maximized
        known.Value <- editor.Handle :: known.Value
        let workspace = Services.desktop.createGroup(false) :?> GroupInfo
        workspace.ruleGroupId <- Some(IdeMode.workspaceId IdeMode.editors.Head)
        (workspace :> IGroup).addWindow(editor.Handle,false)
        let editorWindow = OS().windowFromHwnd(editor.Handle)
        let near (a:Rect) (b:Rect) = abs(a.x-b.x)<=1 && abs(a.y-b.y)<=1 && abs(a.right-b.right)<=1 && abs(a.bottom-b.bottom)<=1
        let work = (Mon.fromHwnd editor.Handle).Value.workRect
        // The column is made on the group's thread just after the group.
        let fitted() =
            try
                let column = OS().windowFromHwnd(workspace.hwnd).bounds
                let editor = editorWindow.visibleBounds
                // The column covers the editor's frame edge by a pixel or two.
                not editorWindow.isMaximized && column.left=work.left && column.height=work.height &&
                editor.left<=column.right && editor.left>=column.right-3 &&
                near editor (Rect(Pt(editor.left,work.top),Sz(work.right-editor.left,work.height)))
            with :? NullReferenceException -> false
        let state() =
            let column = try OS().windowFromHwnd(workspace.hwnd).bounds with :? NullReferenceException -> Rect()
            sprintf "editor %A maximized=%b, column %A, work area %A" editorWindow.visibleBounds.record editorWindow.isMaximized column.record work.record
        try pumpUntil "The workspace did not size its editor beside a full-height column" fitted
        with error -> failwithf "%s: %s" error.Message (state())
        // Its shortcut hides the column and the editor takes the whole work area; again shows it.
        workspace.invokeGroup(fun () -> workspace.group.toggleSidebar())
        pumpUntil "Hiding the sidebar did not give the editor the whole work area" (fun () ->
            near editorWindow.visibleBounds work && not (WinUserApi.IsWindowVisible workspace.hwnd))
        workspace.invokeGroup(fun () -> workspace.group.toggleSidebar())
        pumpUntil "Showing the sidebar again did not put the editor beside it" (fun () ->
            fitted() && WinUserApi.IsWindowVisible workspace.hwnd)
        // The chevron at the column's top narrows it to icons only, and the editor widens; again widens it.
        let columnWidth() = OS().windowFromHwnd(workspace.hwnd).bounds.width
        let wide = columnWidth()
        let clickChevron() =
            let point = (10 <<< 16) ||| 10
            WinUserApi.SendMessage(workspace.hwnd,WindowMessages.WM_LBUTTONDOWN,1,point) |> ignore
            WinUserApi.SendMessage(workspace.hwnd,WindowMessages.WM_LBUTTONUP,0,point) |> ignore
        clickChevron()
        pumpUntil "The chevron did not narrow the column beside its editor" (fun () -> columnWidth()<wide && fitted())
        clickChevron()
        pumpUntil "The chevron did not widen the column again" (fun () -> columnWidth()=wide && fitted())
        (workspace :> IGroup).removeWindow(editor.Handle)
        pumpUntil "An editor leaving the workspace was not maximized again" (fun () -> editorWindow.isMaximized)
        (workspace :> IGroup).addWindow(editor.Handle,false)
        pumpUntil "An editor joining the workspace again was not sized beside the column" fitted
        api.setValue("ideMode",box false)
        pumpUntil "Turning IDE Mode off did not maximize the editor again" (fun () -> editorWindow.isMaximized)
        known.Value <- known.Value |> List.filter((<>)editor.Handle)

        discovering <- false
        program.refresh()
        pumpUntil "IDE Mode fixture left group STAs running" (fun () -> Services.desktop.groups.isEmpty)
    finally Environment.CurrentDirectory <- previousDirectory
    printfn "PASS: IDE Mode tabs only the supported editors, gathers them into a workspace beside a full-height column it can collapse and hide, names tabs by project and leaves the app's own choices intact."

TestInit.run main
