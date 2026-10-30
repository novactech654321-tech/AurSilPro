# Exports photos/voice clips from the MediaCaptures table into real files you can open/play.
# Usage:  powershell -ExecutionPolicy Bypass -File Database\export-media.ps1
#         powershell -ExecutionPolicy Bypass -File Database\export-media.ps1 -OutDir "E:\AurSil\media-export" -MediaId 1
param(
    [string]$OutDir = "E:\AurSil\media-export",
    [long]$MediaId = 0   # 0 = export every row
)

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$cs = "Server=(localdb)\MSSQLLocalDB;Database=aursil;Integrated Security=True;TrustServerCertificate=True"

$conn = New-Object System.Data.SqlClient.SqlConnection $cs
$conn.Open()
$cmd = $conn.CreateCommand()
if ($MediaId -gt 0) {
    $cmd.CommandText = "SELECT MediaId, Kind, ContentType, Data FROM MediaCaptures WHERE MediaId = @id"
    [void]$cmd.Parameters.AddWithValue("@id", $MediaId)
} else {
    $cmd.CommandText = "SELECT MediaId, Kind, ContentType, Data FROM MediaCaptures ORDER BY MediaId"
}

$reader = $cmd.ExecuteReader()
$count = 0
while ($reader.Read()) {
    $id   = $reader["MediaId"]
    $kind = $reader["Kind"]
    $ct   = [string]$reader["ContentType"]
    $bytes = $reader["Data"]

    $ext = switch -Wildcard ($ct) {
        "image/png"  { "png"; break }
        "image/*"    { "jpg"; break }
        "audio/*"    { "m4a"; break }
        default      { "bin" }
    }
    $path = Join-Path $OutDir ("{0}-{1}.{2}" -f $kind, $id, $ext)
    [System.IO.File]::WriteAllBytes($path, $bytes)
    Write-Host ("Wrote {0}  ({1} bytes, {2})" -f $path, $bytes.Length, $ct)
    $count++
}
$reader.Close()
$conn.Close()
Write-Host ("Done. {0} file(s) in {1}" -f $count, $OutDir)
