namespace Bemo
open System

/// An editor IDE Mode supports once it has been tested with it, and the setting that turns it off.
type SupportedEditor = { id: string; name: string; executables: string list; setting: string }

/// IDE Mode: tabs only for the supported editors, each editor's windows gathered into a workspace
/// of their own beside its project column, each tab named after its project.
module IdeMode =
    let editors =
        [ { id="cursor"; name="Cursor"; executables=["CURSOR.EXE"]; setting="editorCursor" }
          { id="vscode"; name="VS Code"; executables=["CODE.EXE"]; setting="editorVSCode" } ]
    let editorOf path = editors |> List.tryFind(fun editor -> List.contains (AppIdentity.key path) editor.executables)
    /// Read through the settings cache, so group threads do not wait on the main thread.
    let isEnabled editor = unbox<bool>(Services.settings.getValue editor.setting)
    let isSupported path = editorOf path |> Option.exists isEnabled
    /// Each editor's workspace is the rule group named after it, so editors are never mixed; a
    /// tab dragged out starts a group of its own instead.
    let private workspacePrefix = "ide-workspace:"
    let workspaceId editor = workspacePrefix + editor.id
    let isWorkspaceId (id:string option) = id |> Option.exists(fun value -> value.StartsWith(workspacePrefix,StringComparison.Ordinal))
    let groupingTargetWith enabled path = editorOf path |> Option.filter enabled |> Option.map workspaceId
    let groupingTarget path = groupingTargetWith isEnabled path
    /// A window joins a group only if it is of the same editor as every window already in it.
    let mayJoin path (others:string list) =
        let id p = editorOf p |> Option.map(fun editor -> editor.id)
        others |> List.forall(fun other -> id other=id path)
    /// IDE Mode's only global shortcut. The editors use the others themselves: Ctrl+1-9 picks
    /// an editor group and Ctrl+Alt+arrows move an editor between groups.
    let hotKeys = ["toggleSidebar"]
    /// Read through the settings cache, so group threads do not wait on the main thread.
    let isOn() = unbox<bool>(Services.settings.getValue "ideMode")

    /// Turning IDE Mode on sets these once; they stay ordinary settings the user can change. Tabs
    /// stay visible on maximized editors, and Alt+Tab stays the native switcher.
    let preset : (string * obj) list =
        ["tabPosition",box "Left";"tabColumnWidth",box 180;"combineIconsInTaskbar",box true;"replaceAltTab",box false;"autoHideMode",box "Never"]

    /// Two letters that stand for a tab's label in the narrow column, where every editor's icon
    /// is the same: the first letters of its first two words ("project-bravo", "ProjectAlpha"), else
    /// its first letter and a number it ends with ("charlie2"), else its first two letters.
    let initials (label:string) =
        if String.IsNullOrWhiteSpace label then None
        else
            let words =
                Text.RegularExpressions.Regex.Matches(label,@"[A-Z]?[a-z]+|[A-Z]+(?![a-z])|\d+")
                |> Seq.cast<Text.RegularExpressions.Match> |> Seq.map(fun m -> m.Value) |> Seq.toList
            let letters = words |> List.filter(fun word -> Char.IsLetter word.[0])
            let text =
                match letters,List.tryLast words with
                | first::_,Some last when Char.IsDigit last.[0] -> string first.[0] + string last.[last.Length-1]
                | first::second::_,_ -> string first.[0] + string second.[0]
                | [only],_ -> only.Substring(0,min 2 only.Length)
                | [],_ -> label.Trim().Substring(0,min 2 (label.Trim().Length))
            Some(text.ToUpperInvariant())

    /// What IDE-tabs needs and does not offer to change: IDE Mode itself, one taskbar button
    /// and so one native Alt+Tab entry per workspace, and a sidebar that stays on the left.
    let required : (string * obj) list =
        ["ideMode",box true;"combineIconsInTaskbar",box true;"replaceAltTab",box false;"tabPosition",box "Left";"autoHideMode",box "Never"]

    /// The visible frame an editor fills: its monitor's work area beside the column, so the
    /// column sits next to it rather than over it.
    let workspaceFrame (work:Rect) (dock:TabDock) (column:int) =
        let column = between (0,max 0 (work.width-1)) column
        if dock=TabDockRight then Rect(work.location,Sz(work.width-column,work.height))
        else Rect(Pt(work.x+column,work.y),Sz(work.width-column,work.height))

    /// Each editor's placement from before it joined the workspace, given back when it leaves it.
    /// Group threads fill it and the main thread reads it when a tab is dropped on the desktop.
    module Restore =
        let private saved = Collections.Concurrent.ConcurrentDictionary<IntPtr,OSWindowPlacement>()
        let remember hwnd (placement:OSWindowPlacement) = saved.TryAdd(hwnd,placement) |> ignore
        let take hwnd =
            match saved.TryRemove hwnd with
            | true,placement -> Some placement
            | _ -> None
        let forget hwnd = saved.TryRemove hwnd |> ignore
        /// Back to maximized, or restored without taking the focus; never back to minimized.
        let showCmd (placement:OSWindowPlacement) =
            if placement.showCmd=ShowWindowCommands.SW_SHOWMAXIMIZED then ShowWindowCommands.SW_SHOWMAXIMIZED
            else ShowWindowCommands.SW_SHOWNOACTIVATE

    /// Each editor's default window title ends with its name.
    let private appSuffixes = [" - Cursor";" - Visual Studio Code"]
    let private separator = " - "

    /// The project folder in the editors' default title, "[file - ]folder - App". "name - App"
    /// alone may be a file opened without a folder, so it names no project.
    let projectName (title:string) =
        if isNull title then None
        else
            appSuffixes |> List.tryPick(fun suffix ->
                if title.EndsWith(suffix,StringComparison.Ordinal) then
                    let rest = title.Substring(0,title.Length-suffix.Length)
                    match rest.LastIndexOf(separator,StringComparison.Ordinal) with
                    | -1 -> None
                    | index -> Some(rest.Substring(index+separator.Length).Trim())
                else None)
            |> Option.filter(String.IsNullOrWhiteSpace >> not)

    /// A tab's label: its project, unless another tab of the group shows the same project or the
    /// title names none; then the whole title, so two windows are never told apart by a guess.
    let label (title:string) (otherTitles:string list) =
        match projectName title with
        | Some name when otherTitles |> List.forall(fun other -> projectName other<>Some name) -> name
        | _ -> title
