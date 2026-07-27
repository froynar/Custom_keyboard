[CmdletBinding()]
param(
    [string]$RepositoryRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
}

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Read-Text {
    param([string]$RelativePath)

    $path = Join-Path $RepositoryRoot $RelativePath
    Assert-True (Test-Path -LiteralPath $path -PathType Leaf) "Required file is missing: $RelativePath"
    return Get-Content -LiteralPath $path -Raw -Encoding UTF8
}

function Get-SqlTableBody {
    param(
        [string]$Sql,
        [string]$TableName
    )

    $escapedName = [regex]::Escape($TableName)
    $pattern = "(?ms)^CREATE\s+TABLE\s+(?:dbo\.)?\[?$escapedName\]?\s*\((?<body>.*?)(?=^\);\s*$)"
    $match = [regex]::Match($Sql, $pattern)
    Assert-True $match.Success "CreateSchema_Refactor.sql is missing CREATE TABLE $TableName."
    return $match.Groups['body'].Value
}

function Get-DbmlTableBody {
    param(
        [string]$Dbml,
        [string]$TableName
    )

    $escapedName = [regex]::Escape($TableName)
    $pattern = "(?ms)^Table\s+$escapedName\s*\{(?<body>.*?)^\}"
    $match = [regex]::Match($Dbml, $pattern)
    Assert-True $match.Success "ERD DBML is missing table $TableName."
    return $match.Groups['body'].Value
}

$qcSchemaVersion = '2026.07.27-qc-concise'
$buildDeviceSchemaVersion = '2026.07.27-build-device-hardening'
$completedQcSchemaVersion = '2026.07.27-completed-qc-reconciliation'
$readViewSchemaVersion = '2026.07.27-read-views'
$qcViewSchemaVersion = '2026.07.27-qc-view-hardening'
$expectedSchemaVersion = '2026.07.27-simplified-read-views'
$legacyPkSchemaVersion = '2026.07.15-pk-id'
$expectedPrimaryKeys = [ordered]@{
    roles = 'role_id'
    users = 'user_id'
    seller_profiles = 'seller_profile_id'
    seller_applications = 'application_id'
    brands = 'brand_id'
    layouts = 'layout_id'
    keyboard_kits = 'kit_id'
    switches = 'switch_id'
    keycap_sets = 'keycap_id'
    stabilizers = 'stab_id'
    accessories = 'accessory_id'
    builds = 'build_id'
    build_items = 'build_item_id'
    build_mods = 'mod_id'
    build_requests = 'request_id'
    devices = 'device_id'
    device_test_sessions = 'session_id'
    device_key_test_results = 'key_test_id'
    audit_log = 'log_id'
    chat_conversations = 'conversation_id'
    chat_messages = 'message_id'
}

$expectedPrimaryKeyShapes = [ordered]@{
    roles = 'int-identity'
    users = 'int-identity'
    seller_profiles = 'int-identity'
    seller_applications = 'int-identity'
    brands = 'int-identity'
    layouts = 'varchar-50'
    keyboard_kits = 'varchar-50'
    switches = 'varchar-50'
    keycap_sets = 'varchar-50'
    stabilizers = 'varchar-50'
    accessories = 'varchar-50'
    builds = 'varchar-50'
    build_items = 'int-identity'
    build_mods = 'int-identity'
    build_requests = 'varchar-50'
    devices = 'varchar-50'
    device_test_sessions = 'varchar-50'
    device_key_test_results = 'bigint-identity'
    audit_log = 'int-identity'
    chat_conversations = 'varchar-50'
    chat_messages = 'varchar-50'
}

$expectedForeignKeys = @(
    'users.role_id->roles.id',
    'seller_profiles.user_id->users.id',
    'seller_applications.buyer_user_id->users.id',
    'seller_applications.reviewed_by->users.id',
    'keyboard_kits.brand_id->brands.id',
    'keyboard_kits.layout_id->layouts.id',
    'switches.brand_id->brands.id',
    'keycap_sets.brand_id->brands.id',
    'stabilizers.brand_id->brands.id',
    'builds.buyer_id->users.id',
    'builds.kit_id->keyboard_kits.id',
    'build_items.build_id->builds.id',
    'build_items.switch_id->switches.id',
    'build_items.keycap_id->keycap_sets.id',
    'build_items.stab_id->stabilizers.id',
    'build_items.accessory_id->accessories.id',
    'build_mods.build_id->builds.id',
    'build_requests.build_id->builds.id',
    'build_requests.seller_user_id->users.id',
    'devices.seller_user_id->users.id',
    'device_test_sessions.request_id->build_requests.id',
    'device_test_sessions.device_id->devices.id',
    'device_key_test_results.session_id->device_test_sessions.id',
    'audit_log.user_id->users.id',
    'chat_conversations.seller_user_id->users.id',
    'chat_conversations.buyer_id->users.id',
    'chat_conversations.admin_user_id->users.id',
    'chat_conversations.build_request_id->build_requests.id',
    'chat_messages.conversation_id->chat_conversations.id',
    'chat_messages.sender_user_id->users.id'
)

