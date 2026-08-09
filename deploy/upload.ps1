<#
.SYNOPSIS
Uploads the WebGL release build and the built voice server to the EC2 instance.

.DESCRIPTION
Run from the repository root on the development machine:

    .\deploy\upload.ps1 -Host ubuntu@1.2.3.4 -KeyPath $HOME\.ssh\pawlice.pem

Uploads to a staging folder and moves it into place in one step, so a half
finished upload is never the thing a judge loads. The old build is kept as
web.previous until the next upload, which is the whole rollback.

Uses scp, which ships with Windows 10 and later. No extra tooling.
#>
[CmdletBinding()]
param(
    # user@host of the instance, e.g. ubuntu@1.2.3.4
    [Parameter(Mandatory = $true)]
    [string] $HostName,

    # Path to the .pem key downloaded when the instance was created.
    [Parameter(Mandatory = $true)]
    [string] $KeyPath,

    # Skip the voice server and upload only the game.
    [switch] $GameOnly
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$webBuild = Join-Path $root "Builds\Release\WebGL"
$serverDir = Join-Path $root "server"

if (-not (Test-Path (Join-Path $webBuild "index.html"))) {
    throw "No release build at $webBuild. Run PawliceAndPurrglar > Build > Build WebGL Release first."
}

if (-not (Test-Path $KeyPath)) {
    throw "No key at $KeyPath"
}

function Invoke-Remote([string] $Command) {
    & ssh -i $KeyPath -o StrictHostKeyChecking=accept-new $HostName $Command
    if ($LASTEXITCODE -ne 0) { throw "Remote command failed: $Command" }
}

function Send-Folder([string] $LocalPath, [string] $RemotePath) {
    & scp -i $KeyPath -o StrictHostKeyChecking=accept-new -r -q "$LocalPath\*" "${HostName}:$RemotePath"
    if ($LASTEXITCODE -ne 0) { throw "Upload failed: $LocalPath -> $RemotePath" }
}

Write-Host "== game build"
Invoke-Remote "rm -rf ~/staging-web && mkdir -p ~/staging-web"
Send-Folder $webBuild "~/staging-web"
Invoke-Remote @"
sudo rm -rf /srv/pawlice/web.previous
sudo mv /srv/pawlice/web /srv/pawlice/web.previous 2>/dev/null || true
sudo mv ~/staging-web /srv/pawlice/web
sudo chown -R pawlice:pawlice /srv/pawlice/web
sudo chmod -R a+rX /srv/pawlice/web
"@

if (-not $GameOnly) {
    Write-Host "== voice server"

    # Built here rather than on the instance. A 1GB machine running tsc while
    # it is also serving the page is how a deploy turns into an outage.
    Push-Location $serverDir
    try {
        & npm ci
        if ($LASTEXITCODE -ne 0) { throw "npm ci failed" }
        & npm run build
        if ($LASTEXITCODE -ne 0) { throw "npm run build failed" }
    }
    finally {
        Pop-Location
    }

    Invoke-Remote "rm -rf ~/staging-server && mkdir -p ~/staging-server"
    Send-Folder (Join-Path $serverDir "dist") "~/staging-server"
    & scp -i $KeyPath -q (Join-Path $serverDir "package.json") "${HostName}:~/staging-server/package.json"
    & scp -i $KeyPath -q (Join-Path $serverDir "package-lock.json") "${HostName}:~/staging-server/package-lock.json"

    Invoke-Remote @"
sudo mkdir -p /srv/pawlice/server
sudo rm -rf /srv/pawlice/server/dist
sudo cp -r ~/staging-server/dist /srv/pawlice/server/dist
sudo cp ~/staging-server/package.json ~/staging-server/package-lock.json /srv/pawlice/server/
cd /srv/pawlice/server && sudo npm ci --omit=dev
sudo chown -R pawlice:pawlice /srv/pawlice/server
sudo systemctl restart pawlice-voice || true
"@
}

Write-Host ""
Write-Host "Uploaded. Check it:"
Write-Host "  ssh -i $KeyPath $HostName 'systemctl status caddy pawlice-voice --no-pager'"
