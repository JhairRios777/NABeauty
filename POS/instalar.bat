@echo off
rem ================================================================
rem  NA Beauty POS: instala (o actualiza) la app en esta PC.
rem   1. Corre las pruebas de las reglas de negocio (si fallan, no instala).
rem   2. Publica NABeautyPOS.exe autocontenido (no necesita .NET para usarse).
rem   3. Lo copia a %LOCALAPPDATA%\Programs\NABeautyPOS (fuera de OneDrive).
rem   4. Crea accesos directos en el Escritorio y en el menu Inicio.
rem  Tus datos NO se tocan: viven aparte en %LOCALAPPDATA%\NABeautyPOS.
rem  Todo queda registrado en instalar.log
rem ================================================================
setlocal
cd /d "%~dp0"
set "LOG=%~dp0instalar.log"
set "DESTINO=%LOCALAPPDATA%\Programs\NABeautyPOS"
set "TEMPORAL=%TEMP%\NABeautyPOSPublicar"
> "%LOG%" echo === Instalacion NA Beauty POS %DATE% %TIME% ===

where dotnet >nul 2>&1
if errorlevel 1 (
  echo No se encontro el SDK de .NET 10. Instalalo desde https://dotnet.microsoft.com/download
  echo NO SE ENCONTRO dotnet >> "%LOG%"
  pause & exit /b 1
)

rem --- La app no puede estar abierta: Windows bloquea el .exe en uso ---
tasklist /FI "IMAGENAME eq NABeautyPOS.exe" 2>nul | find /I "NABeautyPOS.exe" >nul
if not errorlevel 1 (
  echo El POS esta abierto. Cierralo y vuelve a ejecutar instalar.bat
  echo APP ABIERTA >> "%LOG%"
  pause & exit /b 1
)

echo [1/4] Ejecutando pruebas...
echo === PRUEBAS === >> "%LOG%"
dotnet run --project "tests\NABeauty.Tests\NABeauty.Tests.csproj" -c Release >> "%LOG%" 2>&1
if errorlevel 1 (
  echo Las pruebas fallaron: no se instalo nada. Revisa instalar.log
  pause & exit /b 1
)

echo [2/4] Publicando NABeautyPOS.exe ^(puede tardar un par de minutos^)...
echo === PUBLICAR === >> "%LOG%"
if exist "%TEMPORAL%" rmdir /s /q "%TEMPORAL%"
dotnet publish "src\NABeauty.App\NABeauty.App.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "%TEMPORAL%" >> "%LOG%" 2>&1
if errorlevel 1 (
  echo La publicacion fallo. Revisa instalar.log
  pause & exit /b 1
)

echo [3/4] Copiando a %DESTINO%...
echo === COPIAR === >> "%LOG%"
robocopy "%TEMPORAL%" "%DESTINO%" /MIR /NFL /NDL /NJH /NJS /NP >> "%LOG%" 2>&1
rem robocopy: codigos 0-7 son exito
if errorlevel 8 (
  echo No se pudo copiar la app. Revisa instalar.log
  pause & exit /b 1
)
rmdir /s /q "%TEMPORAL%" >nul 2>&1

echo [4/4] Creando accesos directos...
echo === ACCESOS === >> "%LOG%"
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$ws = New-Object -ComObject WScript.Shell;" ^
  "$exe = Join-Path $env:LOCALAPPDATA 'Programs\NABeautyPOS\NABeautyPOS.exe';" ^
  "$destinos = @([Environment]::GetFolderPath('Desktop'), [Environment]::GetFolderPath('Programs'));" ^
  "foreach ($d in $destinos) { $a = $ws.CreateShortcut((Join-Path $d 'NA Beauty POS.lnk'));" ^
  "  $a.TargetPath = $exe; $a.WorkingDirectory = (Split-Path $exe); $a.IconLocation = $exe + ',0';" ^
  "  $a.Description = 'NA Beauty - punto de venta'; $a.Save(); Write-Output ('Acceso: ' + $d) }" >> "%LOG%" 2>&1
if errorlevel 1 echo Aviso: no se pudieron crear los accesos directos ^(revisa instalar.log^). La app quedo en %DESTINO%

echo.
echo ================================================================
echo  Listo. Abre NA Beauty POS desde el Escritorio o el menu Inicio.
echo  Para actualizar en el futuro: vuelve a ejecutar instalar.bat
echo ================================================================
echo === OK === >> "%LOG%"
start "" "%DESTINO%\NABeautyPOS.exe"
pause