Assert-True ($expectedPrimaryKeys.Count -eq 21) 'Internal source verifier mapping must contain 21 PK entries.'
Assert-True ($expectedPrimaryKeyShapes.Count -eq 21) 'Internal source verifier shape mapping must contain 21 PK entries.'
Assert-True ($expectedForeignKeys.Count -eq 30) 'Internal source verifier mapping must contain 30 FK entries.'

$schema = Read-Text 'Database\SqlServer\CreateSchema_Refactor.sql'
$dbml = Read-Text 'Documents\Custom_Keyboard_ERD_Final.dbml'
$migration = Read-Text 'Database\SqlServer\MigratePkToId_20260715.sql'
$preflight = Read-Text 'Database\SqlServer\VerifyPkToId_Preflight.sql'
$postflight = Read-Text 'Database\SqlServer\VerifyPkToId_Postflight.sql'
$qcMigration = Read-Text 'Database\SqlServer\MigrateQcConcise_20260727.sql'
$qcPreflight = Read-Text 'Database\SqlServer\VerifyQcConcise_Preflight.sql'
$qcPostflight = Read-Text 'Database\SqlServer\VerifyQcConcise_Postflight.sql'
$buildDeviceMigration = Read-Text 'Database\SqlServer\MigrateBuildDeviceHardening_20260727.sql'
$buildDevicePreflight = Read-Text 'Database\SqlServer\VerifyBuildDeviceHardening_Preflight.sql'
$buildDevicePostflight = Read-Text 'Database\SqlServer\VerifyBuildDeviceHardening_Postflight.sql'
$completedQcMigration = Read-Text 'Database\SqlServer\MigrateCompletedQcReconciliation_20260727.sql'
$completedQcPreflight = Read-Text 'Database\SqlServer\VerifyCompletedQcReconciliation_Preflight.sql'
$completedQcPostflight = Read-Text 'Database\SqlServer\VerifyCompletedQcReconciliation_Postflight.sql'
$readViews = Read-Text 'Database\SqlServer\ReadViews.sql'
$baselineReadViews = Read-Text 'Database\SqlServer\ReadViews_20260727.sql'
$finalReadViews = Read-Text 'Database\SqlServer\ReadViews_QcViewHardening_20260727.sql'
$simplifiedReadViews = Read-Text 'Database\SqlServer\ReadViews_Simplified_20260727.sql'
$readViewsMigration = Read-Text 'Database\SqlServer\MigrateReadViews_20260727.sql'
$readViewsPreflight = Read-Text 'Database\SqlServer\VerifyReadViews_Preflight.sql'
$readViewsPostflight = Read-Text 'Database\SqlServer\VerifyReadViews_Postflight.sql'
$qcViewMigration = Read-Text 'Database\SqlServer\MigrateQcViewHardening_20260727.sql'
$qcViewPreflight = Read-Text 'Database\SqlServer\VerifyQcViewHardening_Preflight.sql'
$qcViewPostflight = Read-Text 'Database\SqlServer\VerifyQcViewHardening_Postflight.sql'
$simplifiedViewMigration = Read-Text 'Database\SqlServer\MigrateSimplifiedReadViews_20260727.sql'
$simplifiedViewPreflight = Read-Text 'Database\SqlServer\VerifySimplifiedReadViews_Preflight.sql'
$simplifiedViewPostflight = Read-Text 'Database\SqlServer\VerifySimplifiedReadViews_Postflight.sql'
$cleanVerifier = Read-Text 'Database\SqlServer\VerifyRefactor.sql'
$sellerApplicationsApply = Read-Text 'Database\SqlServer\ApplySellerApplications.sql'
$mainSeed = Read-Text 'Documents_Refactor\SeedData_Refactor.sql'
$mainSeedBody = Read-Text 'Documents_Refactor\SeedData_Refactor.Body.sql'
$analyticsSeed = Read-Text 'Database\SqlServer\SeedDemoAnalytics_Refactor.sql'
$analyticsSeedBody = Read-Text 'Database\SqlServer\SeedDemoAnalytics_Refactor.Body.sql'
$healthCheck = Read-Text 'Data\SqlServer\SqlServerHealthCheck.cs'
$mainWindow = Read-Text 'MainWindow.xaml.cs'

$actualSchemaForeignKeys = @()
$actualDbmlForeignKeys = @()

