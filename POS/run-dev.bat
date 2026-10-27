@echo off
rem ================================================================
rem  NA Beauty POS: compila y abre la app. Todo queda en build.log
rem  (doble clic; si falla, envia build.log para revisarlo)
rem ================================================================
setlocal
cd /d "%~dp0"
set "LOG=%~dp0build.log"
> "%LOG%" echo === NA Beauty POS %DATE% %TIME% ===
where dotnet >nul 2>&1
if errorlevel 1 (
  echo NO SE ENCONTRO dotnet >> "%LOG%"
  echo No se encontro el SDK de .NET 10. Instalalo desde https://dotnet.microsoft.com/download
  pause
  exit /b 1
)
dotnet --list-sdks >> "%LOG%" 2>&1
echo === PRUEBAS (reglas de negocio) === >> "%LOG%"
dotnet run --project "tests\NABeauty.Tests\NABeauty.Tests.csproj" >> "%LOG%" 2>&1
echo === BUILD === >> "%LOG%"
dotnet build "src\NABeauty.App\NABeauty.App.csproj" -c Debug >> "%LOG%" 2>&1
if errorlevel 1 (
  echo === BUILD FALLO === >> "%LOG%"
  echo La compilacion fallo. Revisa build.log
  pause
  exit /b 1
)
echo === RUN === >> "%LOG%"
start "" "src\NABeauty.App\bin\Debug\net10.0-windows\NABeautyPOS.exe"
echo El POS se esta abriendo...
timeout /t 3 >nul
