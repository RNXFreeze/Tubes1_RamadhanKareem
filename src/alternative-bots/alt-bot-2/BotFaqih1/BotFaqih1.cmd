@echo off
REM TemplateBot.cmd - Run the bot in development or release mode
REM Set MODE=dev for development (default, always rebuilds)
REM Set MODE=release for release (only runs if bin exists)

set MODE=dev

if "%MODE%"=="dev" (
    REM Development mode: always clean, build, and run
    rmdir /s /q bin obj  2>&1
    dotnet build 
    dotnet run --no-build 
) else if "%MODE%"=="release" (
    REM Release mode: no rebuild if bin exists
    if exist bin\ (
        dotnet run --no-build >nul
    ) else (
        dotnet build >nul
        dotnet run --no-build >nul
    )
) else (
    echo Error: Invalid MODE value. Use "dev" or "release".
)