foreach ($entry in $expectedPrimaryKeys.GetEnumerator()) {
    $tableName = [string]$entry.Key
    $legacyName = [string]$entry.Value
    $sqlBody = Get-SqlTableBody $schema $tableName
    $dbmlBody = Get-DbmlTableBody $dbml $tableName

    Assert-True ([regex]::IsMatch($sqlBody, '(?mi)^\s*id\s+[^\r\n]*\bPRIMARY\s+KEY\b')) `
        "SQL schema table $tableName does not define id as its primary key."
    Assert-True (-not [regex]::IsMatch($sqlBody, "(?mi)^\s*$([regex]::Escape($legacyName))\s+[^\r\n]*\bPRIMARY\s+KEY\b")) `
        "SQL schema table $tableName still defines legacy PK $legacyName."
    Assert-True ([regex]::IsMatch($dbmlBody, '(?mi)^\s*id\s+\S+\s+\[[^\]]*\bpk\b[^\]]*\]')) `
        "DBML table $tableName does not define id as its primary key."

    $shape = $expectedPrimaryKeyShapes[$tableName]
    switch ($shape) {
        'int-identity' {
            $sqlShapePattern = '(?mi)^\s*id\s+INT\s+IDENTITY\s*\(\s*1\s*,\s*1\s*\)\s+PRIMARY\s+KEY\b'
            $dbmlShapePattern = '(?mi)^\s*id\s+int\s+\[[^\]]*\bpk\b[^\]]*\bincrement\b[^\]]*\]'
        }
        'bigint-identity' {
            $sqlShapePattern = '(?mi)^\s*id\s+BIGINT\s+IDENTITY\s*\(\s*1\s*,\s*1\s*\)\s+PRIMARY\s+KEY\b'
            $dbmlShapePattern = '(?mi)^\s*id\s+bigint\s+\[[^\]]*\bpk\b[^\]]*\bincrement\b[^\]]*\]'
        }
        'varchar-50' {
            $sqlShapePattern = '(?mi)^\s*id\s+VARCHAR\s*\(\s*50\s*\)\s+PRIMARY\s+KEY\b'
            $dbmlShapePattern = '(?mi)^\s*id\s+varchar\s*\(\s*50\s*\)\s+\[[^\]]*\bpk\b[^\]]*\]'
        }
        default {
            throw "Unknown PK shape '$shape' for table $tableName."
        }
    }

    Assert-True ([regex]::IsMatch($sqlBody, $sqlShapePattern)) `
        "SQL schema PK shape is wrong for table $tableName; expected $shape."
    Assert-True ([regex]::IsMatch($dbmlBody, $dbmlShapePattern)) `
        "DBML PK shape is wrong for table $tableName; expected $shape."

    $renamePattern = "(?i)EXEC\s+sys\.sp_rename\s+N'dbo\.$([regex]::Escape($tableName))\.$([regex]::Escape($legacyName))'\s*,\s*N'id'\s*,\s*N'COLUMN'"
    Assert-True ([regex]::IsMatch($migration, $renamePattern)) `
        "Migration is missing rename dbo.$tableName.$legacyName -> id."

    foreach ($match in [regex]::Matches($sqlBody, '(?im)FOREIGN\s+KEY\s*\(\s*(?<parent>[a-z_][a-z0-9_]*)\s*\)\s+REFERENCES\s+(?:dbo\.)?(?<referenced>[a-z_][a-z0-9_]*)\s*\(\s*(?<column>[a-z_][a-z0-9_]*)\s*\)')) {
        $actualSchemaForeignKeys += "$tableName.$($match.Groups['parent'].Value)->$($match.Groups['referenced'].Value).$($match.Groups['column'].Value)"
    }

    foreach ($match in [regex]::Matches($dbmlBody, '(?im)^\s*(?<parent>[a-z_][a-z0-9_]*)\s+\S+\s+\[[^\]]*\bref:\s*[>-]\s*(?<referenced>[a-z_][a-z0-9_]*)\.(?<column>[a-z_][a-z0-9_]*)[^\]]*\]')) {
        $actualDbmlForeignKeys += "$tableName.$($match.Groups['parent'].Value)->$($match.Groups['referenced'].Value).$($match.Groups['column'].Value)"
    }
}

$expectedQcColumns = [ordered]@{
    device_test_sessions = @(
        'id', 'request_id', 'device_id', 'switch_technology',
        'noise_requirement', 'total_keys', 'status', 'completed_at'
    )
    device_key_test_results = @(
        'id', 'session_id', 'key_code', 'received_key',
        'press_signal_detected', 'latency', 'press_count',
        'release_signal', 'hold_duration', 'noise', 'result', 'recorded_at'
    )
}

