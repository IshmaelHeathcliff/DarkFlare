[CmdletBinding()]
param(
    [string]$PythonPath = "python"
)

$ErrorActionPreference = "Stop"

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$manifestRoot = Join-Path $projectRoot "Docs/docs/assets/visual-assets/alpha-0.1/manifests"
$reportRoot = Join-Path $projectRoot "Temp/VisualAssetAudits/alpha-0.1/reports"
$auditorPath = Join-Path $projectRoot ".agents/skills/unity-2d-sprite-workflow/scripts/audit_sprite_assets.py"

if (-not (Test-Path -LiteralPath $auditorPath -PathType Leaf))
{
    throw "找不到内置审计器：$auditorPath"
}

if (-not (Test-Path -LiteralPath $manifestRoot -PathType Container))
{
    throw "找不到审计合同目录：$manifestRoot"
}

New-Item -ItemType Directory -Path $reportRoot -Force | Out-Null

$manifests = Get-ChildItem -LiteralPath $manifestRoot -Filter "*.json" -File | Sort-Object Name
if ($manifests.Count -eq 0)
{
    throw "审计合同目录中没有 JSON：$manifestRoot"
}

$failed = [System.Collections.Generic.List[string]]::new()
foreach ($manifest in $manifests)
{
    $reportPath = Join-Path $reportRoot ($manifest.BaseName + ".report.json")
    Write-Host "[AUDIT] $($manifest.Name)"
    & $PythonPath $auditorPath $manifest.FullName --json-report $reportPath
    if ($LASTEXITCODE -ne 0)
    {
        $failed.Add($manifest.Name)
    }
}

if ($failed.Count -gt 0)
{
    throw "美术资产审计失败：$($failed -join ', ')"
}

Write-Host "[AUDIT] 全部 $($manifests.Count) 个合同通过；报告：$reportRoot"
