; MetroCIM for Navisworks — per-user NSIS installer (no administrator rights)
Unicode true
!include "MUI2.nsh"

; PRODUCT_VERSION / STAGING_DIR / OUT_FILE are passed by Build-NavisworksInstaller.ps1
!ifndef PRODUCT_VERSION
  !define PRODUCT_VERSION "1.0.0"
!endif
!ifndef STAGING_DIR
  !define STAGING_DIR "artifacts\staging\MetroCIM-Navisworks"
!endif
!ifndef OUT_FILE
  !define OUT_FILE "dist\MetroCIM-Navisworks-${PRODUCT_VERSION}.exe"
!endif

!define PRODUCT_NAME "MetroCIM for Navisworks"
!define PRODUCT_PUBLISHER "Metropolitan CIM"
!define PRODUCT_REGKEY "Software\MetroCIM\Navisworks"
!define UNINSTALL_REGKEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\MetroCIMNavisworks"
!define BUNDLE_DIR "$APPDATA\Autodesk\ApplicationPlugins\MetroCIM.Navisworks.bundle"

Name "${PRODUCT_NAME}"
OutFile "${OUT_FILE}"
InstallDir "$APPDATA\MetroCIM\Navisworks"
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

  SetOutPath "$INSTDIR"
  WriteUninstaller "$INSTDIR\Uninstall.exe"

  ; Replace any previous bundle
  RMDir /r "${BUNDLE_DIR}"
  SetOutPath "${BUNDLE_DIR}"
  File "${STAGING_DIR}\MetroCIM.Navisworks.bundle\PackageContents.xml"
  SetOutPath "${BUNDLE_DIR}\Contents"
  File /r "${STAGING_DIR}\MetroCIM.Navisworks.bundle\Contents\*.*"

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

  RMDir /r "${BUNDLE_DIR}"

  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"
  RMDir "$APPDATA\MetroCIM"

  DeleteRegKey HKCU "${UNINSTALL_REGKEY}"
  DeleteRegKey HKCU "${PRODUCT_REGKEY}"
SectionEnd
