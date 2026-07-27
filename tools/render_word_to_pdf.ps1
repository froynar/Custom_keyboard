param(
    [Parameter(Mandatory = $true)]
    [string]$InputDocx,
    [Parameter(Mandatory = $true)]
    [string]$OutputPdf,
    [switch]$OpenAndRepair
)

$ErrorActionPreference = 'Stop'
$inputPath = (Resolve-Path -LiteralPath $InputDocx).Path
$outputPath = [System.IO.Path]::GetFullPath($OutputPdf)
$outputDirectory = [System.IO.Path]::GetDirectoryName($outputPath)

if (-not [System.IO.Directory]::Exists($outputDirectory)) {
    [System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
}

$word = $null
$document = $null
try {
    $word = New-Object -ComObject Word.Application
    $word.Visible = $false
    $word.DisplayAlerts = 0
    if ($OpenAndRepair) {
        # Positional argument 13 is OpenAndRepair in Word.Documents.Open.
        $document = $word.Documents.Open(
            $inputPath, $false, $true, $false, '', '', $false, '', '', 0, 0, $false, $true
        )
    }
    else {
        $document = $word.Documents.Open($inputPath, $false, $true, $false)
    }

    foreach ($story in $document.StoryRanges) {
        try { $story.Fields.Update() | Out-Null } catch { }
    }

    # 17 = wdExportFormatPDF; 0 = optimized for print.
    $document.ExportAsFixedFormat($outputPath, 17, $false, 0, 0, 1, 9999, 0, $true, $true, 1, $true, $true, $false)
}
finally {
    if ($null -ne $document) {
        $document.Close($false)
        [System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($document) | Out-Null
    }
    if ($null -ne $word) {
        $word.Quit()
        [System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($word) | Out-Null
    }
    [GC]::Collect()
    [GC]::WaitForPendingFinalizers()
}

Write-Output $outputPath
