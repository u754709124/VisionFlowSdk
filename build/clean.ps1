$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot

Get-ChildItem $root -Directory -Recurse -Force |
    Where-Object { $_.Name -in @("bin", "obj", "TestResults", "artifacts") } |
    ForEach-Object {
        Write-Host "Removing $($_.FullName)" -ForegroundColor DarkGray
        Remove-Item $_.FullName -Recurse -Force
    }

Write-Host "Clean completed." -ForegroundColor Green
