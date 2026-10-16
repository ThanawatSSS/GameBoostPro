!macro GBP_RECOVERY_GUARD
  ; Hold both transaction locks until the installer process exits. Do not let
  ; Restart Manager terminate an app that still owns a recovery transaction.
  System::Call 'kernel32::CreateMutexW(p 0, i 1, w "Local\Codex.GameBoostPro") p.r9 ?e'
  Pop $8
  StrCmp $9 0 0 +3
    MessageBox MB_ICONSTOP "Cannot lock Game Boost Pro. Close the app before updating or removing it."
    Abort
  StrCmp $8 183 0 +3
    MessageBox MB_ICONSTOP "Close Game Boost Pro, including its tray icon, before updating or removing it."
    Abort
  System::Call 'kernel32::CreateMutexW(p 0, i 1, w "Global\Codex.GameBoostPro.RustV4") p.r9 ?e'
  Pop $8
  StrCmp $9 0 0 +3
    MessageBox MB_ICONSTOP "Cannot reserve the Game Boost transaction lock."
    Abort
  StrCmp $8 183 0 +3
    MessageBox MB_ICONSTOP "Game Boost Pro is running in another Windows session. Restore and close it first."
    Abort
  ; Enumerate names rather than interpreting recovery values as strings.
  !insertmacro GBP_REG_GUARD 64
  !insertmacro GBP_REG_GUARD 32
  ReadEnvStr $0 "LOCALAPPDATA"
  IfFileExists "$0\CodexGameBoost\state-pro.json" 0 +3
    MessageBox MB_ICONSTOP "Restore the legacy Boost session before updating or removing the app."
    Abort
  IfFileExists "$0\CodexGameBoost\state.json" 0 +3
    MessageBox MB_ICONSTOP "Restore the legacy Boost session before updating or removing the app."
    Abort
  SetRegView 64
!macroend

!macro GBP_REG_GUARD VIEW
  SetRegView ${VIEW}
  StrCpy $1 0
  ${Do}
    ClearErrors
    EnumRegValue $0 HKLM "SOFTWARE\GameBoostPro" $1
    ${If} ${Errors}
      ${ExitDo}
    ${EndIf}
    ${If} $0 == "RecoveryState"
    ${OrIf} $0 == "RecoveryV4"
      MessageBox MB_ICONSTOP "A recovery snapshot exists. Restore the Boost session before updating or removing the app."
      Abort
    ${EndIf}
    IntOp $1 $1 + 1
  ${Loop}
!macroend

!macro NSIS_HOOK_PREINSTALL
  !insertmacro GBP_RECOVERY_GUARD
  SetRegView 64
!macroend

!macro NSIS_HOOK_POSTINSTALL
  ; Keep the existing product's fixed location and Start entry during migration.
  SetShellVarContext all
  CreateDirectory "$SMPROGRAMS\Game Boost Pro"
  CreateShortCut "$SMPROGRAMS\Game Boost Pro\Game Boost Pro.lnk" "$INSTDIR\gameboostpro.exe"
  ReadRegStr $0 HKLM "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\GameBoostPro" "InstallLocation"
  StrCmp $0 "$INSTDIR" 0 +2
    DeleteRegKey HKLM "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\GameBoostPro"
!macroend

!macro NSIS_HOOK_PREUNINSTALL
  !insertmacro GBP_RECOVERY_GUARD
!macroend
