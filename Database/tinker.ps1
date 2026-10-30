# AurSil "tinker" - an interactive REPL for password hashes, like `php artisan tinker`.
# Start it with:   powershell -NoExit -ExecutionPolicy Bypass -File Database\tinker.ps1
# Then type, for example:
#   hash  'NewPass123'
#   check 'guess123' '100000.<salt>.<hash>'
#   users
#   reset 'admin' 'NewPass123'
#
# NOTE: like Laravel's Hash, there is NO function that returns a plaintext password
#       from a hash - a hash is one-way. 'check' only answers true/false.

$global:Iterations = 100000
$global:Cs = "(localdb)\MSSQLLocalDB"

function global:hash([string]$password) {
    $salt = New-Object byte[] 16
    ([System.Security.Cryptography.RandomNumberGenerator]::Create()).GetBytes($salt)
    $d = New-Object System.Security.Cryptography.Rfc2898DeriveBytes($password, $salt, $Iterations, [System.Security.Cryptography.HashAlgorithmName]::SHA256)
    "{0}.{1}.{2}" -f $Iterations, [Convert]::ToBase64String($salt), [Convert]::ToBase64String($d.GetBytes(32))
}

function global:check([string]$password, [string]$stored) {
    $p = $stored.Split('.')
    $salt = [Convert]::FromBase64String($p[1]); $expected = [Convert]::FromBase64String($p[2])
    $d = New-Object System.Security.Cryptography.Rfc2898DeriveBytes($password, $salt, [int]$p[0], [System.Security.Cryptography.HashAlgorithmName]::SHA256)
    [System.Linq.Enumerable]::SequenceEqual([byte[]]$d.GetBytes($expected.Length), [byte[]]$expected)
}

function global:users() {
    sqlcmd -S $Cs -d aursil -I -Q "SET QUOTED_IDENTIFIER ON; SELECT UserId, Status, Role, PasswordHash FROM Users;"
}

function global:gethash([string]$userId) {
    (sqlcmd -S $Cs -d aursil -I -h -1 -W -Q "SET QUOTED_IDENTIFIER ON; SET NOCOUNT ON; SELECT PasswordHash FROM Users WHERE UserId='$userId';").Trim()
}

function global:reset([string]$userId, [string]$password) {
    $h = hash $password
    sqlcmd -S $Cs -d aursil -I -Q "SET QUOTED_IDENTIFIER ON; UPDATE Users SET PasswordHash='$h' WHERE UserId='$userId';" | Out-Null
    "Password for '$userId' is now: $password"
}

Write-Host ""
Write-Host "AurSil tinker ready. Commands:" -ForegroundColor Cyan
Write-Host "  hash 'pw'                  -> make a hash"
Write-Host "  check 'pw' 'storedHash'    -> True / False"
Write-Host "  users                      -> list users + hashes"
Write-Host "  gethash 'userId'           -> that user's stored hash"
Write-Host "  reset 'userId' 'newPw'     -> set a new password"
Write-Host ""