foreach ($qcEntry in $expectedQcColumns.GetEnumerator()) {
    $tableName = [string]$qcEntry.Key
    $expectedColumns = @($qcEntry.Value)
    $sqlBody = Get-SqlTableBody $schema $tableName
    $dbmlBody = Get-DbmlTableBody $dbml $tableName

    $sqlColumns = @(
        [regex]::Matches(
            $sqlBody,
            '(?im)^\s*(?<name>[a-z_][a-z0-9_]*)\s+(?:INT|BIGINT|VARCHAR|BIT|DECIMAL|DATETIME2)\b'
        ) | ForEach-Object { $_.Groups['name'].Value.ToLowerInvariant() }
    )
    $dbmlColumns = @(
        [regex]::Matches(
            $dbmlBody,
            '(?im)^\s*(?<name>[a-z_][a-z0-9_]*)\s+(?:int|bigint|varchar|boolean|decimal|datetime|NoiseRequirement|SwitchTechnology|TestSessionStatus|KeyTestResult)\b'
        ) | ForEach-Object { $_.Groups['name'].Value.ToLowerInvariant() }
    )

    foreach ($columnSet in @(
        [pscustomobject]@{ Name = "SQL $tableName"; Values = $sqlColumns },
        [pscustomobject]@{ Name = "DBML $tableName"; Values = $dbmlColumns }
    )) {
        $missing = @($expectedColumns | Where-Object { $_ -notin $columnSet.Values })
        $unexpected = @($columnSet.Values | Where-Object { $_ -notin $expectedColumns })
        Assert-True ($columnSet.Values.Count -eq $expectedColumns.Count) `
            "$($columnSet.Name) must contain exactly $($expectedColumns.Count) columns; found $($columnSet.Values.Count)."
        Assert-True ($missing.Count -eq 0) "$($columnSet.Name) is missing column(s): $($missing -join ', ')"
        Assert-True ($unexpected.Count -eq 0) "$($columnSet.Name) has unexpected column(s): $($unexpected -join ', ')"
    }
}

$renameCount = [regex]::Matches($migration, "(?i)EXEC\s+sys\.sp_rename\s+N'dbo\.[^']+'\s*,\s*N'id'\s*,\s*N'COLUMN'").Count
Assert-True ($renameCount -eq 21) "Migration must contain exactly 21 PK rename commands; found $renameCount."
Assert-True (-not [regex]::IsMatch($migration, '(?i)\bDROP\s+TABLE\b')) 'In-place migration must not drop business tables.'

foreach ($actualSet in @(
    [pscustomobject]@{ Name = 'SQL schema'; Values = $actualSchemaForeignKeys },
    [pscustomobject]@{ Name = 'DBML'; Values = $actualDbmlForeignKeys }
)) {
    $missing = @($expectedForeignKeys | Where-Object { $_ -notin $actualSet.Values })
    $unexpected = @($actualSet.Values | Where-Object { $_ -notin $expectedForeignKeys })
    Assert-True ($actualSet.Values.Count -eq 30) "$($actualSet.Name) must contain exactly 30 FK relationships; found $($actualSet.Values.Count)."
    Assert-True ($missing.Count -eq 0) "$($actualSet.Name) is missing FK relationship(s): $($missing -join ', ')"
    Assert-True ($unexpected.Count -eq 0) "$($actualSet.Name) has unexpected FK relationship(s): $($unexpected -join ', ')"
}

foreach ($versionedText in @(
    [pscustomobject]@{ Name = 'CreateSchema_Refactor.sql'; Text = $schema },
    [pscustomobject]@{ Name = 'MigrateSimplifiedReadViews_20260727.sql'; Text = $simplifiedViewMigration },
    [pscustomobject]@{ Name = 'VerifySimplifiedReadViews_Postflight.sql'; Text = $simplifiedViewPostflight },
    [pscustomobject]@{ Name = 'VerifyRefactor.sql'; Text = $cleanVerifier },
    [pscustomobject]@{ Name = 'SeedData_Refactor.sql'; Text = $mainSeed },
    [pscustomobject]@{ Name = 'SeedDemoAnalytics_Refactor.sql'; Text = $analyticsSeed },
    [pscustomobject]@{ Name = 'SqlServerHealthCheck.cs'; Text = $healthCheck }
)) {
    Assert-True ($versionedText.Text.Contains($expectedSchemaVersion)) `
        "$($versionedText.Name) does not contain schema version $expectedSchemaVersion."
}

foreach ($qcViewVersionedText in @(
    [pscustomobject]@{ Name = 'CreateSchema_Refactor.sql'; Text = $schema },
    [pscustomobject]@{ Name = 'MigrateQcViewHardening_20260727.sql'; Text = $qcViewMigration },
    [pscustomobject]@{ Name = 'VerifyQcViewHardening_Postflight.sql'; Text = $qcViewPostflight },
    [pscustomobject]@{ Name = 'VerifySimplifiedReadViews_Preflight.sql'; Text = $simplifiedViewPreflight },
    [pscustomobject]@{ Name = 'MigrateSimplifiedReadViews_20260727.sql'; Text = $simplifiedViewMigration }
)) {
    Assert-True ($qcViewVersionedText.Text.Contains($qcViewSchemaVersion)) `
        "$($qcViewVersionedText.Name) does not contain prerequisite schema version $qcViewSchemaVersion."
}

foreach ($readViewVersionedText in @(
    [pscustomobject]@{ Name = 'CreateSchema_Refactor.sql'; Text = $schema },
    [pscustomobject]@{ Name = 'ReadViews_20260727.sql'; Text = $baselineReadViews },
    [pscustomobject]@{ Name = 'MigrateReadViews_20260727.sql'; Text = $readViewsMigration },
    [pscustomobject]@{ Name = 'VerifyReadViews_Postflight.sql'; Text = $readViewsPostflight },
    [pscustomobject]@{ Name = 'VerifyQcViewHardening_Preflight.sql'; Text = $qcViewPreflight },
    [pscustomobject]@{ Name = 'MigrateQcViewHardening_20260727.sql'; Text = $qcViewMigration }
)) {
    Assert-True ($readViewVersionedText.Text.Contains($readViewSchemaVersion)) `
        "$($readViewVersionedText.Name) does not contain prerequisite schema version $readViewSchemaVersion."
}

foreach ($completedQcVersionedText in @(
    [pscustomobject]@{ Name = 'CreateSchema_Refactor.sql'; Text = $schema },
    [pscustomobject]@{ Name = 'MigrateCompletedQcReconciliation_20260727.sql'; Text = $completedQcMigration },
    [pscustomobject]@{ Name = 'VerifyCompletedQcReconciliation_Postflight.sql'; Text = $completedQcPostflight },
    [pscustomobject]@{ Name = 'VerifyReadViews_Preflight.sql'; Text = $readViewsPreflight },
    [pscustomobject]@{ Name = 'MigrateReadViews_20260727.sql'; Text = $readViewsMigration }
)) {
    Assert-True ($completedQcVersionedText.Text.Contains($completedQcSchemaVersion)) `
        "$($completedQcVersionedText.Name) does not contain prerequisite schema version $completedQcSchemaVersion."
}

foreach ($qcVersionedText in @(
    [pscustomobject]@{ Name = 'CreateSchema_Refactor.sql'; Text = $schema },
    [pscustomobject]@{ Name = 'MigrateQcConcise_20260727.sql'; Text = $qcMigration },
    [pscustomobject]@{ Name = 'VerifyQcConcise_Postflight.sql'; Text = $qcPostflight },
    [pscustomobject]@{ Name = 'VerifyBuildDeviceHardening_Preflight.sql'; Text = $buildDevicePreflight }
)) {
    Assert-True ($qcVersionedText.Text.Contains($qcSchemaVersion)) `
        "$($qcVersionedText.Name) does not contain schema version $qcSchemaVersion."
}

foreach ($buildDeviceVersionedText in @(
    [pscustomobject]@{ Name = 'CreateSchema_Refactor.sql'; Text = $schema },
    [pscustomobject]@{ Name = 'MigrateBuildDeviceHardening_20260727.sql'; Text = $buildDeviceMigration },
    [pscustomobject]@{ Name = 'VerifyBuildDeviceHardening_Postflight.sql'; Text = $buildDevicePostflight },
    [pscustomobject]@{ Name = 'VerifyCompletedQcReconciliation_Preflight.sql'; Text = $completedQcPreflight }
)) {
    Assert-True ($buildDeviceVersionedText.Text.Contains($buildDeviceSchemaVersion)) `
        "$($buildDeviceVersionedText.Name) does not contain schema version $buildDeviceSchemaVersion."
}

foreach ($legacyVersionedText in @(
    [pscustomobject]@{ Name = 'MigratePkToId_20260715.sql'; Text = $migration },
    [pscustomobject]@{ Name = 'VerifyPkToId_Preflight.sql'; Text = $preflight },
    [pscustomobject]@{ Name = 'VerifyPkToId_Postflight.sql'; Text = $postflight },
    [pscustomobject]@{ Name = 'ApplySellerApplications.sql'; Text = $sellerApplicationsApply },
    [pscustomobject]@{ Name = 'VerifyQcConcise_Preflight.sql'; Text = $qcPreflight }
)) {
    Assert-True ($legacyVersionedText.Text.Contains($legacyPkSchemaVersion)) `
        "$($legacyVersionedText.Name) does not contain prerequisite schema version $legacyPkSchemaVersion."
}

Assert-True ($healthCheck.Contains('ExpectedPrimaryKeyCount = 21')) 'Startup schema guard does not require 21 PKs.'
Assert-True ($healthCheck.Contains('ExpectedForeignKeyCount = 30')) 'Startup schema guard does not require 30 FKs.'
Assert-True ($healthCheck.Contains('ExpectedQcColumnCount = 20')) 'Startup schema guard does not require the final 8/12 QC column count.'
Assert-True ($mainWindow.Contains('EnsureCompatibleSchemaAsync')) 'Application startup does not invoke the schema compatibility guard.'
Assert-True (-not [regex]::IsMatch($migration, '(?mi)^\s*USE\s+')) 'Migration must require an explicitly selected target database.'
Assert-True (-not [regex]::IsMatch($preflight, '(?mi)^\s*USE\s+')) 'Preflight must require an explicitly selected target database.'
Assert-True (-not [regex]::IsMatch($postflight, '(?mi)^\s*USE\s+')) 'Postflight must require an explicitly selected target database.'
Assert-True (-not [regex]::IsMatch($qcMigration, '(?mi)^\s*USE\s+')) 'QC migration must require an explicitly selected target database.'
Assert-True (-not [regex]::IsMatch($qcPreflight, '(?mi)^\s*USE\s+')) 'QC preflight must require an explicitly selected target database.'
Assert-True (-not [regex]::IsMatch($qcPostflight, '(?mi)^\s*USE\s+')) 'QC postflight must require an explicitly selected target database.'
Assert-True (-not [regex]::IsMatch($buildDeviceMigration, '(?mi)^\s*USE\s+')) 'Build/device migration must require an explicitly selected target database.'
Assert-True (-not [regex]::IsMatch($buildDevicePreflight, '(?mi)^\s*USE\s+')) 'Build/device preflight must require an explicitly selected target database.'
Assert-True (-not [regex]::IsMatch($buildDevicePostflight, '(?mi)^\s*USE\s+')) 'Build/device postflight must require an explicitly selected target database.'
Assert-True (-not [regex]::IsMatch($completedQcMigration, '(?mi)^\s*USE\s+')) 'Completed/QC migration must require an explicitly selected target database.'
Assert-True (-not [regex]::IsMatch($completedQcPreflight, '(?mi)^\s*USE\s+')) 'Completed/QC preflight must require an explicitly selected target database.'
Assert-True (-not [regex]::IsMatch($completedQcPostflight, '(?mi)^\s*USE\s+')) 'Completed/QC postflight must require an explicitly selected target database.'
Assert-True (-not [regex]::IsMatch($readViewsMigration, '(?mi)^\s*USE\s+')) 'Read-view migration must require an explicitly selected target database.'
Assert-True (-not [regex]::IsMatch($readViewsPreflight, '(?mi)^\s*USE\s+')) 'Read-view preflight must require an explicitly selected target database.'
Assert-True (-not [regex]::IsMatch($readViewsPostflight, '(?mi)^\s*USE\s+')) 'Read-view postflight must require an explicitly selected target database.'
Assert-True (-not [regex]::IsMatch($qcViewMigration, '(?mi)^\s*USE\s+')) 'QC/view migration must require an explicitly selected target database.'
Assert-True (-not [regex]::IsMatch($qcViewPreflight, '(?mi)^\s*USE\s+')) 'QC/view preflight must require an explicitly selected target database.'
Assert-True (-not [regex]::IsMatch($qcViewPostflight, '(?mi)^\s*USE\s+')) 'QC/view postflight must require an explicitly selected target database.'
Assert-True (-not [regex]::IsMatch($simplifiedViewMigration, '(?mi)^\s*USE\s+')) 'Simplified-view migration must require an explicitly selected target database.'
Assert-True (-not [regex]::IsMatch($simplifiedViewPreflight, '(?mi)^\s*USE\s+')) 'Simplified-view preflight must require an explicitly selected target database.'
Assert-True (-not [regex]::IsMatch($simplifiedViewPostflight, '(?mi)^\s*USE\s+')) 'Simplified-view postflight must require an explicitly selected target database.'
Assert-True ($migration.Contains('(version, description, applied_at, succeeded)')) `
    'Migration history insert must set applied_at explicitly.'
Assert-True ($migration.Contains('SellerProfileAuditResolution')) `
    'Migration does not normalize legacy seller_profiles audit record IDs.'
Assert-True ($postflight.Contains('seller_profiles audit record_id must use the integer profile PK')) `
    'Postflight does not enforce the seller_profiles audit record-id contract.'
Assert-True ($qcPostflight.Contains('device_test_sessions does not match the exact 8-column concise contract')) `
    'QC postflight does not enforce the concise session column contract.'
Assert-True ($qcPostflight.Contains('device_key_test_results does not match the exact 12-column concise contract')) `
    'QC postflight does not enforce the concise key-result column contract.'

foreach ($guardedText in @(
    [pscustomobject]@{ Name = 'MigratePkToId_20260715.sql'; Text = $migration },
    [pscustomobject]@{ Name = 'VerifyPkToId_Preflight.sql'; Text = $preflight },
    [pscustomobject]@{ Name = 'VerifyPkToId_Postflight.sql'; Text = $postflight },
    [pscustomobject]@{ Name = 'VerifyRefactor.sql'; Text = $cleanVerifier },
    [pscustomobject]@{ Name = 'ApplySellerApplications.sql'; Text = $sellerApplicationsApply }
)) {
    Assert-True ($guardedText.Text.Contains("column_info.name = N'description'")) `
        "$($guardedText.Name) does not validate the full schema_migrations column contract."
    Assert-True ($guardedText.Text.Contains('column_info.scale = 7')) `
        "$($guardedText.Name) does not validate schema_migrations.applied_at precision."
    Assert-True ($guardedText.Text.Contains('pk.is_primary_key = 1')) `
        "$($guardedText.Name) does not validate the schema_migrations primary key."
}

