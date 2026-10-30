# AurSil password helper. Passwords are PBKDF2-SHA256 hashes (one-way) - they CANNOT be decrypted.
# This tool can only (a) make a hash, (b) check a guess against a stored hash, (c) reset a user's password.
#
# Examples (run from E:\AurSil):
#   powershell -ExecutionPolicy Bypass -File Database\password-tool.ps1 -Mode hash   -Password "NewPass123"
#   powershell -ExecutionPolicy Bypass -File Database\password-tool.ps1 -Mode verify -Password "guess123" -Stored "100000.AbC...=.XyZ...="
#   powershell -ExecutionPolicy Bypass -File Database\password-tool.ps1 -Mode reset  -Password "NewPass123" -UserId admin
param(
    [ValidateSet("hash","verify","reset")] [string]$Mode = "hash",
    [string]$Password,
    [string]$Stored,     # only for verify
    [string]$UserId      # only for reset
)

$Iterations = 100000
function New-Hash([string]$pw) {
    $salt = New-Object byte[] 16
    ([System.Security.Cryptography.RandomNumberGenerator]::Create()).GetBytes($salt)
    $d = New-Object System.Security.Cryptography.Rfc2898DeriveBytes($pw, $salt, $Iterations, [System.Security.Cryptography.HashAlgorithmName]::SHA256)
    $key = $d.GetBytes(32)
    return "{0}.{1}.{2}" -f $Iterations, [Convert]::ToBase64String($salt), [Convert]::ToBase64String($key)
}

switch ($Mode) {
    "hash" {
        if (-not $Password) { Write-Host "Give -Password"; exit 1 }
        Write-Host (New-Hash $Password)
    }
    "verify" {
        if (-not $Password -or -not $Stored) { Write-Host "Give -Password and -Stored"; exit 1 }
        $parts = $Stored.Split('.')
        $iter = [int]$parts[0]; $salt = [Convert]::FromBase64String($parts[1]); $expected = [Convert]::FromBase64String($parts[2])
        $d = New-Object System.Security.Cryptography.Rfc2898DeriveBytes($Password, $salt, $iter, [System.Security.Cryptography.HashAlgorithmName]::SHA256)
        $actual = $d.GetBytes($expected.Length)
        $match = [System.Linq.Enumerable]::SequenceEqual([byte[]]$actual, [byte[]]$expected)
        if ($match) { Write-Host "MATCH - that is the password." -ForegroundColor Green }
        else        { Write-Host "NO MATCH." -ForegroundColor Red }
    }
    "reset" {
        if (-not $Password -or -not $UserId) { Write-Host "Give -Password and -UserId"; exit 1 }
        $hash = New-Hash $Password
        $sql = "SET QUOTED_IDENTIFIER ON; UPDATE Users SET PasswordHash = '$hash' WHERE UserId = '$UserId';"
        sqlcmd -S "(localdb)\MSSQLLocalDB" -d aursil -I -Q $sql
        Write-Host ("Password for '{0}' set to: {1}" -f $UserId, $Password) -ForegroundColor Green
    }
}
