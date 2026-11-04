# Publishes the game to GitHub, wildhunchstudios/PondBrew, without Docs/Evidence (GR.83). Usage: Publish-PondBrew.ps1 [-DryRun]
# The evidence (the test runs' screenshots, logs and videos, about 33 GB with its history) stays in this repository only:
#   1. copy     a bare copy of this repository in Temp\publish-pondbrew.git, made afresh (its objects hard-linked, so it takes little room)
#   2. filter   every commit and tag in the copy rewritten without Docs/Evidence, nothing else changed (empty commits kept),
#               by a commit filter written to Temp\publish-filter.sh
#   3. check    no path under Docs/Evidence left, and as many commits as master here
#   4. push     master and the tags, never forced
# The rewrite comes out the same every time, so the commits already on GitHub keep their IDs and only new ones upload. A push that
# isn't a fast-forward, or a tag that moved, is refused rather than overwritten.
param([string]$Remote = 'https://github.com/wildhunchstudios/PondBrew.git', [switch]$DryRun)
$ErrorActionPreference = 'Stop'
$Root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (-not (Test-Path -LiteralPath (Join-Path $Root 'EvolutionShop\ProjectSettings\ProjectVersion.txt'))) { throw 'Not the Evolution Shop root.' }
$Copy = Join-Path $Root 'Temp\publish-pondbrew.git'
# The commit filter: each commit's tree with the Evidence entry taken out of Docs (and Docs out of the root if that empties it),
# written with git mktree from the two trees alone. An index filter (git rm on the index) crashed on the larger commits.
$FilterScript = Join-Path $Root 'Temp\publish-filter.sh'
$FilterText = @'
tree=$1; shift
tab=$(printf '\t')
empty=4b825dc642cb6eb9a060e54bf8d69288fbee4904
if docs=$(git rev-parse --verify -q "$tree:Docs") && git cat-file -e "$tree:Docs/Evidence" 2>/dev/null; then
  newdocs=$(git ls-tree -z "$docs" | grep -zv "${tab}Evidence\$" | git mktree -z) || exit 1
  tree=$(git ls-tree -z "$tree" | awk -v d="$newdocs" -v e="$empty" 'BEGIN { RS = "\0"; ORS = "\0"; FS = "\t" } $2 == "Docs" { if (d != e) { split($1, a, " "); print a[1] " tree " d "\tDocs" } next } { print }' | git mktree -z) || exit 1
fi
git commit-tree "$tree" "$@"
'@
# Native commands under Windows PowerShell 5.1: stderr with ErrorActionPreference Stop throws, so run them with Continue and check the exit code.
function Native([scriptblock]$cmd) {
    $old = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
    try { & $cmd; if ($LASTEXITCODE) { throw ('git exited ' + $LASTEXITCODE) } } finally { $ErrorActionPreference = $old }
}
$started = Get-Date

if (Test-Path -LiteralPath $Copy) { Remove-Item -LiteralPath $Copy -Recurse -Force }
Native { git clone --quiet --bare $Root $Copy }
Native { git -C $Copy config gc.auto 0 }
Native { git -C $Copy config maintenance.auto false }
Write-Host ('1. copy: {0:n0} s' -f ((Get-Date) - $started).TotalSeconds)

[IO.File]::WriteAllText($FilterScript, $FilterText.Replace("`r`n", "`n"))
$env:FILTER_BRANCH_SQUELCH_WARNING = '1'
Native { git -C $Copy filter-branch --commit-filter ("sh '" + $FilterScript.Replace('\', '/') + "' `$@") --tag-name-filter cat -- --all }
Write-Host ('2. filter: {0:n0} s' -f ((Get-Date) - $started).TotalSeconds)

$left = Native { git -C $Copy rev-list --objects refs/heads/master --tags } | Where-Object { $_ -match ' Docs/Evidence(/|$)' }
if ($left) { throw ('Docs/Evidence is still in the copy: ' + (($left | Select-Object -First 3) -join '; ')) }
$here = [int](Native { git -C $Root rev-list --count refs/heads/master })
$there = [int](Native { git -C $Copy rev-list --count refs/heads/master })
if ($here -ne $there) { throw ('The copy has ' + $there + ' commits, master here ' + $here) }
$tags = @(Native { git -C $Copy tag }).Count
Write-Host ('3. check: {0} commits and {1} tags, nothing under Docs/Evidence' -f $there, $tags)

$push = @('push', $Remote, 'refs/heads/master:refs/heads/master', 'refs/tags/*:refs/tags/*')
if ($DryRun) { $push = @('push', '--dry-run') + $push[1..3] }
Native { git -C $Copy @push }
$head = (Native { git -C $Copy rev-parse --short=8 refs/heads/master }).Trim()
Write-Host ('4. push: master {0} (here {1}){2}, {3:n0} s in all' -f $head, (Native { git -C $Root rev-parse --short=8 refs/heads/master }).Trim(), $(if ($DryRun) { ', dry run' } else { '' }), ((Get-Date) - $started).TotalSeconds)
