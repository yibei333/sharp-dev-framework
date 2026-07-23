@echo off
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -o "bin\Release\net10.0\publish\"
if %errorlevel% neq 0 (
    echo Publish failed!
    pause
    exit /b %errorlevel%
)
echo Publish succeeded: bin\Release\net10.0\publish\
pause
