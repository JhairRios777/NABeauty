@echo off
rem ================================================================
rem  NA Beauty POS: genera el ejecutable final en la carpeta "Publicado".
rem  No necesita tener .NET instalado en la PC donde se use.
rem ================================================================
cd /d "%~dp0"
dotnet publish "src\NABeauty.App\NABeauty.App.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o Publicado
if errorlevel 1 ( echo Fallo la publicacion & pause & exit /b 1 )
echo.
echo Listo: Publicado\NABeautyPOS.exe
pause