Assert-True ($mainSeed.Contains(':ON ERROR EXIT')) 'Main seed launcher must stop before including DML after a guard failure.'
Assert-True ($mainSeed.Contains(':r Database\SqlServer\VerifySimplifiedReadViews_Postflight.sql')) `
    'Main seed launcher must enforce the final simplified read-view contract.'
Assert-True (-not $mainSeed.Contains(':r Database\SqlServer\VerifyQcConcise_Postflight.sql')) `
    'Final main seed must not run the intermediate exact-8-column QC postflight.'
Assert-True ($mainSeed.Contains(':r Documents_Refactor\SeedData_Refactor.Body.sql')) `
    'Main seed launcher does not include its guarded DML body.'
Assert-True ($mainSeedBody.Contains('BEGIN TRANSACTION;')) 'Main seed DML body is missing its transaction.'
Assert-True ($analyticsSeed.Contains(':ON ERROR EXIT')) 'Analytics seed launcher must stop before including DML after a guard failure.'
Assert-True ($analyticsSeed.Contains(':r Database\SqlServer\VerifySimplifiedReadViews_Postflight.sql')) `
    'Analytics seed launcher must enforce the final simplified read-view contract.'
Assert-True (-not $analyticsSeed.Contains(':r Database\SqlServer\VerifyQcConcise_Postflight.sql')) `
    'Final analytics seed must not run the intermediate exact-8-column QC postflight.'
Assert-True ($analyticsSeed.Contains(':r Database\SqlServer\SeedDemoAnalytics_Refactor.Body.sql')) `
    'Analytics seed launcher does not include its guarded DML body.'
