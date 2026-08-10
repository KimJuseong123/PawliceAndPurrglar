<#
.SYNOPSIS
Uploads the WebGL release build and the built voice server to the EC2 instance.

.DESCRIPTION
Run from the repository root on the development machine:

    .\deploy\upload.ps1 -HostName ec2-user@pawlice.duckdns.org -KeyPath $HOME\Downloads\PawliceAndPurrglar-key.pem

Uploads to a staging folder and moves it into place in one step, so a half
finished upload is never the thing a judge loads. The old build is kept as
web.previous until the next upload, which is the whole rollback.

Uses scp, which ships with Windows 10 and later. No extra tooling.
#>
[CmdletBinding()]
param(
    # user@host of the instance. This one is Amazon Linux, so the login is
    # `ec2-user`. `ubuntu` — which every AWS tutorial and this file's own
    # example used to say — is refused with `Permission denied (publickey)`,
    # which reads as a wrong key rather than a wrong user.
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

# Line endings are converted, and this is not a tidiness measure.
#
# A PowerShell here-string ends every line with CRLF. Sent to a POSIX shell, the
# CR is not a line ending — it is the last character of the last word on the
# line. `sudo mv ~/staging-web /srv/pawlice/web` therefore created a directory
# literally named `web<CR>`, nginx kept serving the real `web` beside it, and the
# deploy reported nothing worse than a failed `chmod` on the very last line.
#
# The result was a site that stayed on an old build while every upload said it
# had succeeded, plus a `web<CR>` that `ls` prints as `web` — so the directory
# listing showed the same name twice and looked like a filesystem fault.
#
# `bash -s` with the script on stdin, so quoting on the remote side is one
# question instead of two.
function Invoke-Remote([string] $Command) {
    $unix = $Command -replace "`r`n", "`n"
    $unix | & ssh -i $KeyPath -o StrictHostKeyChecking=accept-new $HostName "bash -s"
    if ($LASTEXITCODE -ne 0) { throw "Remote command failed: $Command" }
}

# Named explicitly rather than with `*`, which PowerShell does not expand for a
# native command and Windows scp does not always expand either.
function Send-Folder([string] $LocalPath, [string] $RemotePath) {
    $items = Get-ChildItem -LiteralPath $LocalPath |
        Where-Object { $_.Name -notlike "*_BurstDebugInformation_DoNotShip" } |
        ForEach-Object { $_.FullName }
    if (-not $items) { throw "Nothing to upload in $LocalPath" }
    & scp -i $KeyPath -o StrictHostKeyChecking=accept-new -r -q @items "${HostName}:$RemotePath"
    if ($LASTEXITCODE -ne 0) { throw "Upload failed: $LocalPath -> $RemotePath" }
}

Write-Host "== game build"
Invoke-Remote "rm -rf ~/staging-web && mkdir -p ~/staging-web"
Send-Folder $webBuild "~/staging-web"
Invoke-Remote @"
set -e
sudo rm -rf /srv/pawlice/web.previous
sudo mv /srv/pawlice/web /srv/pawlice/web.previous 2>/dev/null || true
sudo mv ~/staging-web /srv/pawlice/web
sudo chown -R pawlice:pawlice /srv/pawlice/web
sudo chmod -R a+rX /srv/pawlice/web
test -f /srv/pawlice/web/index.html
test -d /srv/pawlice/web/Build
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
Write-Host "  ssh -i $KeyPath $HostName 'systemctl status nginx pawlice-voice --no-pager'"
