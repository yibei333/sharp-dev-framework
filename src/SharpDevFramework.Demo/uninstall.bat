@echo off
title Uninstall Service
setlocal enabledelayedexpansion

:: ------------------- Request Admin Privileges -------------------
>nul 2>&1 "%SYSTEMROOT%\system32\cacls.exe" "%SYSTEMROOT%\system32\config\system"
if !errorlevel! neq 0 (
    echo Requesting admin privileges...
    echo Set UAC = CreateObject^("Shell.Application"^) > "%temp%\getadmin.vbs"
    echo UAC.ShellExecute "%~s0", "", "", "runas", 1 >> "%temp%\getadmin.vbs"
    "%temp%\getadmin.vbs"
    del "%temp%\getadmin.vbs"
    exit /b
)

:: Ensure working directory is script directory
pushd "%~dp0"

set SERVICE_NAME=SharpDevFramework.Demo

:: ------------------- Check if service exists -------------------
sc qc "%SERVICE_NAME%" >nul 2>&1
if !errorlevel! neq 0 (
    echo Service "%SERVICE_NAME%" does not exist.
    pause
    exit /b 0
)

:: ------------------- Check if service is running and stop it -------------------
sc query "%SERVICE_NAME%" | find "STATE" | find "RUNNING" >nul
if !errorlevel! equ 0 (
    echo Stopping service "%SERVICE_NAME%"...
    sc stop "%SERVICE_NAME%" >nul
    if !errorlevel! neq 0 (
        echo Failed to stop service!
        pause
        exit /b 1
    )
    timeout /t 3 /nobreak >nul
    echo Service stopped.
) else (
    echo Service is not running, skip stopping.
)

:: ------------------- Delete service -------------------
echo Deleting service "%SERVICE_NAME%"...
sc delete "%SERVICE_NAME%" >nul 2>&1
if !errorlevel! neq 0 (
    echo Failed to delete service!
    pause
    exit /b 1
)

echo Service "%SERVICE_NAME%" uninstalled successfully.
pause
exit /b 0