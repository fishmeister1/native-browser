@echo off
echo Building Native Browser Application...
dotnet restore
dotnet build
if %errorlevel% equ 0 (
    echo.
    echo Build completed successfully!
    echo To run the application, execute: dotnet run
    echo.
) else (
    echo.
    echo Build failed! Please check the error messages above.
    echo.
    pause
)