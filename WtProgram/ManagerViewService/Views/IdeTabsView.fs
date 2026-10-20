namespace Bemo
open System
open System.Windows.Forms

/// IDE-tabs' only settings page: the editors it manages, starting with Windows and the shortcut
/// that hides the sidebar. Everything else keeps the values IDE-tabs needs; the troubleshooting
/// report and settings recovery open in a window of their own.
type IdeTabsView(openTroubleshooting:unit -> unit) =
    let panel,table = SettingsUi.page()
    do
        // The window shows no caption, so the page names the app itself.
        let title = new Label(Text="IDE-tabs",AutoSize=true,Font=SettingsUi.sectionFont(),Margin=Padding(0,0,0,Dpi.scale 4))
        SettingsUi.add table title
        SettingsUi.note table (tr Strings.General.ideTabsIntro)
        let applications = SettingsUi.sectionCard table (tr Strings.General.applications)
        for editor in IdeMode.editors do SettingsBindings.toggleRow applications ("editor-"+editor.id)
        let preferences = SettingsUi.sectionCard table (tr Strings.General.preferences)
        SettingsBindings.toggleRow preferences "launch-at-sign-in"
        let shortcut = new SettingsShortcutInput(Font=SettingsUi.bodyFont(),Shortcut=Services.program.getHotKey "toggleSidebar")
        shortcut.Changed.Add(fun _ ->
            let previous = Services.program.getHotKey "toggleSidebar"
            if not (Services.program.setHotKey "toggleSidebar" shortcut.Shortcut) then shortcut.Reject(previous,tr Strings.Shortcuts.inUse))
        SettingsUi.settingRow preferences "hide-sidebar" shortcut
        let footer = new FlowLayoutPanel(AutoSize=true,WrapContents=true,Margin=Padding(0,Dpi.scale 20,0,0))
        let version = new Label(Text=tr (Strings.General.version AssemblyInfo.informationalVersion),AutoSize=true,Tag="muted",
                                UseMnemonic=false,Margin=Padding(Dpi.scale 3,Dpi.scale 8,Dpi.scale 15,Dpi.scale 3))
        let report = new SettingsLink(tr Strings.Diagnostics.reportIssue,WebLink,Margin=Padding(Dpi.scale 3,Dpi.scale 8,Dpi.scale 15,Dpi.scale 3))
        report.Click.Add(fun _ -> try Diagnostics.Process.Start("https://github.com/Pindaman77/IDE-tabs/issues/new") |> ignore with _ -> ())
        let troubleshooting = SettingsUi.button (tr Strings.General.troubleshooting)
        troubleshooting.Name <- "troubleshooting"
        troubleshooting.Margin <- Padding(0,Dpi.scale 2,0,0)
        troubleshooting.Click.Add(fun _ -> openTroubleshooting())
        footer.Controls.AddRange([|version :> Control;report;troubleshooting|])
        SettingsUi.add table footer
    interface ISettingsView with
        member _.key = GeneralSettings
        member _.title = "IDE-tabs"
        member _.control = panel :> Control
