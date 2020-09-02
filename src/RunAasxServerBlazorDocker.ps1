#!/usr/bin/env pwsh

<#
.SYNOPSIS
This script starts a aasx-server-blazor docker container.
#>

$ErrorActionPreference = "Stop"

function Main()
{
    if ($null -eq (Get-Command "docker" -ErrorAction SilentlyContinue))
    {
        throw "Unable to find docker in your PATH"
    }

    docker run -d -p 51210:51210 -p 51310:51310 --name AasxServerBlazor aasx-server-blazor
}

Main
