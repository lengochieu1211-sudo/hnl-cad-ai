!macro hnlRefreshShortcut shortcutPath
  ${if} ${FileExists} "${shortcutPath}"
    CreateShortCut "${shortcutPath}" "$appExe" "" "$INSTDIR\resources\icon.ico" 0 "" "" "${APP_DESCRIPTION}"
    ClearErrors
    WinShell::SetLnkAUMI "${shortcutPath}" "${APP_ID}"
  ${endIf}
!macroend

!macro customInstall
  ${if} ${FileExists} "$INSTDIR\resources\icon.ico"
    !insertmacro hnlRefreshShortcut "$newStartMenuLink"
    !insertmacro hnlRefreshShortcut "$newDesktopLink"
    WriteRegStr SHELL_CONTEXT "${UNINSTALL_REGISTRY_KEY}" "DisplayIcon" "$INSTDIR\resources\icon.ico"
    System::Call 'Shell32::SHChangeNotify(i 0x8000000, i 0, i 0, i 0)'
  ${endIf}
!macroend
