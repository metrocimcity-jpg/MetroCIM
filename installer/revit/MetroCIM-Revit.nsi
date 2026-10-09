; MetroCIM for Revit — per-user NSIS installer (no administrator rights)
Unicode true
!include "MUI2.nsh"

; PRODUCT_VERSION / STAGING_DIR / OUT_FILE are passed by Build-RevitInstaller.ps1
!ifndef PRODUCT_VERSION
  !define PRODUCT_VERSION "1.0.0"
!endif
!ifndef STAGING_DIR
  !define STAGING_DIR "artifacts\staging\MetroCIM-Revit"
!endif
!ifndef OUT_FILE
  !define OUT_FILE "dist\MetroCIM-Revit-${PRODUCT_VERSION}.exe"
!endif

!define PRODUCT_NAME "MetroCIM for Revit"
!define PRODUCT_PUBLISHER "Metropolitan CIM"
!define PRODUCT_REGKEY "Software\MetroCIM\Revit"
!define UNINSTALL_REGKEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\MetroCIMRevit"

Name "${PRODUCT_NAME}"
OutFile "${OUT_FILE}"
InstallDir "$APPDATA\MetroCIM\Revit"
RequestExecutionLevel user
ShowInstDetails show
ShowUninstDetails show
SetCompressor /SOLID lzma
BrandingText "${PRODUCT_NAME} ${PRODUCT_VERSION}"

!define MUI_ABORTWARNING
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"

Function .onInit
  SetShellVarContext current
FunctionEnd

Section "Install" SecInstall
  SetShellVarContext current
  SetOverwrite on

  ; Keep a small product folder for the uninstaller
  SetOutPath "$INSTDIR"
  WriteUninstaller "$INSTDIR\Uninstall.exe"

  ; Revit 2025
  SetOutPath "$APPDATA\Autodesk\Revit\Addins\2025"
  File "${STAGING_DIR}\Addins\2025\MetroCIM.addin"
  SetOutPath "$APPDATA\Autodesk\Revit\Addins\2025\MetroCIM"
  File /r "${STAGING_DIR}\Addins\2025\MetroCIM\*.*"

  ; Revit 2026
  SetOutPath "$APPDATA\Autodesk\Revit\Addins\2026"
  File "${STAGING_DIR}\Addins\2026\MetroCIM.addin"
  SetOutPath "$APPDATA\Autodesk\Revit\Addins\2026\MetroCIM"
  File /r "${STAGING_DIR}\Addins\2026\MetroCIM\*.*"

  ; Revit 2027
  SetOutPath "$APPDATA\Autodesk\Revit\Addins\2027"
  File "${STAGING_DIR}\Addins\2027\MetroCIM.addin"
  SetOutPath "$APPDATA\Autodesk\Revit\Addins\2027\MetroCIM"
  File /r "${STAGING_DIR}\Addins\2027\MetroCIM\*.*"

  WriteRegStr HKCU "${PRODUCT_REGKEY}" "InstallDir" "$INSTDIR"
  WriteRegStr HKCU "${PRODUCT_REGKEY}" "Version" "${PRODUCT_VERSION}"

  WriteRegStr HKCU "${UNINSTALL_REGKEY}" "DisplayName" "${PRODUCT_NAME}"
  WriteRegStr HKCU "${UNINSTALL_REGKEY}" "DisplayVersion" "${PRODUCT_VERSION}"
  WriteRegStr HKCU "${UNINSTALL_REGKEY}" "Publisher" "${PRODUCT_PUBLISHER}"
  WriteRegStr HKCU "${UNINSTALL_REGKEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${UNINSTALL_REGKEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegDWORD HKCU "${UNINSTALL_REGKEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNINSTALL_REGKEY}" "NoRepair" 1
SectionEnd

Section "Uninstall"
  SetShellVarContext current

  Delete "$APPDATA\Autodesk\Revit\Addins\2025\MetroCIM.addin"
  RMDir /r "$APPDATA\Autodesk\Revit\Addins\2025\MetroCIM"
  Delete "$APPDATA\Autodesk\Revit\Addins\2026\MetroCIM.addin"
  RMDir /r "$APPDATA\Autodesk\Revit\Addins\2026\MetroCIM"
  Delete "$APPDATA\Autodesk\Revit\Addins\2027\MetroCIM.addin"
  RMDir /r "$APPDATA\Autodesk\Revit\Addins\2027\MetroCIM"

  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"
  RMDir "$APPDATA\MetroCIM"

  DeleteRegKey HKCU "${UNINSTALL_REGKEY}"
  DeleteRegKey HKCU "${PRODUCT_REGKEY}"
SectionEnd