Assert-True ($analyticsSeedBody.Contains('BEGIN TRANSACTION;')) 'Analytics seed DML body is missing its transaction.'

$repositoryText = (Get-ChildItem -LiteralPath (Join-Path $RepositoryRoot 'Repositories\SqlServer') -Filter '*.cs' -File |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw -Encoding UTF8 }) -join "`n"

$forbiddenOwnerReferences = @(
    '\bu\.user_id\b',
    '\br\.role_id\b',
    '\bsp\.seller_profile_id\b',
    '\bsa\.application_id\b',
    '\bb\.build_id\b',
    '\bbd\.build_id\b',
    '\bbr\.request_id\b',
    '\bk\.kit_id\b',
    '\bsw\.switch_id\b',
    '\bkc\.keycap_id\b',
    '\bst\.stab_id\b',
    '\ba\.accessory_id\b',
    '\bbi\.build_item_id\b',
    '\bbm\.mod_id\b',
    '\bd\.device_id\b',
    '\bdts\.session_id\b',
    '\bdktr\.key_test_id\b',
    '\bal\.log_id\b',
    '\bc\.conversation_id\b',
    '\bm\.message_id\b',
    '\bkeyboard_kits\.kit_id\b',
    '\bswitches\.switch_id\b',
    '\bkeycap_sets\.keycap_id\b',
    '\bstabilizers\.stab_id\b',
    '\baccessories\.accessory_id\b',
    '\bbuilds\.build_id\b',
    '\bbuild_items\.build_item_id\b',
    '\bbuild_mods\.mod_id\b',
    '\bbuild_requests\.request_id\b',
    '\bdevices\.device_id\b',
    '\bdevice_test_sessions\.session_id\b',
    '\bdevice_key_test_results\.key_test_id\b',
    '\baudit_log\.log_id\b',
    '\bchat_conversations\.conversation_id\b',
    '\bchat_messages\.message_id\b'
)

