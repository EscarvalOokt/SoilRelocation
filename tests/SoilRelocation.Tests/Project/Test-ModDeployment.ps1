param(
    [string] $Parent = [IO.Path]::GetTempPath(),
    [string] $Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$parentPath = (Resolve-Path -LiteralPath $Parent).Path
$fixture = [IO.Directory]::CreateDirectory((Join-Path $parentPath ('soil-deploy-test-' + [guid]::NewGuid().ToString('N'))))
$modRoot = Join-Path $fixture.FullName 'mod'
$project = Join-Path $PSScriptRoot '../../../src/SoilRelocation/SoilRelocation.csproj'
$publishedId = Join-Path $modRoot 'About/PublishedFileId.txt'
$localNotes = Join-Path $modRoot 'notes.txt'

function Deploy([bool] $expectSuccess, [string] $expectedMessage) {
    $arguments = @('msbuild', $project, '-nologo', '-t:DeployModToGame', '-p:DeployToGame=true',
        "-p:Configuration=$Configuration", "-p:RimWorldModAssembliesDir=$(Join-Path $modRoot 'Current/Assemblies')")
    $result = & dotnet @arguments
    $output = $result -join [Environment]::NewLine
    if (($LASTEXITCODE -eq 0) -ne $expectSuccess) { throw $output }
    if ($expectedMessage -and !$output.Contains($expectedMessage)) { throw "Missing '$expectedMessage' in: $output" }
}

function Assert-FileText([string] $path, [string] $value) {
    if (![IO.File]::Exists($path) -or [IO.File]::ReadAllText($path) -cne $value) { throw "Unexpected contents of $path" }
}

try {
    $foreign = [IO.Directory]::CreateDirectory((Join-Path $modRoot 'Resources'))
    $foreignFile = Join-Path $foreign.FullName 'keep.txt'
    [IO.File]::WriteAllText($foreignFile, 'foreign data')
    Deploy $false
    Assert-FileText $foreignFile 'foreign data'
    [IO.File]::Delete($foreignFile)
    [IO.Directory]::Delete($foreign.FullName)

    Deploy $true
    $currentDll = Join-Path $modRoot 'Current/Assemblies/SoilRelocation.dll'
    $historicalDll = Join-Path $modRoot '1.6/Assemblies/SoilRelocation.dll'
    if (!(Test-Path -LiteralPath $currentDll) -or !(Test-Path -LiteralPath $historicalDll)) { throw 'Missing assembly.' }
    [IO.File]::WriteAllText($publishedId, 'local workshop ID')
    [IO.File]::WriteAllText($localNotes, 'local notes')
    $obsolete = Join-Path $modRoot 'Defs/obsolete.xml'
    [IO.File]::WriteAllText($obsolete, 'stale')
    Deploy $true
    if (Test-Path -LiteralPath $obsolete) { throw 'A stale managed file survived deployment.' }
    Assert-FileText $publishedId 'local workshop ID'
    Assert-FileText $localNotes 'local notes'

    # Windows denies the staging copy while the ID is open exclusively. The installed package must survive.
    if ([IO.Path]::DirectorySeparatorChar -eq '\') {
        $exclusive = [IO.File]::Open($publishedId, 'Open', 'ReadWrite', 'None')
        try { Deploy $false } finally { $exclusive.Dispose() }
        Assert-FileText $publishedId 'local workshop ID'
        Assert-FileText $localNotes 'local notes'
        if (!(Test-Path -LiteralPath $currentDll)) { throw 'Deployment failure removed the installed assembly.' }
    }

    # Model termination after the live root was renamed and one unmanaged file transferred.
    $journal = [IO.Directory]::CreateDirectory((Join-Path $fixture.FullName '.mod.soilrelocation-deploy'))
    $nextPackage = [IO.Directory]::CreateDirectory((Join-Path $journal.FullName 'next'))
    $previousPackage = Join-Path $journal.FullName 'previous'
    [IO.File]::WriteAllText((Join-Path $journal.FullName 'owner'), 'escarval.soilrelocation')
    [IO.File]::WriteAllText((Join-Path $journal.FullName 'unmanaged'),
        [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes('notes.txt')))
    [IO.Directory]::Move($modRoot, $previousPackage)
    [IO.File]::Move((Join-Path $previousPackage 'notes.txt'), (Join-Path $nextPackage.FullName 'notes.txt'))
    Deploy $true
    Assert-FileText $publishedId 'local workshop ID'
    Assert-FileText $localNotes 'local notes'
    if (Test-Path -LiteralPath $journal.FullName) { throw 'Unrecovered deployment journal.' }
    if (!(Test-Path -LiteralPath $currentDll)) { throw 'Missing current assembly after recovery.' }

    if ([IO.Path]::DirectorySeparatorChar -eq '\') {
        # Publication finished: the live package is present, the old package is in previous, and next is gone.
        $null = [IO.Directory]::CreateDirectory($journal.FullName)
        $null = [IO.Directory]::CreateDirectory($previousPackage)
        Get-ChildItem -LiteralPath $modRoot | Where-Object Name -ne 'notes.txt' | ForEach-Object {
            Copy-Item -LiteralPath $_.FullName -Destination $previousPackage -Recurse
        }
        [IO.File]::WriteAllText((Join-Path $journal.FullName 'owner'), 'escarval.soilrelocation')
        [IO.File]::WriteAllText((Join-Path $journal.FullName 'unmanaged'),
            [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes('notes.txt')))
        $payload = Join-Path $previousPackage 'LoadFolders.xml'
        [IO.File]::WriteAllText($obsolete, 'before update')
        $expectedDllHash = (Get-FileHash -LiteralPath $currentDll).Hash

        # Windows keeps a process's current directory open against rename and deletion.
        $originalDirectory = [Environment]::CurrentDirectory
        try {
            [Environment]::CurrentDirectory = $journal.FullName
            $exclusive = [IO.File]::Open($payload, 'Open', 'Read', 'None')
            try { Deploy $false } finally { $exclusive.Dispose() }
            if (Test-Path -LiteralPath $nextPackage.FullName) { throw 'Staging started while the old package was locked.' }
            Assert-FileText $obsolete 'before update'
            Assert-FileText $publishedId 'local workshop ID'
            Assert-FileText $localNotes 'local notes'

            Deploy $true 'Deployment cleanup deferred'
            if (!(Test-Path -LiteralPath $journal.FullName)) { throw 'The locked journal disappeared.' }
            if (Test-Path -LiteralPath $obsolete) { throw 'Update was skipped while the journal directory was locked.' }
            if ((Get-FileHash -LiteralPath $currentDll).Hash -ne $expectedDllHash) { throw 'Incorrect published DLL.' }
            Assert-FileText $publishedId 'local workshop ID'
            Assert-FileText $localNotes 'local notes'
        }
        finally {
            [Environment]::CurrentDirectory = $originalDirectory
        }

        Deploy $true
        if (Test-Path -LiteralPath $journal.FullName) { throw 'Journal remained after the directory lock was released.' }
        Assert-FileText $publishedId 'local workshop ID'
        Assert-FileText $localNotes 'local notes'
    }
    Write-Output 'Deployment regression checks passed.'
}
finally {
    $actual = (Resolve-Path -LiteralPath $fixture.FullName).Path
    if ([IO.Path]::GetDirectoryName($actual) -ne $parentPath.TrimEnd([IO.Path]::DirectorySeparatorChar) -or
        [IO.Path]::GetFileName($actual) -notmatch '^soil-deploy-test-[a-f0-9]{32}$') { throw 'Unexpected fixture path.' }
    Remove-Item -LiteralPath $actual -Recurse -Force
}