foreach ($pattern in $forbiddenOwnerReferences) {
    Assert-True (-not [regex]::IsMatch($repositoryText, $pattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)) `
        "Repository SQL still contains forbidden owner-PK reference matching $pattern."
}

$sellerRepository = Read-Text 'Repositories\SqlServer\SqlSellerRepository.cs'
Assert-True ([regex]::IsMatch($sellerRepository, '(?is)private\s+const\s+string\s+BaseSelectSql\s*=.*?\bid\s+AS\s+seller_profile_id\b')) `
    'SqlSellerRepository.BaseSelectSql must alias id as seller_profile_id.'

foreach ($viewName in @('Last_QC', 'Catalog_Comps', 'Build_items', 'Req_view')) {
    Assert-True ($simplifiedReadViews.Contains("CREATE VIEW views.$viewName")) `
        "Final read-view snapshot is missing views.$viewName."
}

Assert-True (-not $simplifiedReadViews.Contains('CREATE OR ALTER VIEW')) `
    'Final simplified view definitions must use CREATE VIEW without ALTER.'
foreach ($viewComment in @(
    '-- Last_QC:',
    '-- Catalog_Comps:',
    '-- Build_items:',
    '-- Req_view:'
)) {
    Assert-True ($simplifiedReadViews.Contains($viewComment)) `
        "Final simplified view definitions are missing purpose comment $viewComment."
}

Assert-True ($readViews.Contains(':r Database\SqlServer\ReadViews_Simplified_20260727.sql')) `
    'Canonical ReadViews.sql does not include the immutable final snapshot.'
Assert-True ($baselineReadViews.Contains('CREATE OR ALTER VIEW views.Last_QC')) `
    'Immutable baseline read-view file is missing Last_QC.'
Assert-True (-not $baselineReadViews.Contains('session_row.completed_at')) `
    'Immutable baseline read-view file must remain on the pre-completion-time contract.'
Assert-True ($readViewsMigration.Contains(':r Database\SqlServer\ReadViews_20260727.sql')) `
    'Baseline read-view migration must include its immutable view definitions.'
Assert-True (
    $readViewsMigration.Contains($qcViewSchemaVersion) -and
    $readViewsMigration.Contains('Read-view baseline cannot be reapplied after QC/view hardening.')
) 'Baseline read-view migration does not guard against downgrading the final view contract.'
Assert-True ($qcViewMigration.Contains(':r Database\SqlServer\ReadViews_QcViewHardening_20260727.sql')) `
    'QC/view hardening migration must include its immutable final view definitions.'
Assert-True (
    $qcViewMigration.Contains($expectedSchemaVersion) -and
    $qcViewMigration.Contains('QC/view hardening cannot be reapplied after simplified read views.')
) 'QC/view hardening migration does not guard against downgrading the simplified view contract.'
Assert-True ($simplifiedViewMigration.Contains(':r Database\SqlServer\ReadViews_Simplified_20260727.sql')) `
    'Simplified-view migration must include its immutable final view definitions.'
Assert-True ($simplifiedReadViews.Contains('session_row.completed_at')) `
    'Final Last_QC does not use the authoritative QC completion timestamp.'
Assert-True ($simplifiedReadViews.Contains('AS decimal(28,2)')) `
    'Final Build_items view does not widen line_total_snapshot to decimal(28,2).'
Assert-True ($simplifiedViewMigration.Contains("OBJECT_ID(N'views.Catalog_Comps')) <> 7")) `
    'Simplified-view migration does not enforce the 7-column Catalog_Comps contract.'
Assert-True ($simplifiedViewMigration.Contains("OBJECT_ID(N'views.Req_view')) <> 15")) `
    'Simplified-view migration does not enforce the 15-column Req_view contract.'

Assert-True ($repositoryText.Contains('views.Last_QC')) 'Repositories do not read from views.Last_QC.'
Assert-True ($repositoryText.Contains('views.Catalog_Comps')) 'Repositories do not read from views.Catalog_Comps.'
Assert-True ($repositoryText.Contains('views.Build_items')) 'Repositories do not read from views.Build_items.'
Assert-True ($repositoryText.Contains('views.Req_view')) 'Repositories do not read from views.Req_view.'
Assert-True (-not [regex]::IsMatch(
    $repositoryText,
    '(?is)\b(?:INSERT\s+INTO|UPDATE|DELETE\s+FROM|MERGE)\s+views\.'
)) 'Repository DML must never target read views.'

Write-Host 'Schema source verification passed: 21 PKs, 30 FKs, final 8/12 QC contract, four simplified read views, and ordered migration guards.'
